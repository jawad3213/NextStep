from fastapi import APIRouter, UploadFile, File, HTTPException
from pydantic import BaseModel
from typing import Optional
from app.domain.resume.service import extract_text_from_pdf, parse_cv_with_ai, parse_linkedin_with_ai
import logging

router = APIRouter()
logger = logging.getLogger(__name__)

class LinkedInRequest(BaseModel):
    url: Optional[str] = None
    rawText: Optional[str] = None

@router.post("/parse")
async def parse_resume(file: UploadFile = File(...)):
    if not file.filename.endswith(".pdf"):
        raise HTTPException(status_code=400, detail="Seuls les fichiers PDF sont supportés pour le moment.")

    try:
        # 1. Extraction du texte du PDF
        pdf_content = await file.read()
        text = await extract_text_from_pdf(pdf_content)

        if not text.strip():
            raise HTTPException(status_code=400, detail="Impossible d'extraire du texte du PDF.")

        # 2. Appel au service d'analyse AI
        parsed_json = await parse_cv_with_ai(text)

        return parsed_json

    except Exception as e:
        logger.exception("Error parsing resume")
        raise HTTPException(status_code=500, detail=f"Erreur lors de l'analyse du CV : {str(e)}")

@router.post("/parse-linkedin")
async def parse_linkedin_route(payload: LinkedInRequest):
    try:
        parsed_json = await parse_linkedin_with_ai(url=payload.url, raw_text=payload.rawText)
        return parsed_json
    except Exception as e:
        logger.exception("Error parsing LinkedIn payload")
        raise HTTPException(status_code=500, detail=f"Erreur lors de l'import LinkedIn : {str(e)}")
