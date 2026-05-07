# ============================================================
# app/domain/scorer/service.py
# ============================================================
import logging
from app.domain.scorer.graph.workflow import build_scorer_workflow
from app.domain.scorer.schemas.state import ScorerState

logger = logging.getLogger(__name__)

class ScorerService:
    """Service pour le calcul des scores (Matching & ATS)."""

    async def calculate_scores(
        self, 
        normalized_offer_skills: list[str],
        normalized_profile_skills: list[str],
        normalized_keywords: list[str],
        profile_full_text: str,
        analyzed_offer: dict,
        profile_data: dict
    ) -> dict:
        """Calcule les scores de matching et ATS."""
        workflow = build_scorer_workflow()
        initial_state: ScorerState = {
            "normalized_offer_skills": normalized_offer_skills,
            "normalized_profile_skills": normalized_profile_skills,
            "normalized_keywords": normalized_keywords,
            "profile_full_text": profile_full_text,
            "analyzed_offer": analyzed_offer,
            "profile_data": profile_data,
            "messages": [],
            "errors": []
        }
        final_state = await workflow.ainvoke(initial_state)
        return {
            "match_result": final_state.get("match_result"),
            "errors": final_state.get("errors")
        }

scorer_service = ScorerService()
