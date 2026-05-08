# ============================================================
# app/domain/offer_analyzer/service.py
# ============================================================
import logging
from app.domain.offer_analyzer.graph.workflow import build_offer_analyzer_workflow
from app.domain.offer_analyzer.schemas.state import OfferAnalyzerState

logger = logging.getLogger(__name__)

class OfferAnalyzerService:
    """Service pour l'analyse d'offres d'emploi."""

    async def analyze(self, raw_text: str) -> dict:
        """Analyse une offre et retourne le JSON structuré + normalisé."""
        workflow = build_offer_analyzer_workflow()
        initial_state: OfferAnalyzerState = {
            "raw_offer_text": raw_text,
            "iteration_count": 0,
            "messages": [],
            "errors": [],
            "normalized_offer_skills": [],
            "normalized_keywords": []
        }
        final_state = await workflow.ainvoke(initial_state)
        return {
            "analyzed_offer": final_state.get("analyzed_offer"),
            "normalized_offer_skills": final_state.get("normalized_offer_skills"),
            "normalized_keywords": final_state.get("normalized_keywords"),
            "errors": final_state.get("errors")
        }

offer_analyzer_service = OfferAnalyzerService()
