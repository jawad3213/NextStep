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

from .schemas import ResumeParsedSchema, normalize_language_level

def _normalize_language_levels(data: dict) -> dict:
    """Post-processes language entries to normalize level descriptors."""
    languages = data.get("languages", [])
    if isinstance(languages, list):
        for lang in languages:
            if isinstance(lang, dict):
                lang["niveau"] = normalize_language_level(lang.get("niveau"))
    return data

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
            result = validated.model_dump()
        except Exception as pydantic_err:
            logger.warning(f"⚠️ Validation Pydantic partielle (utilisation du fallback brut) : {pydantic_err}")
            result = parsed_data
            
        # 3. Normalisation des niveaux de langue (mapping français -> CECRL / standard)
        return _normalize_language_levels(result)
            
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

async def parse_linkedin_with_ai(url: str = None, raw_text: str = None) -> dict:
    """
    Parses LinkedIn profile data using LLM.
    If raw_text is provided, it is parsed directly using parse_cv_with_ai.
    If only url is provided, we extract name and search for public details, or construct a clean empty profile if no data is found.
    """
    import re
    import logging
    # On utilise les fonctions déjà présentes dans le scope global
    from app.domain.company.tools.web_tool import smart_search
    from app.domain.resume.schemas import ResumeParsedSchema

    logger = logging.getLogger(__name__)

    # --- Logique Globale Wrapper ---
    try:
        if raw_text and raw_text.strip():
            logger.info("Parsing copy-pasted LinkedIn raw text...")
            return await parse_cv_with_ai(raw_text)

        if not url or not url.strip():
            raise HTTPException(status_code=400, detail="Veuillez fournir une URL LinkedIn ou le texte brut du profil.")

        logger.info(f"Importing LinkedIn profile from URL: {url}")
        
        # 1. Extract name from URL
        name = ""
        match = re.search(r"linkedin\.com/in/([^/\?#]+)", url, re.IGNORECASE)
        if match:
            slug = match.group(1)
            slug_clean = re.sub(r'-[0-9a-zA-Z]+$', '', slug)
            if len(slug_clean) < 3:
                slug_clean = slug
            name = " ".join([part.capitalize() for part in slug_clean.split("-") if part])

        if not name:
            name = "Utilisateur LinkedIn"

        parts = name.split(" ")
        prenom = parts[0]
        nom = " ".join(parts[1:]) if len(parts) > 1 else ""

        # 2. Try searching public info
        scraped_text = ""
        try:
            logger.info(f"Searching public info for: {name}")
            # On ajoute un timeout implicite via le smart_search qui utilise httpx
            search_results = await smart_search(f"{name} site:linkedin.com/in/")
            snippets = []
            for r in search_results:
                snippets.append(f"{r.get('title', '')}: {r.get('snippet', '')}")
            scraped_text = "\n".join(snippets)
        except Exception as search_err:
            logger.warning(f"Failed searching public info for {name}: {search_err}")

        # 3. LLM Completion
        llm = get_llm(temperature=0.0, agent_name="resume")
        if hasattr(llm, "bind"):
            llm = llm.bind(response_format={"type": "json_object"})

        system_prompt = f"""
Ta tâche est d'extraire les informations d'un profil LinkedIn à partir des snippets publics fournis.
Nom extrait : {prenom} {nom}
Lien : {url}

IMPORTANT : Ne simule pas d'infos. Si vide, laisse vide.
Retourne un JSON valide respectant le schéma ResumeParsedSchema.
"""

        prompt = ChatPromptTemplate.from_messages([
            ("system", system_prompt),
            ("human", f"Snippets trouvés :\n{scraped_text}\n\nGénère le profil JSON.")
        ])

        chain = prompt | llm
        response = await chain.ainvoke({})
        parsed_data = clean_and_parse_json(response.content)
        
        # Merge mandatory info
        if "personal" not in parsed_data: parsed_data["personal"] = {}
        parsed_data["personal"]["prenom"] = parsed_data["personal"].get("prenom") or prenom
        parsed_data["personal"]["nom"] = parsed_data["personal"].get("nom") or nom
        parsed_data["personal"]["linkedinUrl"] = url
        
        # Validation Pydantic
        try:
            return ResumeParsedSchema(**parsed_data).model_dump()
        except Exception:
            return parsed_data

    except Exception as e:
        logger.error(f"❌ Erreur critique Import LinkedIn : {e}")
        # Toujours retourner un objet valide pour éviter le crash 500 du frontend
        return {
            "personal": {"nom": nom if 'nom' in locals() else "", "prenom": prenom if 'prenom' in locals() else "Utilisateur", "email": "", "telephone": "", "ville": "", "pays": "", "titrePoste": "", "resumeProfessionnel": ""},
            "experience": [], "education": [], "projects": [], "extracurricular": [], "certifications": [], "skills": [], "languages": []
        }
