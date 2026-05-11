from fastapi import APIRouter, UploadFile, File, HTTPException
from fastapi.responses import StreamingResponse
from fastapi import APIRouter, UploadFile, File, HTTPException
from fastapi.responses import StreamingResponse
from app.domain.resume.service import extract_text_from_pdf, parse_cv_with_ai
import json
import asyncio

router = APIRouter()

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
        print(f"Error parsing resume: {str(e)}")
        raise HTTPException(status_code=500, detail=f"Erreur lors de l'analyse du CV : {str(e)}")
