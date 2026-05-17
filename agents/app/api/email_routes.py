# ============================================================
# email_engine/router.py — FastAPI router for Email Agent
# Route: POST /email/generate
# ============================================================
import logging
from fastapi import APIRouter, HTTPException
from app.domain.email_engine.models import GenerateEmailRequest, GenerateEmailResponse
from app.domain.email_engine.service import generate_email_with_llm

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