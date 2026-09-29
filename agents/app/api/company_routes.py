# ============================================================
# app/api/company_routes.py
# ============================================================
import logging

from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.database import get_db
from app.domain.company.schemas.models import CompanyAnalyzeRequest
from app.domain.company.service import company_service, has_usable_data

logger = logging.getLogger(__name__)
router = APIRouter(tags=["Company - Analyse Entreprise"])


@router.post(
    "/analyze-company",
    response_model=dict,
    summary="Analyse d'une entreprise depuis l'offre",
    description=(
        "Extrait les informations sur l'entreprise, calcule le score de "
        "compatibilite culture candidat-entreprise, et genere des insights."
    ),
)
async def analyze_company(
    payload: CompanyAnalyzeRequest,
    db: AsyncSession = Depends(get_db),
) -> dict:
    company_name = payload.company_name.strip()
    logger.info("POST /analyze-company - '%s'", company_name)

    try:
        # A recent analysis of the same company is reused (see CompanyService.CACHE_DAYS).
        cached = await company_service.find_cached_intelligence(db, company_name)
        if cached:
            logger.info("Company '%s' served from cache.", company_name)
            return cached[0]

        result = await company_service.get_company_intelligence(
            company_name=company_name,
            job_title=payload.offer_data.get("titre", "Unknown"),
            user_id=str(payload.user_id),
            candidate_cv=payload.profile_data,
            job_offer=payload.offer_data,
        )
        if not has_usable_data(result):
            # Nothing found (e.g. web search blocked): not cached, so the next try searches again.
            return result
        try:
            await company_service.save_company_intelligence(db, result, company_name=company_name)
        except Exception as save_error:
            await db.rollback()
            logger.warning("Unable to cache company intelligence for '%s': %s", company_name, save_error)
        return result
    except Exception as e:
        logger.error("POST /analyze-company failed: %s", e, exc_info=True)
        raise HTTPException(status_code=500, detail="Erreur lors de l'analyse de l'entreprise.")
