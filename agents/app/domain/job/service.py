# ============================================================
# app/domain/job/service.py
# Couche service du domaine JOB
# ============================================================
import logging
from app.domain.job.schemas.state import JobState
from app.domain.job.agents.cv_formatter import cv_formatter_node
from app.domain.job.agents.email_composer import email_composer_node

logger = logging.getLogger(__name__)


class JobService:
    """
    Service du domaine JOB.

    Responsabilités :
      - Préparer les données CV pour QuestPDF (Agent 5)
      - Générer l'email de candidature / relance (Agent 6)
    """

    async def prepare_cv_data(
        self,
        user_id: str,
        offer_data: dict,
        profile_data: dict,
        match_result: dict,
        template_id: int = 1,
    ) -> dict:
        """
        Lance Agent 5 pour préparer le JSON CV.

        Returns:
            cv_template_json (dict) prêt pour QuestPDF
        """
        logger.info("JobService.prepare_cv_data — user_id=%s | template=%d", user_id, template_id)

        state: JobState = {
            "user_id":       user_id,
            "offer_data":    offer_data,
            "profile_data":  profile_data,
            "match_result":  match_result,
            "template_id":   template_id,
            "messages": [], "errors": [],
        }
        result = await cv_formatter_node(state)  # type: ignore[arg-type]
        return result.get("cv_template_json") or {}

    async def generate_email(
        self,
        user_id: str,
        candidature_id: str,
        offer_data: dict,
        profile_data: dict,
        email_type: str = "candidature",
    ) -> dict:
        """
        Lance Agent 6 pour générer l'email.

        Returns:
            email_draft (dict) avec objet + corps
        """
        logger.info(
            "JobService.generate_email — user_id=%s | type=%s",
            user_id, email_type,
        )
        state: JobState = {
            "user_id":        user_id,
            "candidature_id": candidature_id,
            "offer_data":     offer_data,
            "profile_data":   profile_data,
            "email_type":     email_type,
            "messages": [], "errors": [],
        }
        result = await email_composer_node(state)  # type: ignore[arg-type]
        return result.get("email_draft") or {}


# ── Singleton ─────────────────────────────────────────────────
job_service = JobService()
