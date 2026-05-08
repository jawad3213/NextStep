# ============================================================
# email_engine/router.py — FastAPI router for Email Agent
# Routes:
#   POST /email/generate          — Initial application email
#   POST /email/generate-follow-up — Follow-up / relance email
# ============================================================
import logging
from fastapi import APIRouter, HTTPException
from .models import (
    GenerateEmailRequest,
    GenerateEmailResponse,
    GenerateFollowUpEmailRequest,
    ClassifyResponseRequest,
    ClassifyResponseResult,
)
from .service import (
    generate_email_with_llm,
    generate_follow_up_email_with_llm,
    classify_recruiter_response_with_llm,
)

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/email", tags=["Email Agent"])


@router.post(
    "/generate",
    response_model=GenerateEmailResponse,
    summary="Générer un email de candidature via LLM",
    description=(
        "Reçoit les données du candidat et de l'offre depuis le backend .NET. "
        "Génère un email de candidature professionnel via Gemini (LangChain). "
        "Retourne un JSON structuré : subject, body, language, tone."
    ),
)
async def generate_email(
    payload: GenerateEmailRequest,
) -> GenerateEmailResponse:
    """POST /email/generate — Appelé par le backend .NET."""
    try:
        return await generate_email_with_llm(payload)
    except ValueError as exc:
        # Configuration errors (missing API key, unsupported provider)
        logger.error("Email agent — configuration error: %s", exc)
        raise HTTPException(status_code=500, detail=f"Configuration error: {exc}") from exc
    except Exception as exc:
        logger.error("Email agent — generation failed: %s", exc)
        raise HTTPException(
            status_code=500,
            detail=f"Erreur génération email : {exc}",
        ) from exc


@router.post(
    "/generate-follow-up",
    response_model=GenerateEmailResponse,
    summary="Générer un email de relance via LLM",
    description=(
        "Reçoit les données du candidat, de l'offre et de l'email précédent depuis le backend .NET. "
        "Génère un email de relance professionnel, poli et concis. "
        "Ne s'envoie PAS automatiquement — retourne un brouillon à valider par l'utilisateur."
    ),
)
async def generate_follow_up_email(
    payload: GenerateFollowUpEmailRequest,
) -> GenerateEmailResponse:
    """POST /email/generate-follow-up — Appelé par le backend .NET."""
    try:
        return await generate_follow_up_email_with_llm(payload)
    except ValueError as exc:
        logger.error("Follow-up agent — configuration error: %s", exc)
        raise HTTPException(status_code=500, detail=f"Configuration error: {exc}") from exc
    except Exception as exc:
        logger.error("Follow-up agent — generation failed: %s", exc)
        raise HTTPException(
            status_code=500,
            detail=f"Erreur génération relance : {exc}",
        ) from exc


@router.post(
    "/classify-response",
    response_model=ClassifyResponseResult,
    summary="Classifier la réponse d'un recruteur via LLM",
    description=(
        "Reçoit les métadonnées d'une réponse recruteur depuis le backend .NET. "
        "Classifie la réponse (entretien, refus, automatique, etc.) via le LLM. "
        "Ne lit pas Gmail directement. Ne génère pas de brouillon. Ne s'envoie pas."
    ),
)
async def classify_response(
    payload: ClassifyResponseRequest,
) -> ClassifyResponseResult:
    """POST /email/classify-response — Appelé par le backend .NET après détection d'une réponse."""
    try:
        return await classify_recruiter_response_with_llm(payload)
    except ValueError as exc:
        logger.error("Classify agent — configuration error: %s", exc)
        raise HTTPException(status_code=500, detail=f"Configuration error: {exc}") from exc
    except Exception as exc:
        logger.error("Classify agent — classification failed: %s", exc)
        raise HTTPException(
            status_code=500,
            detail=f"Erreur classification réponse : {exc}",
        ) from exc