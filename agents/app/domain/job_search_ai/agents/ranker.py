import json
import logging
import re
from typing import Iterable

from langchain_core.prompts import ChatPromptTemplate
from pydantic import BaseModel, Field

from app.core.config import get_llm
from app.core.utils.normalizer import normalize_skills, normalize_text
from app.domain.job_search_ai.schemas.models import (
    CandidateProfile,
    JobSearchOffer,
    JobSearchRankRequest,
    JobSearchRankResponse,
    RankedJobOffer,
)

logger = logging.getLogger(__name__)

TECH_TERMS = [
    "python",
    "java",
    "javascript",
    "typescript",
    "react",
    "angular",
    "vue",
    "node.js",
    "express",
    "next.js",
    "spring",
    ".net",
    "asp.net",
    "c#",
    "php",
    "laravel",
    "go",
    "golang",
    "sql",
    "postgresql",
    "mysql",
    "mongodb",
    "redis",
    "elasticsearch",
    "docker",
    "kubernetes",
    "terraform",
    "aws",
    "azure",
    "gcp",
    "linux",
    "git",
    "ci/cd",
    "jenkins",
    "gitlab ci",
    "github actions",
    "fastapi",
    "django",
    "flask",
    "machine learning",
    "deep learning",
    "data science",
    "data analysis",
    "power bi",
    "tableau",
    "spark",
    "kafka",
    "airflow",
    "dbt",
    "scrum",
    "agile",
    "qa",
    "selenium",
    "playwright",
    "figma",
]

TITLE_STOPWORDS = {
    "developer",
    "engineer",
    "specialist",
    "consultant",
    "manager",
    "junior",
    "senior",
    "lead",
    "stage",
    "intern",
    "full",
    "remote",
}


class _AiRankedOffer(BaseModel):
    offer_id: str
    score_total: int = Field(ge=0, le=100)
    confidence: float = Field(ge=0.0, le=1.0)
    reasons: list[str] = Field(default_factory=list)
    summary: str | None = None


class _AiRankingBatch(BaseModel):
    ranked_offers: list[_AiRankedOffer] = Field(default_factory=list)


def _clamp_int(value: float, minimum: int, maximum: int) -> int:
    return max(minimum, min(maximum, int(round(value))))


def _clean_items(values: Iterable[str | None]) -> list[str]:
    result: list[str] = []
    seen: set[str] = set()
    for value in values:
        clean = (value or "").strip()
        if not clean:
            continue
        key = clean.lower()
        if key not in seen:
            seen.add(key)
            result.append(clean)
    return result


def _split_tech_blob(value: str | None) -> list[str]:
    if not value:
        return []
    return [item.strip() for item in re.split(r"[,;|/]+", value) if item.strip()]


def _normalized_skill_set(values: Iterable[str | None]) -> set[str]:
    return set(normalize_skills(_clean_items(values)))


def _profile_skill_set(profile: CandidateProfile) -> set[str]:
    values: list[str | None] = []
    values.extend(profile.skills)
    values.extend(profile.target_keywords)
    values.extend(profile.project_technologies)
    for blob in profile.project_technologies:
        values.extend(_split_tech_blob(blob))
    return _normalized_skill_set(values)


def _profile_title_tokens(profile: CandidateProfile) -> set[str]:
    corpus = " ".join(
        _clean_items(
            [
                profile.title,
                profile.sector,
                *profile.experience_titles[:4],
                *profile.target_keywords,
            ]
        )
    )
    tokens = set(re.findall(r"[a-z0-9+#.]{3,}", normalize_text(corpus)))
    return {token for token in tokens if token not in TITLE_STOPWORDS}


def _offer_skill_set(offer: JobSearchOffer) -> set[str]:
    values: list[str | None] = []
    values.extend(offer.matched_it_terms)

    corpus = normalize_text(
        " ".join(
            _clean_items(
                [
                    offer.title,
                    offer.description,
                    offer.employment_type,
                    offer.seniority_level,
                    *offer.matched_it_terms,
                ]
            )
        )
    )
    for term in TECH_TERMS:
        if normalize_text(term) in corpus:
            values.append(term)
    return _normalized_skill_set(values)


