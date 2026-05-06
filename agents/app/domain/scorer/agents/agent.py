# ============================================================
# app/domain/offer/agents/scorer/agent.py
# Agent 4 — Scorer (Calcul mathématique, SANS LLM)
#
# Score ATS  : pondération positionnelle (titre/résumé/compétences/expériences)
# Score Match: Jaccard pondéré 70% compétences requises + 30% souhaitées
# ============================================================
import re
import logging
from langchain_core.messages import AIMessage
from app.domain.scorer.schemas.state import ScorerState
from app.core.utils.normalizer import normalize_skills, normalize_text
from app.domain.scorer.agents.scoring import (
    ats_score,
    matching_score,
    recommendations,
)

logger = logging.getLogger(__name__)


async def scorer_node(state: ScorerState) -> dict:
    """
    Nœud LangGraph — Agent 4 : Calcul des scores matching + ATS.

    ┌────────────────────────────────────────────────────────────┐
    │  Entrées state  │  normalized_offer_skills                │
    │                 │  normalized_profile_skills              │
    │                 │  normalized_keywords                    │
    │                 │  profile_full_text                      │
    │                 │  analyzed_offer (compétences souhaitées)│
    │                 │  profile_data (titre, résumé)           │
    │  Sorties state  │  match_result, messages                 │
    │  LLM            │  aucun — calcul mathématique pur        │
    └────────────────────────────────────────────────────────────┘
    """
    logger.info("📈 Agent 4 [Scorer] — Démarrage")

    offer_skills   = state.get("normalized_offer_skills")   or []
    profile_skills = state.get("normalized_profile_skills") or []
    keywords       = state.get("normalized_keywords")       or []
    full_text      = state.get("profile_full_text")         or ""

    offer   = state.get("analyzed_offer")  or {}
    profile = state.get("profile_data")    or {}

    # Normaliser les compétences souhaitées (pas encore normalisées)
    offer_optional = normalize_skills(offer.get("competences_souhaitees", []))

    # Extraire les zones de texte spécifiques pour la pondération positionnelle ATS
    profile_titre       = normalize_text(profile.get("titre") or "")
    profile_resume      = normalize_text(profile.get("resume") or "")
    profile_skills_text = " ".join(profile_skills)

    # ── Calculs ────────────────────────────────────────────────
    score_ats, kw_presents, kw_manquants = ats_score(
        keywords, profile_titre, profile_resume, profile_skills_text, full_text
    )
    score_matching, comp_matching, comp_manquantes = matching_score(
        offer_skills, offer_optional, profile_skills, full_text
    )
    recs = recommendations(kw_manquants, comp_manquantes, score_ats, score_matching)

    result = {
        "score_matching":         score_matching,
        "score_ats":              score_ats,
        "keywords_presents":      kw_presents,
        "keywords_manquants":     kw_manquants,
        "recommandations":        recs,
        "competences_matching":   comp_matching,
        "competences_manquantes": comp_manquantes,
    }

    logger.info(
        "Agent 4 ✅ — Score matching=%d%% | Score ATS=%d%% | %d/%d keywords",
        score_matching, score_ats,
        len(kw_presents), len(kw_presents) + len(kw_manquants),
    )

    summary = (
        f"[Agent 4] Scores : Matching={score_matching}% | ATS={score_ats}% "
        f"({len(kw_presents)}/{len(kw_presents)+len(kw_manquants)} keywords)"
    )
    return {
        "match_result": result,
        "messages": [AIMessage(content=summary, name="scorer")],
    }
