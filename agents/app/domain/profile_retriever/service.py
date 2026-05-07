# ============================================================
# app/domain/profile_retriever/service.py
# ============================================================
import logging
from app.domain.profile_retriever.graph.workflow import build_profile_retriever_workflow
from app.domain.profile_retriever.schemas.state import ProfileRetrieverState

logger = logging.getLogger(__name__)

class ProfileRetrieverService:
    """Service pour la récupération du profil candidat."""

    async def get_profile(self, user_id: str) -> dict:
        """Récupère le profil complet depuis la DB."""
        workflow = build_profile_retriever_workflow()
        initial_state: ProfileRetrieverState = {
            "user_id": user_id,
            "messages": [],
            "errors": []
        }
        final_state = await workflow.ainvoke(initial_state)
        return {
            "profile_data":              final_state.get("profile_data"),
            "normalized_profile_skills": final_state.get("normalized_profile_skills"),
            "profile_full_text":         final_state.get("profile_full_text"),
            "errors":                    final_state.get("errors")
        }

profile_retriever_service = ProfileRetrieverService()
