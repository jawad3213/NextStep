# ============================================================
# app/agents/profile_retriever.py
# Agent 2 — Profile Retriever (RAG + DB)
#
# Pattern : Agent utilise l'outil @tool `get_user_profile_from_db`
# pour récupérer le profil et l'injecter dans le state.
# ============================================================
import logging
from langchain_core.messages import AIMessage
from app.schemas.state import AgentState
from app.tools.backend_api import get_user_profile_from_db

logger = logging.getLogger(__name__)


async def profile_retriever_node(state: AgentState) -> dict:
    """
    Nœud LangGraph — Agent 2 : Récupération du profil utilisateur.

    Stratégie :
        Utilise l'outil get_user_profile_from_db (requête PostgreSQL directe)
        pour charger le profil complet (compétences, expériences, formations, etc.)

    Entrées depuis state :
        - user_id : ID Keycloak
        - analyzed_offer : pour le contexte (keywords_ats)

    Mise à jour de state :
        - profile_data : dict profil complet
        - messages : message AI ajouté
    """
    logger.info("📊 Agent 2 [Profile Retriever] — Démarrage")
    user_id = state.get("user_id", "")
    analyzed_offer = state.get("analyzed_offer") or {}

    if not user_id:
        logger.warning("Agent 2 — user_id manquant")
        return {
            "profile_data": {"user_id": "", "competences": [], "experiences": []},
            "errors": ["Agent2: user_id manquant"],
        }

    if not analyzed_offer:
        logger.warning("Agent 2 — analyzed_offer absent (Agent 1 a échoué ?)")

    try:
        # ─── Appel de l'outil @tool ───
        profile: dict = await get_user_profile_from_db.ainvoke({"user_id": user_id})

        nb_comp = len(profile.get("competences", []))
        nb_exp = len(profile.get("experiences", []))
        logger.info(
            "Agent 2 ✅ — Profil récupéré : %s %s | %d compétences | %d expériences",
            profile.get("prenom", "?"), profile.get("nom", "?"),
            nb_comp, nb_exp,
        )

        summary = (
            f"[Agent 2] Profil chargé : {profile.get('prenom', '')} {profile.get('nom', '')} "
            f"({nb_comp} compétences, {nb_exp} expériences)"
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
