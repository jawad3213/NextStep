import logging
from typing import Dict, Any, List, Optional

from app.domain.cv_engine.agents.formatter import build_questpdf_payload

logger = logging.getLogger(__name__)

class CVEngineService:
    async def format_for_questpdf(
        self,
        original_profile: Dict[str, Any],
        optimized_cv: Dict[str, Any],
        matched_skills: Optional[List[str]] = None,
        offer_skills: Optional[List[str]] = None,
    ) -> dict:
        logger.info("CVEngineService: format_for_questpdf appele.")
        try:
            payload_model = build_questpdf_payload(
                original_profile,
                optimized_cv,
                matched_skills=matched_skills,
                offer_skills=offer_skills,
            )
            payload_dict = payload_model.model_dump(by_alias=True)
            logger.info("Payload QuestPDF genere avec succes.")
            return payload_dict
        except Exception as e:
            logger.error("Erreur lors de la generation du payload QuestPDF: %s", str(e), exc_info=True)
            raise ValueError(f"Erreur de formatage CV Engine: {str(e)}")

cv_engine_service = CVEngineService()
