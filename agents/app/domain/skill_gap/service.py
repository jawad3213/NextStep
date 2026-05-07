import logging
from app.domain.skill_gap.graph.workflow import build_skill_gap_workflow
from app.domain.skill_gap.schemas.state import SkillGapState
from app.domain.skill_gap.schemas.models import SkillGapOutput

logger = logging.getLogger(__name__)

class SkillGapService:
    """Service pour l'analyse des écarts de compétences (Skill Gap)."""

    def __init__(self):
        # Compilation du workflow une seule fois (Singleton)
        self._workflow = build_skill_gap_workflow()

    async def analyze_skill_gap(self, candidate_cv: dict, job_offer: dict) -> SkillGapOutput:
        """
        Lance l'analyse complète de l'écart de compétences.
        """
        initial_state: SkillGapState = {
            "candidate_cv": candidate_cv,
            "job_offer": job_offer,
            "messages": [],
            "errors": [],
            "iteration_count": 0
        }
        
        final_state = await self._workflow.ainvoke(initial_state)
        
        return SkillGapOutput(
            skill_gap=final_state.get("skill_gap"),
            errors=final_state.get("errors", [])
        )

# Instance globale
skill_gap_service = SkillGapService()
