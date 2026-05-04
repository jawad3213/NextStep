# ============================================================
# app/api/job_routes.py
# Routes FastAPI du domaine JOB
#
# Endpoints :
#   POST /prepare-cv-data    — Agent 5 (CV Formatter)
#   POST /generate-email     — Agent 6 (Email Composer LLM)
# ============================================================
import logging
from fastapi import APIRouter, HTTPException

from app.domain.job.schemas.job_schemas import (
    CVDataRequest, EmailGenerateRequest,
)
from app.domain.job.service import job_service

logger = logging.getLogger(__name__)
router = APIRouter(tags=["Job — CV & Email"])


@router.post(
    "/prepare-cv-data",
    response_model=dict,
    summary="Agent 5 — Préparer les données CV pour QuestPDF",
    description=(
        "Formate le profil + offre + scores dans le schéma JSON "
        "attendu par QuestPDF (.NET M3)."
    ),
)
async def prepare_cv_data(payload: CVDataRequest) -> dict:
    """POST /prepare-cv-data — Agent 5."""
    logger.info("POST /prepare-cv-data — user_id=%s | template=%d", payload.user_id, payload.template_id)
    try:
        return await job_service.prepare_cv_data(
            user_id=payload.user_id,
            offer_data=payload.offer_data,
            profile_data=payload.profile_data,
            match_result=payload.match_result,
            template_id=payload.template_id,
        )
    except Exception as e:
        logger.error("POST /prepare-cv-data ❌ — %s", str(e))
        raise HTTPException(status_code=500, detail=str(e))


@router.post(
    "/generate-email",
    response_model=dict,
    summary="Agent 6 — Générer l'email de candidature (LLM)",
    description=(
        "Utilise le LLM (Groq / OpenAI) pour rédiger un email de candidature "
        "ou de relance personnalisé (J+7)."
    ),
)
async def generate_email(payload: EmailGenerateRequest) -> dict:
    """POST /generate-email — Agent 6 (LLM)."""
    logger.info(
        "POST /generate-email — user_id=%s | type=%s",
        payload.user_id, payload.email_type,
    )
    try:
        return await job_service.generate_email(
            user_id=payload.user_id,
            candidature_id=payload.candidature_id,
            offer_data=payload.offer_data,
            profile_data=payload.profile_data,
            email_type=payload.email_type,
        )
    except Exception as e:
        logger.error("POST /generate-email ❌ — %s", str(e))
        raise HTTPException(status_code=500, detail=str(e))
