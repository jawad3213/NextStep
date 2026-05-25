import logging

from app.domain.cv_optimizer.graph.workflow import build_cv_optimizer_workflow
from app.domain.cv_optimizer.schemas.models import OptimizedCVOutput
from app.domain.cv_optimizer.schemas.state import CVOptimizerState

logger = logging.getLogger(__name__)


class CVOptimizerService:
    """Service pour l'optimisation de CV (CV Optimizer)."""

    def __init__(self):
        # Compilation du workflow une seule fois (Singleton)
        self._workflow = build_cv_optimizer_workflow()

    async def optimize_cv(
        self,
        candidate_cv: dict,
        job_offer: dict,
        match_result: dict = None,
        skill_gap_analysis: dict = None,
    ) -> OptimizedCVOutput:
        """Lance le processus d'optimisation du CV."""
        resolved_skill_gap = skill_gap_analysis or match_result
        initial_state: CVOptimizerState = {
            "candidate_cv": candidate_cv,
            "job_offer": job_offer,
            "skill_gap_analysis": resolved_skill_gap,
            "match_result": match_result or resolved_skill_gap,
            "messages": [],
            "errors": [],
            "iteration_count": 0,
        }

        final_state = await self._workflow.ainvoke(initial_state)

        # Retourne le resultat structure
        optimized_data = final_state.get("optimized_cv")
        if not optimized_data:
            return OptimizedCVOutput(
                resume_optimise={"contenu": ""},
                experiences_optimisees=[],
                projets_optimises=[],
                formations_optimisees=[],
                certifications_optimisees=[],
                competences_reordonnees=[],
                competences_mises_en_avant=[],
            )

        return OptimizedCVOutput(**optimized_data)


cv_optimizer_service = CVOptimizerService()
