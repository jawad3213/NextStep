import os
import fitz  # PyMuPDF
import json
from langchain_groq import ChatGroq
from langchain_core.prompts import ChatPromptTemplate
from fastapi import HTTPException
from .prompts import SYSTEM_PROMPT

async def extract_text_from_pdf(pdf_content: bytes) -> str:
    """
    Extracts text from a PDF byte stream.
    """
    try:
        doc = fitz.open(stream=pdf_content, filetype="pdf")
        text = ""
        for page in doc:
            text += page.get_text()
        doc.close()
        return text
    except Exception as e:
        raise Exception(f"Error extracting text from PDF: {str(e)}")

import re
import logging
from app.core.config import get_llm

logger = logging.getLogger(__name__)

from .schemas import ResumeParsedSchema

async def parse_cv_with_ai(cv_text: str) -> dict:
    """
    Sends the CV text to the LLM and returns a structured JSON matching ResumeParsedSchema.
    Uses the stable JSON Mode of Groq for extraction, followed by Pydantic validation for perfect typing.
    """
    llm = get_llm(temperature=0.0, agent_name="resume")
    if hasattr(llm, "bind"):
        llm = llm.bind(response_format={"type": "json_object"})

    prompt = ChatPromptTemplate.from_messages([
        ("system", SYSTEM_PROMPT),
        ("human", "Voici le texte du CV à parser :\n\n{cv_text}")
    ])

    chain = prompt | llm
    
    try:
        response = await chain.ainvoke({"cv_text": cv_text})
        logger.info(f"🚀 RAW LLM RESPONSE:\n{response.content}\n====================")
        
        # 1. Nettoyage et parsing robuste de la chaîne JSON
        parsed_data = clean_and_parse_json(response.content)
        
        # 2. Validation de type et complétion via Pydantic
        try:
            validated = ResumeParsedSchema(**parsed_data)
            return validated.model_dump()
        except Exception as pydantic_err:
            logger.warning(f"⚠️ Validation Pydantic partielle (utilisation du fallback brut) : {pydantic_err}")
            return parsed_data
            
    except Exception as e:
        logger.error(f"❌ Erreur critique lors de l'appel ou du parsing du CV : {e}")
        raise HTTPException(status_code=500, detail=f"Erreur d'analyse du CV : {str(e)}")

def clean_and_parse_json(raw_content: str) -> dict:
    """
    Cleans the AI response and parses it as JSON.
    Supports comments stripping, trailing commas, and non-strict control characters.
    """
    if not raw_content:
        raise ValueError("L'IA a retourné une réponse vide.")

    # Enlever les commentaires de type // s'ils existent
    text_clean = re.sub(r'(?<!:)\/\/.*$', '', raw_content, flags=re.MULTILINE)
    
    # Extraire le bloc JSON des backticks markdown si présent
    match = re.search(r'```(?:json)?\s*([\s\S]*?)\s*```', text_clean, re.IGNORECASE)
    if match:
        json_str = match.group(1).strip()
    else:
        start = text_clean.find("{")
        end = text_clean.rfind("}")
        if start != -1 and end != -1:
            json_str = text_clean[start:end+1].strip()
        else:
            json_str = text_clean.strip()

    try:
        # Essai initial en mode non-strict (permet les sauts de ligne dans les chaînes, etc.)
        return json.loads(json_str, strict=False)
    except Exception as e:
        # Tentative de nettoyage des virgules traînantes devant une fermeture d'objet ou tableau
        json_str_repaired = re.sub(r',\s*([\]}])', r'\1', json_str)
        try:
            return json.loads(json_str_repaired, strict=False)
        except Exception as e2:
            # Si tout échoue, on log l'erreur et le contenu brut pour débugger
            logger.error(f"❌ Échec critique du parsing JSON du CV. Erreur originale : {e}. Erreur réparation : {e2}")
            logger.error(f"Contenu brut reçu de l'IA : \n{raw_content}")
            raise ValueError(f"Format JSON invalide même après extraction. Erreur : {str(e2)}")
