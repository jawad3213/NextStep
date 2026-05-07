import logging
from typing import Dict, Any

from app.domain.cv_engine.agents.formatter import build_questpdf_payload
from app.domain.cv_engine.schemas.models import QuestPDFCvData

logger = logging.getLogger(__name__)

class CVEngineService:
    """Service d'orchestration pour le domaine cv_engine."""
    
    def __init__(self):
        pass

    async def format_for_questpdf(self, original_profile: Dict[str, Any], optimized_cv: Dict[str, Any]) -> dict:
        """
        Orchestre le mapping algorithmique entre le profil brut et le CV optimisé.
        Renvoie un dictionnaire compatible Pydantic alias (camelCase) prêt pour QuestPDF.
        """
        logger.info("CVEngineService : format_for_questpdf appelé.")
        
        try:
            # Appel du formateur algorithmique
            payload_model: QuestPDFCvData = build_questpdf_payload(original_profile, optimized_cv)
            
            # Export avec by_alias=True pour avoir camelCase (si alias_generator a fonctionné)
            # ou par défaut pour la validation.
            payload_dict = payload_model.model_dump(by_alias=True)
            
            logger.info("Payload QuestPDF généré avec succès.")
            return payload_dict
            
        except Exception as e:
            logger.error("Erreur lors de la génération du payload QuestPDF : %s", str(e), exc_info=True)
            raise ValueError(f"Erreur de formatage CV Engine : {str(e)}")

# Singleton pour le routeur
cv_engine_service = CVEngineService()
