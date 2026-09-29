# ============================================================
# app/domain/profile_retriever/service.py
# Agent 2 — the candidate profile, loaded from the backend's Profile module.
# ============================================================
import logging

from app.domain.profile_retriever.schemas.models import UserProfile
from app.domain.profile_retriever.tools.profile_source import get_user_profile

logger = logging.getLogger(__name__)


class ProfileRetrieverService:
    """Loads and validates the candidate profile."""

    async def get_profile(self, user_id: str) -> dict:
        """{"profile_data": dict | None, "errors": [...]}. Read fresh on every call, so a CV
        generated right after a profile edit uses the edited profile."""
        if not user_id:
            return {"profile_data": None, "errors": ["Agent2: user_id manquant"]}

        raw_profile = await get_user_profile(user_id)
        if "error" in raw_profile:
            logger.error("Agent 2 — profile not loaded for %s: %s", user_id, raw_profile["error"])
            return {"profile_data": None, "errors": [f"Agent2: Profil introuvable pour l'ID {user_id}: {raw_profile['error']}"]}

        try:
            return {"profile_data": UserProfile(**raw_profile).model_dump(), "errors": []}
        except Exception as e:
            logger.error("Agent 2 — invalid profile for %s: %s", user_id, e)
            return {"profile_data": None, "errors": [f"Agent2: profil invalide: {e}"]}


profile_retriever_service = ProfileRetrieverService()
