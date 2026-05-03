# ============================================================
# app/api/offer_routes.py
# Routes FastAPI du domaine OFFER
#
# Endpoints :
#   POST /run-pipeline       — Pipeline complet (6 agents)
#   POST /analyze-offer      — Agent 1 uniquement
#   POST /match              — Agents 2-3-4 uniquement
# ============================================================
import logging
from fastapi import APIRouter, HTTPException

from app.domain.offer.schemas.offer_schemas import (
    OfferInput, MatchRequest, PipelineResult,
)
from app.domain.offer.service import offer_service

logger = logging.getLogger(__name__)
router = APIRouter(tags=["M2 — Offer Pipeline"])


@router.post(
    "/run-pipeline",
    response_model=PipelineResult,
    summary="Pipeline complet — Agents 1-4 + stubs 5-6",
    description=(
        "Reçoit une offre brute + user_id + template_id. "
        "Orchestre les 4 agents M2 via LangGraph StateGraph. "
        "Retourne le résultat complet (analyse, profil, scores, CV JSON, email)."
    ),
)
async def run_pipeline(payload: OfferInput) -> PipelineResult:
    """
    POST /run-pipeline — Appelé par le backend .NET.

    Flux :
      1. Initialise l'OfferState avec les données d'entrée
      2. Invoque le graphe LangGraph (Router + 6 agents)
      3. Retourne le state final comme PipelineResult
    """
    logger.info("POST /run-pipeline — user_id=%s | template=%d", payload.user_id, payload.template_id)
    try:
        return await offer_service.run_pipeline(
            raw_text=payload.raw_text,
            user_id=payload.user_id,
            template_id=payload.template_id,
        )
    except Exception as e:
        logger.error("POST /run-pipeline ❌ — %s", str(e))
        raise HTTPException(status_code=500, detail=f"Erreur pipeline : {str(e)}")


@router.post(
    "/analyze-offer",
    response_model=dict,
    summary="Agent 1 uniquement — Analyser une offre (LLM)",
    description="Lance uniquement Agent 1 (Offer Analyzer LLM) sans le pipeline complet.",
)
async def analyze_offer(payload: OfferInput) -> dict:
    """POST /analyze-offer — Agent 1 isolé."""
    logger.info("POST /analyze-offer — user_id=%s", payload.user_id)
    try:
        result = await offer_service.analyze_offer_only(
            raw_text=payload.raw_text,
            user_id=payload.user_id,
        )
        if not result:
            raise HTTPException(status_code=502, detail="Erreur LLM — analyse échouée")
        return result
    except HTTPException:
        raise
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))


@router.post(
    "/match",
    response_model=dict,
    summary="Agents 2-3-4 — Matching profil ↔ offre",
    description="Lance Agents 2 (profil), 3 (normalisation), 4 (scoring) sans Agent 1.",
)
async def match_profile(payload: MatchRequest) -> dict:
    """POST /match — Agents 2-3-4 isolés."""
    logger.info("POST /match — user_id=%s", payload.user_id)
    try:
        result = await offer_service.match_profile(
            user_id=payload.user_id,
            analyzed_offer=payload.analyzed_offer,
        )
        return result or {}
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))
