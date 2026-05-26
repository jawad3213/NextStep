import logging

from fastapi import APIRouter, HTTPException
from pydantic import BaseModel, Field

from app.domain.cv_optimizer.schemas.models import OptimizedCVOutput
from app.domain.cv_optimizer.service import cv_optimizer_service

logger = logging.getLogger(__name__)
router = APIRouter(prefix="/cv-optimizer", tags=["CV Optimizer"])


class CVOptimizeRequest(BaseModel):
    """Payload pour lancer l'optimisation d'un CV."""

    candidate_cv: dict = Field(..., description="Donnees du profil original")
    job_offer: dict = Field(..., description="Offre d'emploi analysee")
    skill_gap_analysis: dict | None = Field(None, description="Analyse d'ecart explicite")
    match_result: dict | None = Field(None, description="Alias historique de skill_gap_analysis")


@router.post(
    "/optimize",
    response_model=OptimizedCVOutput,
    summary="Optimiser un CV pour une offre d'emploi",
    description="Reorganise et reecrit les descriptions du CV en langage oriente resultat (STAR).",
)
async def optimize_cv_endpoint(payload: CVOptimizeRequest) -> OptimizedCVOutput:
    """
    POST /optimize
    Execute le workflow LangGraph de l'agent CV Optimizer avec validation anti-hallucination.
    """
    logger.info("POST /cv-optimizer/optimize - Demarrage de l'optimisation")
    try:
        result = await cv_optimizer_service.optimize_cv(
            candidate_cv=payload.candidate_cv,
            job_offer=payload.job_offer,
            skill_gap_analysis=payload.skill_gap_analysis,
            match_result=payload.match_result,
        )
        return result
    except Exception as e:
        logger.error("POST /cv-optimizer/optimize failed - %s", str(e))
        raise HTTPException(status_code=500, detail=f"Erreur lors de l'optimisation: {str(e)}")