def _posted_hours(offer: JobSearchOffer) -> int | None:
    if offer.posted_window:
        window = offer.posted_window.lower()
        if window == "24h":
            return 24
        if window == "3d":
            return 72
        if window == "7d":
            return 24 * 7
        if window == "14d":
            return 24 * 14
        if window == "30d":
            return 24 * 30

    text = (offer.posted_at_text or "").lower()
    if not text:
        return None
    if any(token in text for token in ["today", "just now", "aujourd", "maintenant"]):
        return 0
    match = re.search(r"(\d+)", text)
    if not match:
        return None
    value = int(match.group(1))
    if any(token in text for token in ["hour", "hr", "heure"]):
        return value
    if any(token in text for token in ["day", "jour"]):
        return value * 24
    if any(token in text for token in ["week", "semaine"]):
        return value * 24 * 7
    if any(token in text for token in ["month", "mois"]):
        return value * 24 * 30
    return None


def _score_freshness(offer: JobSearchOffer) -> int:
    hours = _posted_hours(offer)
    if hours is None:
        return 4
    if hours <= 24:
        return 10
    if hours <= 72:
        return 8
    if hours <= 24 * 7:
        return 6
    if hours <= 24 * 14:
        return 4
    if hours <= 24 * 30:
        return 2
    return 1


def _score_location(profile: CandidateProfile, offer: JobSearchOffer) -> int:
    profile_location = normalize_text(" ".join(_clean_items([profile.location, profile.country])))
    offer_location = normalize_text(offer.location or "")
    if not profile_location:
        return 6
    if any(term in offer_location for term in ["remote", "worldwide", "teletravail", "hybride", "hybrid"]):
        return 9
    profile_parts = {part for part in re.split(r"\s+", profile_location) if len(part) >= 3}
    if profile_parts and any(part in offer_location for part in profile_parts):
        return 10
    return 3


def _score_contract(profile: CandidateProfile, offer: JobSearchOffer) -> int:
    contract = (offer.normalized_contract_type or "").strip().lower()
    preferred = {item.strip().lower() for item in profile.preferred_contract_types if item}
    if preferred and contract in preferred:
        return 10
    if preferred and contract and contract not in preferred:
        return 3
    if contract and contract != "other":
        return 7
    return 4


def _score_title(profile: CandidateProfile, offer: JobSearchOffer) -> int:
    profile_tokens = _profile_title_tokens(profile)
    if not profile_tokens:
        return 8
    offer_tokens = set(re.findall(r"[a-z0-9+#.]{3,}", normalize_text(offer.title)))
    overlap = profile_tokens & offer_tokens
    if not overlap:
        return 4
    return _clamp_int((len(overlap) / max(len(profile_tokens), 1)) * 20, 6, 20)


def _deterministic_rank(profile: CandidateProfile, offer: JobSearchOffer) -> RankedJobOffer:
    candidate_skills = _profile_skill_set(profile)
    offer_skills = _offer_skill_set(offer)
    matched = sorted(candidate_skills & offer_skills)
    missing = sorted(offer_skills - candidate_skills)[:10]

    if offer_skills:
        ratio = len(matched) / len(offer_skills)
        score_skills = _clamp_int(ratio * 42 + min(len(matched), 4) * 2, 0, 50)
    else:
        score_skills = 18

    score_title = _score_title(profile, offer)
    score_location = _score_location(profile, offer)
    score_contract = _score_contract(profile, offer)
    score_freshness = _score_freshness(offer)
    total = _clamp_int(score_skills + score_title + score_location + score_contract + score_freshness, 0, 100)

    reasons: list[str] = []
    if matched:
        reasons.append(f"Match skills: {', '.join(matched[:5])}")
    if missing:
        reasons.append(f"Skills a renforcer: {', '.join(missing[:4])}")
    if score_title >= 14:
        reasons.append("Titre proche de la cible du profil.")
    if score_location >= 9:
        reasons.append("Localisation compatible avec le profil.")
    if score_freshness >= 8:
        reasons.append("Offre recente, prioritaire pour postuler vite.")
    if not reasons:
        reasons.append("Offre gardee pour analyse, mais peu de signaux forts detectes.")

    confidence = 0.55
    if offer.description:
        confidence += 0.15
    if offer.matched_it_terms:
        confidence += 0.15
    if matched:
        confidence += 0.10
    confidence = min(confidence, 0.95)

    return RankedJobOffer(
        offer_id=offer.id,
        score_total=total,
        score_skills=score_skills,
        score_title=score_title,
        score_location=score_location,
        score_contract=score_contract,
        score_freshness=score_freshness,
        confidence=round(confidence, 2),
        matched_skills=matched[:12],
        missing_skills=missing,
        reasons=reasons[:5],
        summary=_build_summary(total, matched, missing),
    )


