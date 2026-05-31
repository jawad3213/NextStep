# ============================================================
# app/api/company_routes.py
# Routes FastAPI du domaine COMPANY
#
# Endpoints :
#   POST /analyze-company    — Analyse entreprise + score culture
# ============================================================
import logging
from fastapi import APIRouter, HTTPException, Depends
from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy import select

from app.core.database import get_db
from app.core.models import IntelEntreprise
from app.domain.company.schemas.models import CompanyAnalyzeRequest
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
async def analyze_company(
    payload: CompanyAnalyzeRequest,
    db: AsyncSession = Depends(get_db)
) -> dict:
    """POST /analyze-company."""
    company_name = payload.company_name.strip()
    logger.info("POST /analyze-company — '%s'", company_name)
    try:
        # Check if company already analyzed in DB
        stmt = select(IntelEntreprise).where(IntelEntreprise.nom_entreprise.ilike(company_name)).order_by(IntelEntreprise.date_collecte.desc())
        res = await db.execute(stmt)
        row = res.scalars().first()
        
        if row:
            logger.info("🏢 Company '%s' found in database, retrieving cached intelligence...", company_name)
            job_title = payload.offer_data.get("titre", "Unknown")
            
            note = row.note_glassdoor or 0.0
            culture_score = int(note * 20) if note > 0 else 75
            
            salaries = []
            if row.salaire_min is not None or row.salaire_max is not None:
                min_s = row.salaire_min or 0
                max_s = row.salaire_max or 0
                avg_s = int((min_s + max_s) / 2)
                salaries.append({
                    "job_title": job_title,
                    "location": "National",
                    "min_salary": min_s,
                    "max_salary": max_s,
                    "avg_salary": avg_s,
                    "currency": row.devise_salaire or "MAD",
                    "source": "Données historiques (Base)"
                })
                
            intelligence = {
                "nom": row.nom_entreprise,
                "summary": row.resume_entreprise or "",
                "sector": "Secteur technologique",
                "hq_location": "Non spécifié",
                "linkedin_url": "",
                "culture": {
                    "culture_score": culture_score,
                    "turnover_rate": "medium",
                    "work_life_balance": note,
                    "glassdoor_rating": note,
                    "key_values": ["Adaptabilité", "Excellence"],
                    "top_reviews": []
                },
                "salaries": salaries,
                "actualites": row.actualites or [],
                "interview_difficulty": row.difficulte_entretien or "medium",
                "interview_questions": row.questions_connues or [],
                "pros": ["Innovation", "Excellence technique"],
                "cons": ["Environnement dynamique"],
                "career_opportunities": []
            }
            
            return {
                "intelligence": intelligence,
                "score": culture_score,
                "recommendations": [
                    "Se renseigner sur les technologies récentes de l'entreprise",
                    "Mettre en avant sa capacité d'adaptation et d'apprentissage rapide"
                ],
                "summary": row.resume_entreprise or "",
                "skill_gap": {}
            }

        # Otherwise, run the LangGraph agent
        return await company_service.get_company_intelligence(
            company_name=company_name,
            job_title=payload.offer_data.get("titre", "Unknown"),
            user_id=str(payload.user_id),
            candidate_cv=payload.profile_data,
            job_offer=payload.offer_data
        )
    except Exception as e:
        logger.error("POST /analyze-company ❌ — %s", str(e))
        raise HTTPException(status_code=500, detail=str(e))
