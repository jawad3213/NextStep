# ============================================================
# app/domain/email_composer/service.py
#
# Business logic for the Email Composer domain.
#
# Two modes:
#   1. Direct mode  — FastAPI /email/generate calls generate_email_with_llm(request)
#   2. Pipeline mode — email_composer_node calls generate_email_from_pipeline_state(state)
#
# The LLM factory (get_email_llm) is kept in email_engine/llm.py and re-imported
# here so the existing Docker env vars (EMAIL_LLM_PROVIDER, EMAIL_LLM_MODEL) are
# unchanged.
# ============================================================
import os
import logging
import json
import re
from langchain_core.prompts import ChatPromptTemplate

from .llm import get_email_llm
from app.domain.email_composer.schemas.models import (
    CandidateInput,
    JobOfferInput,
    EmailOptions,
    GenerateEmailRequest,
    GenerateEmailResponse,
    GenerateFollowUpEmailRequest,
    ClassifyResponseRequest,
    ClassifyResponseResult,
    GenerateReplyEmailRequest,
    SkillGapInput,
    CompanyIntelligenceInput,
)
from app.domain.email_composer.schemas.state import EmailComposerState
from app.domain.email_composer.agents.prompts import (
    APPLICATION_SYSTEM,
    APPLICATION_HUMAN,
    FOLLOWUP_SYSTEM,
    FOLLOWUP_HUMAN,
    CLASSIFY_SYSTEM,
    CLASSIFY_HUMAN,
    REPLY_SYSTEM,
    REPLY_HUMAN,
)

logger = logging.getLogger(__name__)


def _with_structured_output(llm, response_model):
    provider = os.getenv("EMAIL_LLM_PROVIDER", "").lower().strip()
    class_name = llm.__class__.__name__.lower()
    if not provider and "groq" in class_name:
        provider = "groq"
    if provider == "groq":
        return llm.with_structured_output(response_model, method="json_mode")
    return llm.with_structured_output(response_model)


# Key aliases the LLM sometimes returns instead of the correct Pydantic field names
_SUBJECT_ALIASES = {"subject", "objet", "sujet", "object", "titre", "title"}
_BODY_ALIASES    = {"body", "corps", "contenu", "message", "texte", "content", "email_body", "email"}
_LANG_ALIASES    = {"language", "langue", "lang"}
_TONE_ALIASES    = {"tone", "ton", "style"}
_CLASSIFY_TYPE_ALIASES = {
    "INTERVIEW_PROPOSED": "ENTRETIEN_PROPOSE",
    "INTERVIEW_SCHEDULED": "ENTRETIEN_PROPOSE",
    "ENTRETIEN": "ENTRETIEN_PROPOSE",
    "INTERVIEW": "ENTRETIEN_PROPOSE",
    "MORE_INFO_REQUESTED": "INFORMATIONS_DEMANDEES",
    "INFO_REQUESTED": "INFORMATIONS_DEMANDEES",
    "INFORMATION_DEMANDEE": "INFORMATIONS_DEMANDEES",
    "INFORMATIONS_DEMANDEE": "INFORMATIONS_DEMANDEES",
    "ACCEPTED": "ACCEPTE",
    "REJECTED": "REFUSE",
    "AUTO_REPLY": "REPONSE_AUTOMATIQUE",
    "AUTOREPLY": "REPONSE_AUTOMATIQUE",
    "GENERAL_REPLY": "REPONSE_GENERALE",
    "UNKNOWN": "INCONNU",
}
_VALID_CLASSIFY_TYPES = {
    "ENTRETIEN_PROPOSE",
    "INFORMATIONS_DEMANDEES",
    "ACCEPTE",
    "REFUSE",
    "REPONSE_AUTOMATIQUE",
    "REPONSE_GENERALE",
    "INCONNU",
}


