# ============================================================
# app/api/company_routes.py
# Routes FastAPI du domaine COMPANY
#
# Endpoints :
#   POST /analyze-company    — Analyse entreprise + score culture
# ============================================================
import logging
from fastapi import APIRouter, HTTPException

from app.domain.company.schemas.company_schemas import CompanyAnalyzeRequest
from app.domain.company.service import company_service

logger = logging.getLogger(__name__)
router = APIRouter(tags=["Company — Analyse Entreprise"])


@router.post(
    "/analyze-company",
    response_model=dict,
    summary="Analyse d'une entreprise depuis l'offre",
    description=(
        "Extrait les informations sur l'entreprise, calcule le score de "
        "compatibilité culture candidat ↔ entreprise, et génère des insights."
    ),
)
async def analyze_company(payload: CompanyAnalyzeRequest) -> dict:
    """POST /analyze-company."""
    logger.info("POST /analyze-company — '%s'", payload.company_name)
    try:
        return await company_service.analyze_company(
            company_name=payload.company_name,
            offer_data=payload.offer_data,
            profile_data=payload.profile_data,
            user_id=payload.user_id,
        )
    except Exception as e:
        logger.error("POST /analyze-company ❌ — %s", str(e))
        raise HTTPException(status_code=500, detail=str(e))
