# ============================================================
# app/api/cv_engine_routes.py
# Routes FastAPI du domaine CV_ENGINE
#
# Endpoints :
#   POST /prepare-cv         — Pipeline complet (Nodes 1-3)
#   POST /optimize-skills    — Optimisation compétences seule
# ============================================================
import logging
from fastapi import APIRouter, HTTPException

from app.domain.cv_engine.schemas.cv_engine_schemas import (
    CvEngineRequest,
    SkillOptimizeRequest,
)
from app.domain.cv_engine.service import cv_engine_service

logger = logging.getLogger(__name__)
router = APIRouter(tags=["CV Engine — Préparation données CV"])


@router.post(
    "/prepare-cv",
    response_model=dict,
    summary="Pipeline CV Engine complet — Préparer les données CV pour QuestPDF",
    description=(
        "Charge le profil utilisateur, optimise les compétences "
        "(ciblage offre si fournie), et structure le tout dans le "
        "schéma JSON attendu par les templates QuestPDF (.NET)."
    ),
)
async def prepare_cv(payload: CvEngineRequest) -> dict:
    """POST /prepare-cv — Pipeline complet CV Engine."""
    logger.info(
        "POST /prepare-cv — user_id=%s | template=%s | has_offer=%s",
        payload.user_id, payload.template_slug, payload.offer_data is not None,
    )
    try:
        return await cv_engine_service.prepare_cv_data(
            user_id=payload.user_id,
            template_slug=payload.template_slug,
            offer_data=payload.offer_data,
            match_result=payload.match_result,
        )
    except Exception as e:
        logger.error("POST /prepare-cv ❌ — %s", str(e))
        raise HTTPException(status_code=500, detail=str(e))


@router.post(
    "/optimize-skills",
    response_model=dict,
    summary="Optimisation des compétences uniquement",
    description=(
        "Charge le profil et réordonne les compétences en fonction "
        "des scores de matching fournis. Utile pour un re-ranking rapide."
    ),
)
async def optimize_skills(payload: SkillOptimizeRequest) -> dict:
    """POST /optimize-skills — Skill Optimizer seul."""
    logger.info("POST /optimize-skills — user_id=%s", payload.user_id)
    try:
        return await cv_engine_service.optimize_skills_only(
            user_id=payload.user_id,
            match_result=payload.match_result,
        )
    except Exception as e:
        logger.error("POST /optimize-skills ❌ — %s", str(e))
        raise HTTPException(status_code=500, detail=str(e))
