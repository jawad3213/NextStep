# ============================================================
# app/domain/profile_retriever/agents/agent.py
# Agent 2 — Profile Retriever (accès direct PostgreSQL)
#
# Pattern : Tool call @tool → requête SQL → dict profil
# Entrée  : user_id + analyzed_offer (pour contexte)
# Sortie  : profile_data (dict profil complet)
#           + normalized_profile_skills + profile_full_text
# ============================================================
import logging
from langchain_core.messages import AIMessage
from app.core.utils.normalizer import normalize_skills, build_profile_full_text
from app.domain.profile_retriever.schemas.state import ProfileRetrieverState
from app.domain.profile_retriever.tools.db_tools import get_user_profile_from_db

logger = logging.getLogger(__name__)


async def profile_retriever_node(state: ProfileRetrieverState) -> dict:
    """
    Nœud LangGraph — Agent 2 : Chargement + normalisation du profil.

    ┌──────────────────────────────────────────────────────────────┐
    │  Entrées state  │  user_id, analyzed_offer (contexte)       │
    │  Sorties state  │  profile_data, messages                   │
    │                 │  normalized_profile_skills                │
    │                 │  profile_full_text                        │
    │  Méthode        │  SQL direct via tool @tool                │
    │  Fallback       │  dict vide avec user_id si non trouvé     │
    └──────────────────────────────────────────────────────────────┘
    """
    logger.info("Agent 2 [Profile Retriever] -- START")
    user_id = state.get("user_id", "")

    if not user_id:
        logger.warning("Agent 2 — user_id manquant")
        return {
            "profile_data":             {"user_id": "", "competences": [], "experiences": []},
            "normalized_profile_skills": [],
            "profile_full_text":         "",
            "errors": ["Agent2: user_id manquant dans le state"],
        }

    if not state.get("analyzed_offer"):
        logger.warning("Agent 2 — analyzed_offer absent (Agent 1 a peut-être échoué)")

    try:
        profile: dict = await get_user_profile_from_db.ainvoke({"user_id": user_id})

        # ── Normalisation intégrée ──────────────────────────────
        profile_skills = normalize_skills([
            c["nom"] for c in profile.get("competences", [])
            if isinstance(c, dict) and c.get("nom")
        ])
        full_text = build_profile_full_text(profile)

        # Analyse granulaire pour le log
        comps_list  = profile.get("competences", [])
        nb_comp     = len(comps_list)
        types_sample = list(set([str(c.get('type_competence', '')) for c in comps_list]))
        nb_lang     = len([c for c in comps_list if 'lang' in str(c.get('type_competence', '')).lower()])
        all_exps    = profile.get("experiences", [])
        nb_work     = len([e for e in all_exps if e.get('type') != 'Extracurricular'])
        nb_extra    = len([e for e in all_exps if e.get('type') == 'Extracurricular'])
        nb_proj     = len(profile.get("projets", []))
        nb_cert     = len(profile.get("certifications", []))
        has_resume  = "OUI" if profile.get("resume") and len(profile["resume"]) > 10 else "NON"

        summary = (
            f"[Agent 2] Profil chargé + normalisé. Types: {types_sample}. "
            f"Résumé ({has_resume}), "
            f"{nb_comp} compétences (dont {nb_lang} langues) → {len(profile_skills)} normalisées, "
            f"{nb_work} expériences pro, {nb_extra} activités, "
            f"{nb_proj} projets, {nb_cert} certifications."
        )
        return {
            "profile_data":              profile,
            "normalized_profile_skills": profile_skills,
            "profile_full_text":         full_text,
            "messages": [AIMessage(content=summary, name="profile_retriever")],
        }

    except Exception as e:
        logger.error("Agent 2 ERROR -- Erreur récupération profil : %s", str(e))
        return {
            "profile_data":              {"user_id": user_id, "competences": [], "experiences": []},
            "normalized_profile_skills": [],
            "profile_full_text":         "",
            "errors": [f"Agent2: {str(e)}"],
            "messages": [AIMessage(content=f"[Agent 2] Erreur : {str(e)}", name="profile_retriever")],
        }
