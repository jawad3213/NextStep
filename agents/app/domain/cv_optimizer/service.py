import logging
from app.domain.cv_optimizer.graph.workflow import build_cv_optimizer_workflow
from app.domain.cv_optimizer.schemas.state import CVOptimizerState
from app.domain.cv_optimizer.schemas.models import OptimizedCVOutput

logger = logging.getLogger(__name__)

class CVOptimizerService:
    """Service pour l'optimisation de CV (CV Optimizer)."""

    def __init__(self):
        # Compilation du workflow une seule fois (Singleton)
        self._workflow = build_cv_optimizer_workflow()

    async def optimize_cv(self, candidate_cv: dict, job_offer: dict) -> OptimizedCVOutput:
        """
        Lance le processus d'optimisation du CV.
        """
        initial_state: CVOptimizerState = {
            "candidate_cv": candidate_cv,
            "job_offer": job_offer,
            "messages": [],
            "errors": [],
            "iteration_count": 0
        }
        
        final_state = await self._workflow.ainvoke(initial_state)
        
        # Retourne le résultat structuré
        optimized_data = final_state.get("optimized_cv")
        if not optimized_data:
            # Sécurité si ça crash
            return OptimizedCVOutput(
                resume_optimise="",
                experiences_optimisees=[],
                projets_optimises=[],
                global_justification="Erreur lors de l'optimisation."
            )
            
        return OptimizedCVOutput(**optimized_data)

# Instance globale
cv_optimizer_service = CVOptimizerService()