def _coerce_email_response(raw, options_language: str = "fr", options_tone: str = "professionnel") -> GenerateEmailResponse:
    """
    Safety net: if the LLM returns a dict with wrong field names
    (e.g. 'corps' instead of 'body'), remap them to what Pydantic expects.
    If `raw` is already a GenerateEmailResponse, return it as-is.
    """
    if isinstance(raw, GenerateEmailResponse):
        return raw

    if not isinstance(raw, dict):
        # Try to access attributes (some parsers return objects)
        try:
            return GenerateEmailResponse(
                subject=getattr(raw, "subject", "") or "",
                body=getattr(raw, "body", "") or "",
                language=getattr(raw, "language", options_language) or options_language,
                tone=getattr(raw, "tone", options_tone) or options_tone,
            )
        except Exception:
            raise ValueError(f"Cannot parse LLM response: {raw!r}")

    raw_lower = {k.lower().strip(): v for k, v in raw.items()}

    def _pick(aliases: set) -> str:
        for alias in aliases:
            if alias in raw_lower and raw_lower[alias]:
                return str(raw_lower[alias])
        return ""

    subject  = _pick(_SUBJECT_ALIASES)
    body     = _pick(_BODY_ALIASES)
    language = _pick(_LANG_ALIASES) or options_language
    tone     = _pick(_TONE_ALIASES) or options_tone

    if not subject and not body:
        logger.error("_coerce_email_response: no subject/body found in keys: %s", list(raw.keys()))
        raise ValueError(f"LLM returned unrecognisable keys: {list(raw.keys())}")

    logger.warning(
        "_coerce_email_response: remapped LLM keys %s -> subject/body/language/tone",
        list(raw.keys()),
    )
    return GenerateEmailResponse(subject=subject, body=body, language=language, tone=tone)


# ─── Helpers ─────────────────────────────────────────────────────────────────

def _fmt(lst: list) -> str:
    """Format a list as a comma-separated string, or 'Non spécifié' if empty."""
    filtered = [str(x).strip() for x in lst if str(x).strip()]
    return ", ".join(filtered) if filtered else "Non spécifié"


def _truncate(text: str | None, max_chars: int = 2000) -> str:
    if not text:
        return "Non spécifié"
    return text[:max_chars] + ("…" if len(text) > max_chars else "")


def _safe(value: str | None) -> str:
    return value or "Non spécifié"


def _normalize_classify_type(value: str | None) -> str:
    raw = (value or "").strip().upper().replace("-", "_").replace(" ", "_")
    normalized = _CLASSIFY_TYPE_ALIASES.get(raw, raw)
    return normalized if normalized in _VALID_CLASSIFY_TYPES else "INCONNU"


def _strip_quoted_sections(text: str | None) -> str:
    if not text:
        return ""
    normalized = text.replace("\r\n", "\n").strip()
    markers = [
        r"\nLe .{0,80} a écrit\s*:",
        r"\nOn .{0,80} wrote\s*:",
        r"\n-----Original Message-----",
        r"\nDe\s*:",
        r"\nFrom\s*:",
        r"\n>{1,}",
    ]
    cut = len(normalized)
    for marker in markers:
        match = re.search(marker, normalized, flags=re.IGNORECASE)
        if match:
            cut = min(cut, match.start())
    cleaned = normalized[:cut].strip()
    return cleaned or normalized[:800]


def _extract_first_json_object(text: str) -> dict | None:
    start = text.find("{")
    if start < 0:
        return None

    depth = 0
    in_string = False
    escape = False
    for i in range(start, len(text)):
        ch = text[i]
        if in_string:
            if escape:
                escape = False
            elif ch == "\\":
                escape = True
            elif ch == "\"":
                in_string = False
            continue
        if ch == "\"":
            in_string = True
        elif ch == "{":
            depth += 1
        elif ch == "}":
            depth -= 1
            if depth == 0:
                candidate = text[start : i + 1]
                try:
                    parsed = json.loads(candidate)
                    return parsed if isinstance(parsed, dict) else None
                except Exception:
                    return None
    return None


