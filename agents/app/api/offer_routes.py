import logging
from difflib import SequenceMatcher
import re
from fastapi import APIRouter, HTTPException
from pydantic import BaseModel, Field
from typing import Optional, Dict, Any

from app.domain.offer_analyzer.service import offer_analyzer_service
from app.domain.profile_retriever.service import profile_retriever_service
from app.core.utils.normalizer.text_utils import normalize_skills, build_profile_full_text, normalize_text

logger = logging.getLogger(__name__)
router = APIRouter(tags=["M2 - Offer Pipeline"])


class OfferInput(BaseModel):
    raw_text: str = Field(..., description="Le texte brut de l'offre d'emploi")
    user_id: str = Field(..., description="ID de l'utilisateur (UUID) pour recuperer son profil")
    template_id: int = Field(1, description="ID du template CV choisi")
    offer_id: str = Field(..., description="ID de l'offre d'emploi (UUID) pour sauvegarde DB")
    only_analysis: bool = Field(False, description="Si True, s'arrete apres l'analyse (Skill Gap)")

    analyzed_offer: Optional[Dict[str, Any]] = None
    profile_data: Optional[Dict[str, Any]] = None
    skill_gap_analysis: Optional[Dict[str, Any]] = None
    match_result: Optional[Dict[str, Any]] = None
    company_intelligence: Optional[Dict[str, Any]] = None
    cv_optimized_content: Optional[Dict[str, Any]] = None


class MatchRequest(BaseModel):
    user_id: str
    analyzed_offer: Dict[str, Any]
    profile_data: Optional[Dict[str, Any]] = None


class PipelineResult(BaseModel):
    analyzed_offer: Optional[Dict[str, Any]] = None
    profile_data: Optional[Dict[str, Any]] = None
    skill_gap_analysis: Optional[Dict[str, Any]] = None
    match_result: Optional[Dict[str, Any]] = None
    skill_gap: Optional[Dict[str, Any]] = None
    company_intelligence: Optional[Dict[str, Any]] = None
    cv_data: Optional[Dict[str, Any]] = None
    errors: list = []


from app.domain.pipeline.workflow import get_offer_pipeline


def _similarity(a: str, b: str) -> float:
    if not a or not b:
        return 0.0
    if a == b:
        return 1.0
    if min(len(a), len(b)) >= 3 and (a in b or b in a):
        return 0.88
    return SequenceMatcher(None, a, b).ratio()


def _canon(s: str) -> str:
    x = (s or "").strip().lower()
    x = re.sub(r"[^\w\s./+#-]", " ", x)
    x = re.sub(r"\s+", " ", x).strip()
    aliases = {
        "chatbots": "chatbot",
        "retrieval augmented generation": "rag",
        "nodejs": "node.js",
        "expressjs": "express.js",
        "nextjs": "next.js",
        "postgres": "postgresql",
        "gh actions": "github actions",
        "github action": "github actions",
        "gitlab ci": "ci/cd",
        "github actions": "ci/cd",
        "ci cd": "ci/cd",
        "cicd": "ci/cd",
        "ai": "ia",
        "artificial intelligence": "ia",
        "intelligence artificielle": "ia",
        "integration ia": "ia",
        "integrtaion ai": "ia",
        "integration ai": "ia",
    }
    return aliases.get(x, x)


def _tokenize_text(text: str) -> set[str]:
    base = _canon(text)
    rough = re.split(r"[\s,;:(){}\[\]<>|]+", base)
    out: set[str] = set()
    for t in rough:
        t = (t or "").strip(" .-_")
        if not t or len(t) == 1:
            continue
        out.add(_canon(t))
    return out


def _collapse_ci_cd(items: list[str]) -> list[str]:
    vals = [_canon(i) for i in items if i]
    has_ci = "ci" in vals
    has_cd = "cd" in vals
    vals = [v for v in vals if v not in {"ci", "cd"}]
    if has_ci and has_cd:
        vals.append("ci/cd")
    return list(dict.fromkeys(vals))


