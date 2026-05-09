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
import logging
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
    structured_llm = llm.with_structured_output(GenerateEmailResponse)
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

    result: GenerateEmailResponse = await chain.ainvoke(
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
    structured_llm = llm.with_structured_output(GenerateEmailResponse)
    prompt = ChatPromptTemplate.from_messages(
        [("system", FOLLOWUP_SYSTEM), ("human", FOLLOWUP_HUMAN)]
    )
    chain = prompt | structured_llm

    c = request.candidate
    j = request.job_offer
    p = request.previous_email
    o = request.options
    days_label = str(o.days_since_sent) if o.days_since_sent is not None else "Non spécifié"

    result: GenerateEmailResponse = await chain.ainvoke(
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
    structured_llm = llm.with_structured_output(ClassifyResponseResult)
    prompt = ChatPromptTemplate.from_messages(
        [("system", CLASSIFY_SYSTEM), ("human", CLASSIFY_HUMAN)]
    )
    chain = prompt | structured_llm

    result: ClassifyResponseResult = await chain.ainvoke(
        {
            "job_title":              _safe(request.job_title),
            "company_name":           _safe(request.company_name),
            "previous_email_subject": _safe(request.previous_email_subject),
            "reply_from":             _safe(request.reply_from),
            "reply_date_utc":         _safe(request.reply_date_utc),
            "reply_subject":          _safe(request.reply_subject),
            "reply_snippet":          _truncate(request.reply_snippet, 800),
            "language":               request.language,
        }
    )
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
    structured_llm = llm.with_structured_output(GenerateEmailResponse)
    prompt = ChatPromptTemplate.from_messages(
        [("system", REPLY_SYSTEM), ("human", REPLY_HUMAN)]
    )
    chain = prompt | structured_llm

    result: GenerateEmailResponse = await chain.ainvoke(
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
    logger.info("EmailComposer — reply succeeded candidature_id=%s", request.candidature_id)
    return result
