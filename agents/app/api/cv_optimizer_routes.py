import logging
from fastapi import APIRouter, HTTPException
from pydantic import BaseModel, Field
from app.domain.cv_optimizer.service import cv_optimizer_service
from app.domain.cv_optimizer.schemas.models import OptimizedCVOutput

logger = logging.getLogger(__name__)
router = APIRouter(prefix="/cv-optimizer", tags=["CV Optimizer"])

class CVOptimizeRequest(BaseModel):
    """Payload pour lancer l'optimisation d'un CV."""
    candidate_cv: dict = Field(..., description="Données du profil original (dictionnaire complet)")
    job_offer: dict = Field(..., description="Offre d'emploi analysée (dictionnaire JSON)")

@router.post(
    "/optimize",
    response_model=OptimizedCVOutput,
    summary="Optimiser un CV pour une offre d'emploi",
    description="Réorganise et réécrit les descriptions du CV en langage orienté résultat (STAR), avec justification complète."
)
async def optimize_cv_endpoint(payload: CVOptimizeRequest) -> OptimizedCVOutput:
    """
    POST /optimize
    Exécute le workflow LangGraph de l'agent CV Optimizer avec validation anti-hallucination.
    """
    logger.info("POST /cv-optimizer/optimize - Démarrage de l'optimisation")
    try:
        result = await cv_optimizer_service.optimize_cv(
            candidate_cv=payload.candidate_cv,
            job_offer=payload.job_offer
        )
        return result
    except Exception as e:
        logger.error(f"POST /cv-optimizer/optimize ❌ - {str(e)}")
        raise HTTPException(status_code=500, detail=f"Erreur lors de l'optimisation: {str(e)}")