def _build_summary(total: int, matched: list[str], missing: list[str]) -> str:
    if total >= 80:
        return "Tres forte priorite: candidature conseillee rapidement."
    if total >= 65:
        return "Bonne opportunite: verifier les details puis postuler."
    if total >= 45:
        return "Opportunite moyenne: utile si les contraintes conviennent."
    if missing:
        return "Faible alignement direct: plusieurs competences semblent manquantes."
    return "Score faible par manque de signaux exploitables."


async def _try_ai_refine(
    profile: CandidateProfile,
    offers: list[JobSearchOffer],
    ranked: list[RankedJobOffer],
    limit: int,
) -> tuple[list[RankedJobOffer], list[str]]:
    warnings: list[str] = []
    if limit <= 0 or not ranked:
        return ranked, warnings

    ranked_by_id = {item.offer_id: item for item in ranked}
    offers_by_id = {item.id: item for item in offers}
    top_ranked = sorted(ranked, key=lambda item: item.score_total, reverse=True)[:limit]
    compact_offers = []
    for item in top_ranked:
        offer = offers_by_id.get(item.offer_id)
        if not offer:
            continue
        compact_offers.append(
            {
                "offer_id": offer.id,
                "title": offer.title,
                "company": offer.company,
                "location": offer.location,
                "contract": offer.normalized_contract_type,
                "seniority": offer.seniority_level,
                "matched_terms": offer.matched_it_terms[:12],
                "description": (offer.description or "")[:1200],
                "deterministic_score": item.score_total,
                "matched_skills": item.matched_skills,
                "missing_skills": item.missing_skills[:8],
            }
        )

    if not compact_offers:
        return ranked, warnings

    prompt = ChatPromptTemplate.from_messages(
        [
            (
                "system",
                "Tu es un recruteur tech senior. Revois un classement d'offres deja filtrees. "
                "Garde des scores realistes entre 0 et 100. Ne favorise pas une offre sans preuve dans la description. "
                "Retourne uniquement la structure demandee.",
            ),
            (
                "human",
                "Profil candidat JSON:\n{profile_json}\n\nOffres candidates JSON:\n{offers_json}",
            ),
        ]
    )

    try:
        llm = get_llm(agent_name="job_search_ai", temperature=0.0).with_structured_output(_AiRankingBatch)
        chain = prompt | llm
        result: _AiRankingBatch = await chain.ainvoke(
            {
                "profile_json": json.dumps(profile.model_dump(), ensure_ascii=False),
                "offers_json": json.dumps(compact_offers, ensure_ascii=False),
            }
        )
    except Exception as exc:
        logger.info("Job search AI refinement skipped: %s", exc)
        warnings.append(f"AI refinement unavailable, deterministic ranking used: {exc}")
        return ranked, warnings

    for ai_item in result.ranked_offers:
        current = ranked_by_id.get(ai_item.offer_id)
        if not current:
            continue
        current.score_total = _clamp_int(ai_item.score_total, 0, 100)
        current.confidence = max(current.confidence, round(ai_item.confidence, 2))
        if ai_item.reasons:
            current.reasons = _clean_items(ai_item.reasons)[:5]
        if ai_item.summary:
            current.summary = ai_item.summary.strip()[:240]

    return sorted(ranked, key=lambda item: item.score_total, reverse=True), warnings


async def rank_job_search_offers(request: JobSearchRankRequest) -> JobSearchRankResponse:
    deterministic = [_deterministic_rank(request.profile, offer) for offer in request.offers]
    deterministic.sort(key=lambda item: item.score_total, reverse=True)
    refined, warnings = await _try_ai_refine(
        profile=request.profile,
        offers=request.offers,
        ranked=deterministic,
        limit=request.ai_refine_limit,
    )
    refined.sort(key=lambda item: item.score_total, reverse=True)
    return JobSearchRankResponse(ranked_offers=refined, warnings=warnings)