def _coerce_classify_response(raw, language: str = "fr") -> ClassifyResponseResult:
    if isinstance(raw, ClassifyResponseResult):
        return raw

    data: dict | None = None
    if isinstance(raw, dict):
        data = raw
    elif hasattr(raw, "model_dump"):
        dumped = raw.model_dump()
        data = dumped if isinstance(dumped, dict) else None
    else:
        content = getattr(raw, "content", str(raw)) or ""
        content = str(content).strip()
        if content.startswith("```"):
            content = re.sub(r"^```(?:json)?\s*", "", content, flags=re.IGNORECASE)
            content = re.sub(r"\s*```$", "", content)
        try:
            parsed = json.loads(content)
            if isinstance(parsed, dict):
                data = parsed
        except Exception:
            data = _extract_first_json_object(content)

    if not data:
        raise ValueError("Unable to parse classification JSON output from LLM.")

    response_type = _normalize_classify_type(
        data.get("response_type") or data.get("category") or data.get("type") or data.get("label")
    )

    confidence_raw = data.get("confidence", 0.55)
    try:
        confidence = float(confidence_raw)
    except Exception:
        confidence = 0.55
    if confidence > 1 and confidence <= 100:
        confidence /= 100.0
    confidence = max(0.0, min(1.0, confidence))

    summary = str(data.get("summary") or "Réponse analysée automatiquement.")
    recommended_action = str(data.get("recommended_action") or "Vérifiez la réponse et adaptez votre prochaine action.")

    should_generate = data.get("should_generate_reply_draft")
    if isinstance(should_generate, str):
        should_generate = should_generate.strip().lower() in {"true", "1", "yes", "oui"}
    if not isinstance(should_generate, bool):
        should_generate = response_type in {"ENTRETIEN_PROPOSE", "INFORMATIONS_DEMANDEES", "REPONSE_GENERALE"}

    return ClassifyResponseResult(
        response_type=response_type,
        confidence=confidence,
        summary=summary,
        recommended_action=recommended_action,
        should_generate_reply_draft=should_generate,
    )


# ─── Direct mode: Application email ─────────────────────────────────────────

async def generate_email_with_llm(
    request: GenerateEmailRequest,
) -> GenerateEmailResponse:
    """
    Generate a job-application email using the LLM.

    Accepts optional skill_gap and company_intelligence enrichment.
    These are injected into the prompt only when present — absence is graceful.
    Called by FastAPI /email/generate (direct mode) and by pipeline mode.
    """
    logger.info(
        "EmailComposer — generate [direct] candidature_id=%s | lang=%s | tone=%s",
        request.candidature_id,
        request.options.language,
        request.options.tone,
    )

    llm = get_email_llm()
    structured_llm = _with_structured_output(llm, GenerateEmailResponse)
    prompt = ChatPromptTemplate.from_messages(
        [("system", APPLICATION_SYSTEM), ("human", APPLICATION_HUMAN)]
    )
    chain = prompt | structured_llm

    c = request.candidate
    j = request.job_offer
    o = request.options
    sg = request.skill_gap
    ci = request.company_intelligence

    # ── Map Skill Gap ────────────────────────────────────────
    # Handle possible field name variations between agents
    sg_matching = []
    sg_recomms = []
    if sg:
        # Pydantic or dict
        sg_dict = sg.model_dump() if hasattr(sg, "model_dump") else sg if isinstance(sg, dict) else {}
        sg_matching = sg_dict.get("matching_skills") or sg_dict.get("matched_skills") or sg_dict.get("competences_communes") or []
        sg_recomms = sg_dict.get("recommendations") or sg_dict.get("revision_hints") or []
        # Handle list of dicts for recommendations (Recommendation objects)
        if sg_recomms and len(sg_recomms) > 0 and isinstance(sg_recomms[0], dict):
            sg_recomms = [r.get("title", str(r)) for r in sg_recomms]

    # ── Map Company Intelligence ─────────────────────────────
    ci_summary = "Non spécifié"
    ci_values = []
    ci_products = []
    ci_context = "Non spécifié"
    if ci:
        ci_dict = ci.model_dump() if hasattr(ci, "model_dump") else ci if isinstance(ci, dict) else {}
        # If it's the full nested result from the company agent
        intel = ci_dict.get("intelligence") or ci_dict
        ci_summary = intel.get("summary") or intel.get("resume_entreprise") or "Non spécifié"
        ci_values = intel.get("values") or intel.get("valeurs") or intel.get("culture", {}).get("key_values") or []
        ci_products = intel.get("products_or_services") or intel.get("produits") or []
        ci_context = intel.get("recent_context") or (
            intel.get("actualites", [""])[0] if intel.get("actualites") else "Non spécifié"
        )

    raw = await chain.ainvoke(
        {
            # Candidate
            "full_name":    _safe(c.full_name),
            "email":        _safe(c.email),
            "phone":        _safe(c.phone),
            "current_title": _safe(c.current_title),
            "skills":       _fmt(c.skills),
            "experiences":  _fmt(c.experiences),
            "education":    _fmt(c.education),
            "projects":     _fmt(c.projects),
            "certifications": _fmt(c.certifications),
            # Offer
            "job_title":       j.job_title,
            "company_name":    j.company_name or "votre entreprise",
            "location":        _safe(j.location),
            "required_skills": _fmt(j.required_skills),
            "preferred_skills": _fmt(j.preferred_skills),
            "missions":        _fmt(j.missions),
            "requirements":    _fmt(j.requirements),
            "raw_text":        _truncate(j.raw_text),
            # Skill gap enrichment
            "matching_skills":      _fmt(sg_matching),
            "skill_recommendations": _fmt(sg_recomms),
            # Company intelligence enrichment
            "company_summary":  _truncate(ci_summary, 600),
            "company_values":   _fmt(ci_values),
            "company_products": _fmt(ci_products),
            "company_context":  _truncate(ci_context, 300),
            # Options
            "language":                   o.language,
            "tone":                       o.tone,
            "include_motivation_letter":  "Oui" if o.include_motivation_letter else "Non",
        }
    )
    result = _coerce_email_response(raw, options_language=o.language, options_tone=o.tone)

    logger.info(
        "EmailComposer — generation succeeded candidature_id=%s | subject=%s",
        request.candidature_id,
        result.subject[:60] if result.subject else "",
    )
    return result


