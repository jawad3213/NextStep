# ============================================================
# app/domain/cv_engine/agents/skill_optimizer.py
# Node 2 — Skill Optimizer (Algorithme pur, SANS LLM)
#
# Trie les compétences par pertinence (matching offre en premier),
# ajoute un flag `matched`, et calcule la couverture ATS.
#
# Entrée  : raw_profile, match_result (optionnel), offer_data (optionnel)
# Sortie  : optimized_skills, ats_coverage
# LLM     : aucun — tri algorithmique
# ============================================================
import logging
from langchain_core.messages import AIMessage
from app.domain.cv_engine.schemas.state import CvEngineState

logger = logging.getLogger(__name__)


def _normalize(text: str) -> str:
    """Normalise un texte pour comparaison insensible à la casse."""
    return text.strip().lower()


async def skill_optimizer_node(state: CvEngineState) -> dict:
    """
    Nœud LangGraph — Node 2 : Optimisation des compétences.

    ┌──────────────────────────────────────────────────────────┐
    │  Entrées state  │  raw_profile, match_result, offer_data│
    │  Sorties state  │  optimized_skills, ats_coverage, msgs │
    │  Méthode        │  Tri algorithmique + couverture ATS   │
    │  Mode sans offre│  Tri par niveau décroissant seulement │
    └──────────────────────────────────────────────────────────┘
    """
    logger.info("🎯 CvEngine Node 2 [Skill Optimizer] — Démarrage")

    profile      = state.get("raw_profile")  or {}
    match_result = state.get("match_result") or {}
    offer_data   = state.get("offer_data")   or {}

    raw_comps = profile.get("competences", []) or []

    # ── Build set of matched skill names ─────────────────────
    matching_names = set()
    if match_result:
        for name in match_result.get("competences_matching", []):
            matching_names.add(_normalize(name))

    # ── Sort: matched first, then by level descending ────────
    def sort_key(c: dict) -> tuple:
        is_matched = 0 if _normalize(c.get("nom", "")) in matching_names else 1
        level = -(c.get("niveau", 0) or 0)
        return (is_matched, level)

    sorted_comps = sorted(
        [c for c in raw_comps if isinstance(c, dict) and c.get("nom")],
        key=sort_key,
    )

    optimized = [
        {
            "nom":             c.get("nom", ""),
            "niveau":          c.get("niveau", 1) or 1,
            "type_competence": c.get("type_competence", "Technical"),
            "matched":         _normalize(c.get("nom", "")) in matching_names,
        }
        for c in sorted_comps
    ]

    nb_matched = sum(1 for s in optimized if s["matched"])

    # ── ATS keyword coverage ─────────────────────────────────
    ats_keywords = []
    if offer_data:
        ats_keywords = offer_data.get("keywords_ats", []) or []
    elif match_result:
        present  = match_result.get("keywords_presents", []) or []
        missing  = match_result.get("keywords_manquants", []) or []
        ats_keywords = present + missing

    if ats_keywords:
        # Check which ATS keywords appear in the profile text
        profile_text = _build_profile_text(profile)
        present_kw = []
        missing_kw = []
        for kw in ats_keywords:
            if _normalize(kw) in profile_text:
                present_kw.append(kw)
            else:
                missing_kw.append(kw)

        total = len(ats_keywords)
        covered = len(present_kw)
        pct = round((covered / total) * 100, 1) if total > 0 else 0.0

        ats_coverage = {
            "covered": covered,
            "total": total,
            "percentage": pct,
            "keywords_present": present_kw,
            "keywords_missing": missing_kw,
        }
    else:
        ats_coverage = {
            "covered": 0, "total": 0, "percentage": 0.0,
            "keywords_present": [], "keywords_missing": [],
        }

    mode = "ciblé (offre)" if matching_names else "générique (sans offre)"
    summary = (
        f"[CvEngine:SkillOptimizer] {len(optimized)} compétences optimisées "
        f"({nb_matched} matchées) — Mode {mode} — "
        f"ATS: {ats_coverage['covered']}/{ats_coverage['total']} "
        f"({ats_coverage['percentage']}%)"
    )
    logger.info("Node 2 ✅ — %s", summary)

    return {
        "optimized_skills": optimized,
        "ats_coverage":     ats_coverage,
        "messages": [AIMessage(content=summary, name="skill_optimizer")],
    }


def _build_profile_text(profile: dict) -> str:
    """
    Construit un texte agrégé du profil pour la recherche de mots-clés ATS.
    """
    parts = [
        profile.get("titre", ""),
        profile.get("resume", ""),
    ]

    for c in profile.get("competences", []):
        if isinstance(c, dict):
            parts.append(c.get("nom", ""))

    for e in profile.get("experiences", []):
        if isinstance(e, dict):
            parts.append(e.get("titre", ""))
            parts.append(e.get("description", ""))

    for p in profile.get("projets", []):
        if isinstance(p, dict):
            parts.append(p.get("titre", ""))
            parts.append(p.get("description", ""))
            parts.append(p.get("technologies", ""))

    return _normalize(" ".join(filter(None, parts)))
