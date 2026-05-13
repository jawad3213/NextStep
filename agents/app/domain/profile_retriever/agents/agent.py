# ============================================================
# app/domain/profile_retriever/agents/agent.py
# Agent 2 — Profile Retriever (accès direct PostgreSQL)
#
# Mission : Récupérer et structurer les données du profil 
#          via un schéma Pydantic strict.
# ============================================================
import logging
from langchain_core.messages import AIMessage
from app.domain.profile_retriever.schemas.state import ProfileRetrieverState
from app.domain.profile_retriever.schemas.models import UserProfile
from app.domain.profile_retriever.tools.db_tools import get_user_profile_from_db, get_user_profile_from_api

logger = logging.getLogger(__name__)

async def profile_retriever_node(state: ProfileRetrieverState) -> dict:
    """
    Nœud LangGraph — Agent 2 : Récupération et Structuration du profil.
    
    Supprime toute logique de matching/scoring pour se concentrer
    uniquement sur la fourniture d'un JSON structuré.
    """
    logger.info("Agent 2 [Profile Retriever] -- START")
    user_id = state.get("user_id", "")
    
    if not user_id:
        return {
            "errors": ["Agent2: user_id manquant"],
            "messages": [AIMessage(content="Erreur: ID utilisateur manquant.", name="profile_retriever")]
        }

    try:
        # 1. Tentative de récupération via l'outil DB (PostgreSQL direct)
        logger.info("Agent 2 -- Tentative DB (PostgreSQL)")
        raw_profile: dict = await get_user_profile_from_db.ainvoke({"user_id": user_id})
        
        # Vérification si la DB a échoué (clé 'error' présente ou retour incomplet)
        if not raw_profile or "error" in raw_profile:
            db_error = raw_profile.get("error", "Profil non trouvé")
            logger.warning(f"Agent 2 -- Échec DB ({db_error}). Tentative de FALLBACK via API .NET")
            
            # 2. FALLBACK via l'outil API .NET
            raw_profile = await get_user_profile_from_api.ainvoke({"user_id": user_id})
            
            if not raw_profile or "error" in raw_profile:
                api_error = raw_profile.get("error", "Profil non trouvé via API")
                raise ValueError(f"Échec total (DB & API) pour l'ID {user_id}. Erreur API: {api_error}")
            
            source = "API .NET"
        else:
            source = "PostgreSQL"

        # 3. Validation et Structuration via Pydantic
        profile_model = UserProfile(**raw_profile)
        structured_data = profile_model.model_dump()

        summary = f"[Agent 2] Profil récupéré via {source} (ID: {user_id})."
        logger.info(summary)
        
        return {
            "profile_data": structured_data,
            "messages": [AIMessage(content=summary, name="profile_retriever")],
        }

    except Exception as e:
        logger.error("Agent 2 ERROR : %s", str(e))
        return {
            "errors": [f"Agent2: {str(e)}"],
            "messages": [AIMessage(content=f"Erreur lors de la récupération : {str(e)}", name="profile_retriever")],
        }