# ─── Pipeline mode: build request from state, then generate ──────────────────

async def generate_email_from_pipeline_state(
    state: EmailComposerState,
) -> GenerateEmailResponse | dict:
    """
    Called by email_composer_node in the pipeline.

    Builds a GenerateEmailRequest from pipeline state fields, with graceful
    degradation if enrichment data is missing.

    Returns GenerateEmailResponse on success, or a dict with an "error" key
    on failure (so the node can surface it to state.errors without crashing).
    """
    profile_data: dict | None = state.get("profile_data")
    analyzed_offer: dict | None = state.get("analyzed_offer")
    raw_offer_text: str | None = state.get("raw_offer_text")
    skill_gap_raw: dict | None = state.get("skill_gap")
    company_intel_raw: dict | None = state.get("company_intelligence")
    gen_options: dict = state.get("generation_options") or {}
    candidature_id: str = state.get("candidature_id") or "pipeline"

    # ── Validate minimal required context ────────────────────
    if not profile_data and not (analyzed_offer or raw_offer_text):
        msg = "EmailComposer: both profile_data and offer context are missing — cannot generate."
        logger.error(msg)
        return {"error": msg}

    # ── Build CandidateInput from profile_data ────────────────
    if profile_data:
        candidate = CandidateInput(
            full_name=profile_data.get("full_name") or profile_data.get("nom_complet") or "Candidat",
            email=profile_data.get("email"),
            phone=profile_data.get("phone") or profile_data.get("telephone"),
            current_title=profile_data.get("current_title") or profile_data.get("titre_actuel"),
            skills=profile_data.get("skills") or profile_data.get("competences") or [],
            experiences=profile_data.get("experiences") or [],
            education=profile_data.get("education") or profile_data.get("formations") or [],
            projects=profile_data.get("projects") or profile_data.get("projets") or [],
            certifications=profile_data.get("certifications") or [],
        )
    else:
        logger.warning("EmailComposer: profile_data missing — using empty candidate.")
        candidate = CandidateInput(full_name="Candidat")

    # ── Build JobOfferInput from analyzed_offer or raw_text ───
    if analyzed_offer:
        job_offer = JobOfferInput(
            job_title=analyzed_offer.get("titre") or analyzed_offer.get("job_title") or "Poste",
            company_name=analyzed_offer.get("entreprise") or analyzed_offer.get("company_name"),
            location=analyzed_offer.get("localisation") or analyzed_offer.get("location"),
            required_skills=analyzed_offer.get("competences_requises") or analyzed_offer.get("required_skills") or [],
            preferred_skills=analyzed_offer.get("competences_souhaitees") or analyzed_offer.get("preferred_skills") or [],
            missions=analyzed_offer.get("missions") or [],
            requirements=analyzed_offer.get("prerequis") or analyzed_offer.get("requirements") or [],
            raw_text=raw_offer_text,
            analysis_json=analyzed_offer,
        )
    elif raw_offer_text:
        logger.warning("EmailComposer: analyzed_offer missing — using raw_offer_text only.")
        job_offer = JobOfferInput(
            job_title="Poste",
            raw_text=raw_offer_text,
        )
    else:
        logger.warning("EmailComposer: no offer context — using empty offer.")
        job_offer = JobOfferInput(job_title="Poste")

    # ── Build optional SkillGapInput from pipeline state ─────
    skill_gap_input: SkillGapInput | None = None
    if skill_gap_raw:
        try:
            skill_gap_input = SkillGapInput(
                matching_skills=skill_gap_raw.get("matching_skills") or skill_gap_raw.get("competences_communes") or [],
                missing_skills=skill_gap_raw.get("missing_skills") or skill_gap_raw.get("competences_manquantes") or [],
                important_missing_skills=skill_gap_raw.get("important_missing_skills") or [],
                minor_missing_skills=skill_gap_raw.get("minor_missing_skills") or [],
                recommendations=skill_gap_raw.get("recommendations") or [],
                score=skill_gap_raw.get("score") or skill_gap_raw.get("ats_score"),
            )
        except Exception as e:
            logger.warning("EmailComposer: failed to parse skill_gap — skipping. (%s)", e)

    # ── Build optional CompanyIntelligenceInput from pipeline state ─
    company_intel_input: CompanyIntelligenceInput | None = None
    if company_intel_raw:
        try:
            intel = company_intel_raw.get("intelligence") or company_intel_raw
            company_intel_input = CompanyIntelligenceInput(
                summary=intel.get("summary") or intel.get("resume_entreprise"),
                values=intel.get("values") or intel.get("valeurs") or [],
                products_or_services=intel.get("products_or_services") or intel.get("produits") or [],
                recent_context=intel.get("recent_context") or (
                    intel.get("actualites", [{}])[0].get("title") if intel.get("actualites") else None
                ),
                recommendations=company_intel_raw.get("recommendations") or [],
                score=company_intel_raw.get("score"),
            )
        except Exception as e:
            logger.warning("EmailComposer: failed to parse company_intelligence — skipping. (%s)", e)

    # ── Build EmailOptions from generation_options ────────────
    options = EmailOptions(
        language=gen_options.get("language", "fr"),
        tone=gen_options.get("tone", "professionnel"),
        include_motivation_letter=gen_options.get("include_motivation_letter", False),
    )

    request = GenerateEmailRequest(
        candidature_id=candidature_id,
        candidate=candidate,
        job_offer=job_offer,
        options=options,
        skill_gap=skill_gap_input.model_dump() if skill_gap_input else None,
        company_intelligence=company_intel_input.model_dump() if company_intel_input else None,
    )

    return await generate_email_with_llm(request)


