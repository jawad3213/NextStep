# ============================================================
# app/api/offer_routes.py
# Routes FastAPI du domaine OFFER (Orchestrateur)
# ============================================================
import logging
from fastapi import APIRouter, HTTPException
from pydantic import BaseModel, Field
from typing import Optional, Dict, Any

from app.domain.offer_analyzer.service import offer_analyzer_service
from app.domain.profile_retriever.service import profile_retriever_service
from app.domain.skill_gap.service import skill_gap_service

logger = logging.getLogger(__name__)
router = APIRouter(tags=["M2 — Offer Pipeline"])

class OfferInput(BaseModel):
    raw_text: str = Field(..., description="Le texte brut de l'offre d'emploi")
    user_id: int = Field(..., description="ID de l'utilisateur pour récupérer son profil")
    template_id: int = Field(1, description="ID du template CV choisi")
    generation_options: Optional[Dict[str, Any]] = Field(
        None,
        description="Options de génération email : language, tone, include_motivation_letter"
    )

class MatchRequest(BaseModel):
    user_id: int
    analyzed_offer: Dict[str, Any]

class PipelineResult(BaseModel):
    analyzed_offer: Optional[Dict[str, Any]] = None
    profile_data: Optional[Dict[str, Any]] = None
    skill_gap: Optional[Dict[str, Any]] = None
    email_draft: Optional[Dict[str, Any]] = None
    company_intelligence: Optional[Dict[str, Any]] = None
    errors: list = []
    warnings: list = []

from app.domain.pipeline.workflow import get_offer_pipeline

@router.post(
    "/run-pipeline",
    response_model=PipelineResult,
    summary="Pipeline complet — Agents 1-5",
    description=(
        "Orchestre l'analyse de l'offre, la récupération du profil, le scoring/skill gap, "
        "l'intelligence entreprise et la génération du brouillon d'email via LangGraph."
    ),
)
async def run_pipeline(payload: OfferInput) -> PipelineResult:
    """
    POST /run-pipeline — Appelé par le backend .NET.
    """
    logger.info("POST /run-pipeline — user_id=%s", payload.user_id)
    try:
        pipeline = get_offer_pipeline()
        initial_state = {
            "raw_offer_text":           payload.raw_text,
            "user_id":                  payload.user_id,
            "template_id":              payload.template_id,
            "generation_options":       payload.generation_options,
            "messages":                 [],
            "errors":                   [],
            "warnings":                 [],
            "normalized_offer_skills":  [],
            "normalized_keywords":      [],
            "normalized_profile_skills": [],
        }

        final_state = await pipeline.ainvoke(initial_state)

        return PipelineResult(
            analyzed_offer=final_state.get("analyzed_offer"),
            profile_data=final_state.get("profile_data"),
            skill_gap=final_state.get("match_result"),
            email_draft=final_state.get("email_draft"),
            company_intelligence=final_state.get("company_intelligence"),
            errors=final_state.get("errors", []),
            warnings=final_state.get("warnings", []),
        )
    except Exception as e:
        logger.error("POST /run-pipeline ❌ — %s", str(e))
        raise HTTPException(status_code=500, detail=f"Erreur pipeline : {str(e)}")


@router.post(
    "/analyze-offer",
    response_model=dict,
    summary="Agent 1 uniquement — Analyser une offre (LLM)",
)
async def analyze_offer(payload: OfferInput) -> dict:
    """POST /analyze-offer — Agent 1 isolé."""
    logger.info("POST /analyze-offer — user_id=%s", payload.user_id)
    try:
        result = await offer_analyzer_service.analyze(payload.raw_text)
        if not result or result.get("errors"):
            raise HTTPException(status_code=502, detail="Erreur LLM — analyse échouée")
        return result
    except HTTPException:
        raise
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))


@router.post(
    "/match",
    response_model=dict,
    summary="Agents 2-3 — Matching profil ↔ offre",
)
async def match_profile(payload: MatchRequest) -> dict:
    """POST /match — Profil + Skill Gap isolés."""
    logger.info("POST /match — user_id=%s", payload.user_id)
    try:
        profile_res = await profile_retriever_service.get_profile(str(payload.user_id))
        profile_data = profile_res.get("profile_data", {})
        
        gap_res = await skill_gap_service.analyze_skill_gap(
            candidate_cv=profile_data,
            job_offer=payload.analyzed_offer
        )
        return gap_res.model_dump() if gap_res else {}
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))
