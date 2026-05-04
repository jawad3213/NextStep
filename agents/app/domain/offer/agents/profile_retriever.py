# ============================================================
# app/domain/offer/agents/profile_retriever.py
# Agent 2 — Profile Retriever (accès direct PostgreSQL)
#
# Pattern : Tool call @tool → requête SQL → dict profil
# Entrée  : user_id + analyzed_offer (pour contexte)
# Sortie  : profile_data (dict profil complet)
# ============================================================
import logging
from langchain_core.messages import AIMessage
from app.domain.offer.schemas.state import OfferState
from app.domain.offer.tools.db_tools import get_user_profile_from_db

logger = logging.getLogger(__name__)


async def profile_retriever_node(state: OfferState) -> dict:
    """
    Nœud LangGraph — Agent 2 : Chargement du profil candidat.

    ┌─────────────────────────────────────────────────────────┐
    │  Entrées state  │  user_id, analyzed_offer (contexte)  │
    │  Sorties state  │  profile_data, messages               │
    │  Méthode        │  SQL direct via tool @tool            │
    │  Fallback       │  dict vide avec user_id si non trouvé │
    └─────────────────────────────────────────────────────────┘
    """
    logger.info("📊 Agent 2 [Profile Retriever] — Démarrage")
    user_id = state.get("user_id", "")

    if not user_id:
        logger.warning("Agent 2 — user_id manquant")
        return {
            "profile_data": {"user_id": "", "competences": [], "experiences": []},
            "errors": ["Agent2: user_id manquant dans le state"],
        }

    if not state.get("analyzed_offer"):
        logger.warning("Agent 2 — analyzed_offer absent (Agent 1 a peut-être échoué)")

    try:
        profile: dict = await get_user_profile_from_db.ainvoke({"user_id": user_id})

        # Analyse granulaire pour le log
        comps_list = profile.get("competences", [])
        nb_comp = len(comps_list)
        
        # DEBUG : on prend les 5 premiers types pour voir
        types_sample = list(set([str(c.get('type_competence','')) for c in comps_list]))
        
        nb_lang = len([c for c in comps_list if 'lang' in str(c.get('type_competence','')).lower()])
        
        all_exps = profile.get("experiences", [])
        nb_work = len([e for e in all_exps if e.get('type') != 'Extracurricular'])
        nb_extra = len([e for e in all_exps if e.get('type') == 'Extracurricular'])
        
        nb_proj = len(profile.get("projets", []))
        nb_cert = len(profile.get("certifications", []))
        nb_form = len(profile.get("formations", []))
        
        has_resume = "OUI" if profile.get("resume") and len(profile["resume"]) > 10 else "NON"

        summary = (
            f"[Agent 2] Profil chargé. Types trouvés: {types_sample}. "
            f"Sections : Résumé ({has_resume}), "
            f"{nb_comp} compétences (dont {nb_lang} langues), "
            f"{nb_work} expériences pro, {nb_extra} activités parascolaires, "
            f"{nb_proj} projets, {nb_cert} certifications."
        )
        return {
            "profile_data": profile,
            "messages": [AIMessage(content=summary, name="profile_retriever")],
        }

    except Exception as e:
        logger.error("Agent 2 ❌ — Erreur récupération profil : %s", str(e))
        return {
            "profile_data": {"user_id": user_id, "competences": [], "experiences": []},
            "errors": [f"Agent2: {str(e)}"],
            "messages": [AIMessage(content=f"[Agent 2] Erreur : {str(e)}", name="profile_retriever")],
        }