# ─── Direct mode: Follow-up email ────────────────────────────────────────────

async def generate_follow_up_email_with_llm(
    request: GenerateFollowUpEmailRequest,
) -> GenerateEmailResponse:
    logger.info(
        "EmailComposer — generate-follow-up candidature_id=%s | lang=%s | days=%s",
        request.candidature_id,
        request.options.language,
        request.options.days_since_sent,
    )
    llm = get_email_llm()
    structured_llm = _with_structured_output(llm, GenerateEmailResponse)
    prompt = ChatPromptTemplate.from_messages(
        [("system", FOLLOWUP_SYSTEM), ("human", FOLLOWUP_HUMAN)]
    )
    chain = prompt | structured_llm

    c = request.candidate
    j = request.job_offer
    p = request.previous_email
    o = request.options
    days_label = str(o.days_since_sent) if o.days_since_sent is not None else "Non spécifié"

    raw = await chain.ainvoke(
        {
            "full_name":     _safe(c.full_name),
            "email":         _safe(c.email),
            "phone":         _safe(c.phone),
            "current_title": _safe(c.current_title),
            "skills":        _fmt(c.skills),
            "experiences":   _fmt(c.experiences),
            "education":     _fmt(c.education),
            "projects":      _fmt(c.projects),
            "certifications": _fmt(c.certifications),
            "job_title":      j.job_title,
            "company_name":   j.company_name or "votre entreprise",
            "location":       _safe(j.location),
            "required_skills": _fmt(j.required_skills),
            "preferred_skills": _fmt(j.preferred_skills),
            "missions":       _fmt(j.missions),
            "requirements":   _fmt(j.requirements),
            "previous_subject": _safe(p.subject),
            "previous_body":    _truncate(p.body, 1500),
            "sent_at_utc":      _safe(p.sent_at_utc),
            "days_since_sent":  days_label,
            "language": o.language,
            "tone":     o.tone,
        }
    )
    result = _coerce_email_response(raw, options_language=o.language, options_tone=o.tone)
    logger.info("EmailComposer — follow-up succeeded candidature_id=%s", request.candidature_id)
    return result


# ─── Direct mode: Response classification ────────────────────────────────────

