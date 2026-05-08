import logging
from fastapi import APIRouter, HTTPException

from app.domain.cv_engine.schemas.models import CVEngineRequest
from app.domain.cv_engine.service import cv_engine_service

logger = logging.getLogger(__name__)
router = APIRouter(tags=["CV Engine — Préparation données CV"])

@router.post(
    "/cv-engine/format-questpdf",
    response_model=dict,
    summary="Fusionne le profil d'origine et le CV optimisé pour QuestPDF",
    description=(
        "Prend en entrée les données brutes (Profile Retriever) et le CV optimisé "
        "(CV Optimizer) pour produire un JSON déterministe respectant la structure "
        "CvData attendue par le backend .NET."
    ),
)
async def format_questpdf(payload: CVEngineRequest) -> dict:
    """POST /cv-engine/format-questpdf"""
    logger.info("POST /cv-engine/format-questpdf appelé.")
    try:
        return await cv_engine_service.format_for_questpdf(
            original_profile=payload.original_profile,
            optimized_cv=payload.optimized_cv
        )
    except Exception as e:
        logger.error("POST /cv-engine/format-questpdf ❌ — %s", str(e))
        raise HTTPException(status_code=500, detail=str(e))