def _word_count(value: str) -> int:
    return len(re.findall(r"\w+", value or ""))


def _is_noisy_skill_phrase(value: str) -> bool:
    c = _canon(value)
    if not c:
        return True
    # Keep concise technical names, drop long sentence-like requirements.
    if _word_count(c) >= 5:
        return True
    return False


def _build_matching_targets(required: list[str], preferred: list[str], keywords: list[str]) -> list[str]:
    required = [str(s) for s in (required or []) if str(s).strip()]
    preferred = [str(s) for s in (preferred or []) if str(s).strip()]
    keywords = [str(s) for s in (keywords or []) if str(s).strip()]

    clean_required = [s for s in required if not _is_noisy_skill_phrase(s)]
    clean_preferred = [s for s in preferred if not _is_noisy_skill_phrase(s)]

    normalized_keywords = _collapse_ci_cd([_canon(s) for s in normalize_skills(keywords)])
    normalized_clean = _collapse_ci_cd([_canon(s) for s in normalize_skills([*clean_required, *clean_preferred])])

    # If Agent 1 returned mostly sentence-like items, fallback to ATS keywords.
    raw_count = len(required) + len(preferred)
    clean_count = len(clean_required) + len(clean_preferred)
    mostly_noisy = raw_count > 0 and clean_count <= max(2, raw_count // 2)
    if mostly_noisy and normalized_keywords:
        return normalized_keywords
    return normalized_clean if normalized_clean else normalized_keywords


def _build_rich_profile_text(profile_data: dict) -> str:
    chunks: list[str] = []
    if not isinstance(profile_data, dict):
        return ""

    # personal info / summary
    chunks.append(str(profile_data.get("resume") or profile_data.get("resume_professionnel") or ""))
    pi = profile_data.get("personalInfo") or {}
    if isinstance(pi, dict):
        chunks.append(str(pi.get("resumeProfessionnel") or ""))
        chunks.append(str(pi.get("titrePoste") or ""))

    # skills
    for c in profile_data.get("competences", []) or []:
        if isinstance(c, dict):
            chunks.append(str(c.get("nom") or c.get("name") or ""))
        elif isinstance(c, str):
            chunks.append(c)

    # experiences
    for e in profile_data.get("experiences", []) or []:
        if not isinstance(e, dict):
            continue
        chunks.append(str(e.get("titre") or e.get("poste") or ""))
        chunks.append(str(e.get("description") or e.get("missions") or ""))
        chunks.append(str(e.get("entreprise") or ""))
        for t in e.get("taches", []) or []:
            chunks.append(str(t))

    # projects
    for p in profile_data.get("projets", []) or profile_data.get("projects", []) or []:
        if not isinstance(p, dict):
            continue
        chunks.append(str(p.get("titre") or p.get("titreProjet") or ""))
        chunks.append(str(p.get("description") or ""))
        techs = p.get("technologies") or p.get("technologiesUtilisees") or []
        if isinstance(techs, str):
            chunks.extend([t.strip() for t in techs.split(",") if t.strip()])
        elif isinstance(techs, list):
            chunks.extend([str(t) for t in techs if t])
        for t in p.get("taches", []) or []:
            chunks.append(str(t))

    return " ".join(chunks)


def _compute_deterministic_match(profile_data: dict, analyzed_offer: dict) -> dict:
    offer_keywords = analyzed_offer.get("keywords_ats", []) or []
    required = analyzed_offer.get("competences_requises", []) or []
    preferred = analyzed_offer.get("competences_souhaitees", []) or []
    normalized_targets = _build_matching_targets(required, preferred, offer_keywords)
    normalized_keywords = _collapse_ci_cd([_canon(s) for s in normalize_skills([str(t) for t in offer_keywords if t])])

    profile_skills = []
    for c in profile_data.get("competences", []) or []:
        if isinstance(c, dict) and c.get("nom"):
            profile_skills.append(c.get("nom"))
    for s in profile_data.get("skills", []) or []:
        if isinstance(s, str):
            profile_skills.append(s)
        elif isinstance(s, dict):
            profile_skills.append(s.get("nom") or s.get("name") or "")

    normalized_profile_skills = _collapse_ci_cd([_canon(s) for s in normalize_skills([str(s) for s in profile_skills if s])])
    profile_text = f"{build_profile_full_text(profile_data)} {_build_rich_profile_text(profile_data)}"
    text_norm = normalize_text(profile_text)
    profile_tokens = set(normalized_profile_skills)
    profile_tokens.update(_tokenize_text(text_norm))
    if ("continuous integration" in text_norm) or ("continuous delivery" in text_norm) or ("github actions" in text_norm):
        profile_tokens.add("ci/cd")
    if (" ai " in f" {text_norm} ") or (" artificial intelligence " in f" {text_norm} ") or (" intelligence artificielle " in f" {text_norm} ") or (" integration ia " in f" {text_norm} ") or (" integration ai " in f" {text_norm} "):
        profile_tokens.add("ia")
    if "n8n" in text_norm:
        profile_tokens.add("n8n")
    if "chatbot" in text_norm or "chatbots" in text_norm:
        profile_tokens.add("chatbot")
    if "retrieval augmented generation" in text_norm or " rag " in f" {text_norm} ":
        profile_tokens.add("rag")

    matched, partial, missing = [], [], []
    for t in normalized_targets:
        best = 0.0
        for p in profile_tokens:
            sim = _similarity(t, p)
            if sim > best:
                best = sim
        if best >= 0.80:
            matched.append(t)
        elif best >= 0.52:
            partial.append(t)
        else:
            missing.append(t)

    score_matching = int(round(((len(matched) + 0.5 * len(partial)) / max(1, len(normalized_targets))) * 100))
    score_matching = max(0, min(100, score_matching))

    kw_present, kw_missing = [], []
    for kw in normalized_keywords:
        best = 0.0
        for p in profile_tokens:
            sim = _similarity(kw, p)
            if sim > best:
                best = sim
        if best >= 0.80:
            kw_present.append(kw)
        else:
            kw_missing.append(kw)

    score_ats = int(round((len(kw_present) / max(1, len(normalized_keywords))) * 100))
    score_ats = max(0, min(100, score_ats))

    return {
        "matched_skills": matched,
        "partial_skills": partial,
        "missing_skills": missing,
        "keywords_presents": kw_present,
        "keywords_manquants": kw_missing,
        "score_matching": score_matching,
        "score_ats": score_ats,
    }


def _display_label(canonical: str, analyzed_offer: dict) -> str:
    labels = [
        *(analyzed_offer.get("competences_requises", []) or []),
        *(analyzed_offer.get("competences_souhaitees", []) or []),
        *(analyzed_offer.get("keywords_ats", []) or []),
    ]
    for label in labels:
        if _canon(str(label)) == canonical:
            return str(label)
    special = {
        "ci/cd": "CI/CD",
        "ia": "IA",
        "rag": "RAG",
        "node.js": "Node.js",
        "express.js": "Express.js",
        "next.js": "Next.js",
        "postgresql": "PostgreSQL",
    }
    return special.get(canonical, canonical)


def _build_deterministic_result(profile_data: dict, analyzed_offer: dict) -> dict:
    deterministic = _compute_deterministic_match(profile_data or {}, analyzed_offer or {})
    matched = _collapse_ci_cd(deterministic["matched_skills"])
    partial = [s for s in _collapse_ci_cd(deterministic["partial_skills"]) if s not in set(matched)]
    missing = [
        s for s in _collapse_ci_cd(deterministic["missing_skills"])
        if s not in set(matched) and s not in set(partial)
    ]

    matched_labels = [_display_label(s, analyzed_offer) for s in matched]
    missing_labels = [_display_label(s, analyzed_offer) for s in missing]
    keyword_present = [_display_label(s, analyzed_offer) for s in _collapse_ci_cd(deterministic["keywords_presents"])]
    keyword_missing = [_display_label(s, analyzed_offer) for s in _collapse_ci_cd(deterministic["keywords_manquants"])]

    return {
        "candidate_name": "Candidat",
        "job_title": analyzed_offer.get("titre") or analyzed_offer.get("job_title") or "Poste",
        "relevance_score": round(deterministic["score_matching"] / 100, 2),
        "score_matching": deterministic["score_matching"],
        "score_ats": deterministic["score_ats"],
        "matched_skills": matched_labels,
        "missing_skills": missing_labels,
        "partial_skills": [_display_label(s, analyzed_offer) for s in partial],
        "competences_matching": matched_labels,
        "competences_manquantes": missing_labels,
        "keywords_presents": keyword_present,
        "keywords_manquants": keyword_missing,
        "required_certs": [],
        "cert_match": False,
        "experience_years": 0.0,
        "required_years": 0.0,
        "experience_gap_years": 0.0,
        "flag": "minor_gap" if deterministic["score_matching"] >= 60 else "critical_gap",
        "recommandations": [
            f"Ajouter une preuve concrete de '{skill}' dans une experience ou un projet."
            for skill in missing_labels[:5]
        ],
        "revision_hints": [],
        "analysis_source": "deterministic_skill_gap_v2",
        "profile_data": profile_data or {},
        "errors": [],
    }


@router.post("/run-pipeline", response_model=PipelineResult)
async def run_pipeline(payload: OfferInput) -> PipelineResult:
    logger.info("POST /run-pipeline - user_id=%s", payload.user_id)
    try:
        pipeline = get_offer_pipeline()
        initial_state = {
            "raw_offer_text": payload.raw_text,
            "user_id": payload.user_id,
            "template_id": payload.template_id,
            "offer_id": payload.offer_id,
            "messages": [],
            "errors": [],
            "normalized_offer_skills": [],
            "normalized_keywords": [],
            "normalized_profile_skills": [],
            "only_analysis": payload.only_analysis,
            "analyzed_offer": payload.analyzed_offer,
            "profile_data": payload.profile_data,
            "skill_gap_analysis": payload.skill_gap_analysis or payload.match_result,
            "match_result": payload.match_result,
            "company_intelligence": payload.company_intelligence,
            "cv_optimized_content": payload.cv_optimized_content,
        }

        final_state = await pipeline.ainvoke(initial_state)
        skill_gap_analysis = final_state.get("skill_gap_analysis") or final_state.get("match_result")

        return PipelineResult(
            analyzed_offer=final_state.get("analyzed_offer"),
            profile_data=final_state.get("profile_data"),
            skill_gap_analysis=skill_gap_analysis,
            match_result=final_state.get("match_result") or skill_gap_analysis,
            skill_gap=skill_gap_analysis,
            company_intelligence=final_state.get("company_intelligence"),
            cv_data=final_state.get("cv_engine_result"),
            errors=final_state.get("errors", []),
        )
    except Exception as e:
        logger.error("POST /run-pipeline failed: %s", str(e))
        raise HTTPException(status_code=500, detail=f"Erreur pipeline : {str(e)}")


@router.post("/analyze-offer", response_model=dict)
async def analyze_offer(payload: OfferInput) -> dict:
    logger.info("POST /analyze-offer - user_id=%s", payload.user_id)
    try:
        result = await offer_analyzer_service.analyze(payload.raw_text)
        if not result or not result.get("analyzed_offer"):
            raise HTTPException(status_code=502, detail="Erreur LLM - analyse echouee")
        return result
    except HTTPException:
        raise
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))


@router.post("/match", response_model=dict)
async def match_profile(payload: MatchRequest) -> dict:
    logger.info("POST /match - user_id=%s", payload.user_id)
    try:
        if payload.profile_data:
            profile_data = payload.profile_data
        else:
            profile_res = await profile_retriever_service.get_profile(str(payload.user_id))
            profile_data = profile_res.get("profile_data", {})

        return _build_deterministic_result(profile_data or {}, payload.analyzed_offer or {})
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))