async def classify_recruiter_response_with_llm(
    request: ClassifyResponseRequest,
) -> ClassifyResponseResult:
    logger.info(
        "EmailComposer — classify-response candidature_id=%s | lang=%s",
        request.candidature_id,
        request.language,
    )
    llm = get_email_llm()
    reply_snippet = _strip_quoted_sections(request.reply_snippet)
    payload = {
        "job_title":              _safe(request.job_title),
        "company_name":           _safe(request.company_name),
        "previous_email_subject": _safe(request.previous_email_subject),
        "previous_email_body":    _truncate(request.previous_email_body, 500),
        "reply_from":             _safe(request.reply_from),
        "reply_date_utc":         _safe(request.reply_date_utc),
        "reply_subject":          _safe(request.reply_subject),
        "reply_snippet":          _truncate(reply_snippet, 1000),
        "language":               request.language,
    }

    prompt = ChatPromptTemplate.from_messages(
        [("system", CLASSIFY_SYSTEM), ("human", CLASSIFY_HUMAN)]
    )

    try:
        structured_llm = _with_structured_output(llm, ClassifyResponseResult)
        chain = prompt | structured_llm
        raw = await chain.ainvoke(payload)
        result = _coerce_classify_response(raw, request.language)
    except Exception as ex:
        logger.warning(
            "EmailComposer — classify structured parsing failed for candidature_id=%s. Retrying semantic pass. Error=%s",
            request.candidature_id,
            ex,
        )

        fallback_system = (
            "Tu es un classificateur sémantique de réponses RH. "
            "Retourne UNIQUEMENT un JSON avec clés: response_type, confidence, summary, recommended_action, should_generate_reply_draft. "
            "response_type doit être l'un de: ENTRETIEN_PROPOSE, INFORMATIONS_DEMANDEES, ACCEPTE, REFUSE, REPONSE_AUTOMATIQUE, REPONSE_GENERALE, INCONNU. "
            "Analyse le sens global, pas seulement des mots-clés."
        )
        fallback_prompt = ChatPromptTemplate.from_messages(
            [("system", fallback_system), ("human", CLASSIFY_HUMAN)]
        )
        fallback_chain = fallback_prompt | llm
        raw_fallback = await fallback_chain.ainvoke(payload)
        result = _coerce_classify_response(raw_fallback, request.language)

    if result.response_type not in _VALID_CLASSIFY_TYPES:
        result.response_type = "INCONNU"

    logger.info(
        "EmailComposer — classify result: %s (confidence=%s) candidature_id=%s",
        result.response_type, result.confidence, request.candidature_id,
    )
    return result


# ─── Direct mode: Reply draft generation ─────────────────────────────────────

async def generate_reply_email_with_llm(
    request: GenerateReplyEmailRequest,
) -> GenerateEmailResponse:
    logger.info(
        "EmailComposer — generate-reply candidature_id=%s | response_type=%s | lang=%s",
        request.candidature_id,
        request.response_type,
        request.language,
    )
    llm = get_email_llm()
    structured_llm = _with_structured_output(llm, GenerateEmailResponse)
    prompt = ChatPromptTemplate.from_messages(
        [("system", REPLY_SYSTEM), ("human", REPLY_HUMAN)]
    )
    chain = prompt | structured_llm

    raw = await chain.ainvoke(
        {
            "full_name":     request.candidate.full_name,
            "current_title": _safe(request.candidate.current_title),
            "email":         _safe(request.candidate.email),
            "job_title":     request.job_offer.job_title,
            "company_name":  _safe(request.job_offer.company_name),
            "previous_subject":  request.previous_email.subject   if request.previous_email else "Non spécifié",
            "previous_sent_at": request.previous_email.sent_at_utc if request.previous_email else "Non spécifié",
            "reply_from":        _safe(request.recruiter_reply.from_email),
            "reply_subject":     _safe(request.recruiter_reply.subject),
            "reply_received_at": _safe(request.recruiter_reply.received_at_utc),
            "reply_snippet":     _truncate(request.recruiter_reply.snippet, 800),
            "response_type":      request.response_type,
            "response_summary":   _safe(request.response_summary),
            "recommended_action": _safe(request.recommended_action),
            "user_instructions":  request.user_instructions or "Aucune instruction supplémentaire.",
            "language": request.language,
            "tone":     request.tone,
        }
    )
    result = _coerce_email_response(raw, options_language=request.language, options_tone=request.tone)
    logger.info("EmailComposer — reply succeeded candidature_id=%s", request.candidature_id)
    return result
