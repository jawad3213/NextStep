# ============================================================
# app/domain/cv_engine/agents/profile_loader.py
# Node 1 — Profile Loader (accès direct PostgreSQL)
#
# Charge le profil complet du candidat depuis la base de données.
# Entrée  : user_id
# Sortie  : raw_profile (dict profil complet)
# LLM     : aucun — requête SQL directe
# ============================================================
import logging
from langchain_core.messages import AIMessage
from app.domain.cv_engine.schemas.state import CvEngineState
from app.domain.cv_engine.tools.db_tools import load_full_profile

logger = logging.getLogger(__name__)


async def profile_loader_node(state: CvEngineState) -> dict:
    """
    Nœud LangGraph — Node 1 : Chargement du profil candidat.

    ┌──────────────────────────────────────────────────────────┐
    │  Entrées state  │  user_id                              │
    │  Sorties state  │  raw_profile, messages                 │
    │  Méthode        │  SQL direct via @tool                  │
    │  Fallback       │  dict vide avec user_id si non trouvé  │
    └──────────────────────────────────────────────────────────┘
    """
    logger.info("📋 CvEngine Node 1 [Profile Loader] — Démarrage")
    user_id = state.get("user_id", "")

    if not user_id:
        logger.warning("Node 1 — user_id manquant")
        return {
            "raw_profile": {
                "user_id": "", "competences": [], "experiences": [],
                "formations": [], "certifications": [], "projets": [],
            },
            "errors": ["CvEngine:ProfileLoader — user_id manquant dans le state"],
        }

    try:
        profile: dict = await load_full_profile.ainvoke({"user_id": user_id})

        # Analyse pour le log
        nb_comp = len(profile.get("competences", []))
        nb_exp  = len(profile.get("experiences", []))
        nb_form = len(profile.get("formations", []))
        nb_proj = len(profile.get("projets", []))
        nb_cert = len(profile.get("certifications", []))
        has_resume = "OUI" if profile.get("resume") and len(str(profile["resume"])) > 10 else "NON"

        summary = (
            f"[CvEngine:ProfileLoader] Profil chargé pour {profile.get('prenom', '')} "
            f"{profile.get('nom', '')} — "
            f"Résumé ({has_resume}), "
            f"{nb_comp} compétences, {nb_exp} expériences, "
            f"{nb_form} formations, {nb_proj} projets, {nb_cert} certifications."
        )
        logger.info("Node 1 ✅ — %s", summary)

        return {
            "raw_profile": profile,
            "messages": [AIMessage(content=summary, name="profile_loader")],
        }

    except Exception as e:
        logger.error("Node 1 ❌ — Erreur : %s", str(e))
        return {
            "raw_profile": {
                "user_id": user_id, "competences": [], "experiences": [],
                "formations": [], "certifications": [], "projets": [],
            },
            "errors": [f"CvEngine:ProfileLoader — {str(e)}"],
            "messages": [AIMessage(
                content=f"[CvEngine:ProfileLoader] Erreur : {str(e)}",
                name="profile_loader",
            )],
        }
