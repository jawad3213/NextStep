import os
from fastapi import APIRouter, UploadFile, File, HTTPException
import fitz  # PyMuPDF
from langchain_groq import ChatGroq
from langchain_core.prompts import ChatPromptTemplate
from pydantic import BaseModel, Field
from typing import List, Optional
import json

router = APIRouter()

# Modèle Pydantic pour structurer la réponse attendue de Groq
class ExperienceSchema(BaseModel):
    entreprise: str
    poste: str
    dateDebut: Optional[str] = None
    dateFin: Optional[str] = None
    missions: str
    ville: Optional[str] = None
    type: Optional[str] = "Stage"

class FormationSchema(BaseModel):
    etablissement: str
    diplome: str
    annee: Optional[str] = None
    anneeFin: Optional[str] = None
    ville: Optional[str] = None
    specialisation: Optional[str] = None

class ProjectSchema(BaseModel):
    titre: str
    description: str
    technologies: str
    lien: Optional[str] = None

class ExtracurricularSchema(BaseModel):
    titre: str
    organisation: str
    dateDebut: Optional[str] = None
    dateFin: Optional[str] = None
    description: str

class CertificationSchema(BaseModel):
    titre: str
    organisation: str
    date: Optional[str] = None
    lien: Optional[str] = None

class ProfileSchema(BaseModel):
    personal: dict = Field(description="Nom, Prenom, Email, Telephone, Ville, Pays, TitrePoste, ResumeProfessionnel")
    experience: List[ExperienceSchema]
    education: List[FormationSchema]
    skills: List[dict] = Field(description="Liste d'objets {nom: str, niveau: str, typeCompetence: str}")
    projects: Optional[List[ProjectSchema]] = []
    extracurricular: Optional[List[ExtracurricularSchema]] = []
    certifications: Optional[List[CertificationSchema]] = []

SYSTEM_PROMPT = """
Tu es un expert en recrutement et en parsing de CV. 
Ta tâche est d'extraire les informations d'un CV texte brut et de les retourner UNIQUEMENT au format JSON structuré.

Voici le schéma JSON strict à respecter :
{{
  "personal": {{
    "nom": "Nom de famille",
    "prenom": "Prénom",
    "email": "Email",
    "telephone": "Téléphone",
    "ville": "Ville",
    "pays": "Pays",
    "titrePoste": "Titre actuel ou recherché",
    "resumeProfessionnel": "Court résumé de 2-3 phrases"
  }},
  "experience": [
    {{
      "entreprise": "Nom",
      "poste": "Titre",
      "dateDebut": "YYYY-MM-DD",
      "dateFin": "YYYY-MM-DD ou null",
      "missions": "Texte décrivant les tâches",
      "ville": "Ville",
      "type": "Stage/Alternance/CDI/CDD/Freelance"
    }}
  ],
  "education": [
    {{
      "etablissement": "Nom",
      "diplome": "Titre du diplôme",
      "annee": "Année début",
      "anneeFin": "Année fin",
      "ville": "Ville",
      "specialisation": "Domaine d'étude"
    }}
  ],
  "skills": [
    {{ "nom": "Nom compétence", "niveau": "A1/A2/B1/B2/C1/C2/Native", "typeCompetence": "Technical/Soft Skill/Language" }}
  ],
  "projects": [
    {{
      "titre": "Nom du projet",
      "description": "Description détaillée",
      "technologies": "Liste séparée par des virgules",
      "lien": "Lien GitHub ou démo ou null"
    }}
  ],
  "extracurricular": [
    {{
      "titre": "Rôle / Titre",
      "organisation": "Nom de l'organisation",
      "dateDebut": "YYYY-MM-DD",
      "dateFin": "YYYY-MM-DD ou null",
      "description": "Description des activités"
    }}
  ],
  "certifications": [
    {{
      "titre": "Nom certification",
      "organisation": "Organisme émetteur",
      "date": "YYYY-MM-DD ou YYYY",
      "lien": "Lien de vérification ou null"
    }}
  ]
}}

Règles critiques :
1. Si une information est manquante, utilise null ou une liste vide [].
2. Ne réponds rien d'autre que le JSON.
3. Pour les skills, extrais TOUTES les compétences techniques, soft skills ET les langues parlées.
4. Pour le champ 'niveau': utilise A1, A2, B1, B2, C1, C2, ou Native pour les langues maternelles.
5. Pour le champ 'typeCompetence': utilise exactement "Technical", "Soft Skill", ou "Language" (pour les langues).
6. Traduis les missions et descriptions en anglais.
"""

@router.post("/parse")
async def parse_resume(file: UploadFile = File(...)):
    if not file.filename.endswith(".pdf"):
        raise HTTPException(status_code=400, detail="Seuls les fichiers PDF sont supportés pour le moment.")

    try:
        # 1. Extraction du texte du PDF
        pdf_content = await file.read()
        doc = fitz.open(stream=pdf_content, filetype="pdf")
        text = ""
        for page in doc:
            text += page.get_text()
        doc.close()

        if not text.strip():
            raise HTTPException(status_code=400, detail="Impossible d'extraire du texte du PDF.")

        # 2. Appel à Groq via LangChain
        api_key = os.getenv("GROQ_API_KEY")
        if not api_key:
            raise HTTPException(status_code=500, detail="GROQ_API_KEY non configurée sur le serveur.")

        llm = ChatGroq(
            temperature=0,
            model_name="llama-3.3-70b-versatile", # Modèle puissant et rapide sur Groq
            groq_api_key=api_key
        )

        prompt = ChatPromptTemplate.from_messages([
            ("system", SYSTEM_PROMPT),
            ("human", "Voici le texte du CV à parser :\n\n{cv_text}")
        ])

        chain = prompt | llm
        # Forcer la réponse en JSON pur si possible avec Groq
        response = await chain.ainvoke({"cv_text": text})

        # 3. Nettoyage et parsing de la réponse
        raw_content = response.content.strip()
        print(f"--- DEBUG AI RESPONSE ---\n{raw_content}\n--- END DEBUG ---")
        
        # Enlever les éventuels backticks markdown JSON
        if "```json" in raw_content:
            raw_content = raw_content.split("```json")[1].split("```")[0].strip()
        elif "```" in raw_content:
            raw_content = raw_content.split("```")[1].split("```")[0].strip()

        try:
            parsed_json = json.loads(raw_content)
        except json.JSONDecodeError:
            # Tentative de récupération si l'IA a mis du texte avant/après
            start_idx = raw_content.find("{")
            end_idx = raw_content.rfind("}")
            if start_idx != -1 and end_idx != -1:
                parsed_json = json.loads(raw_content[start_idx:end_idx+1])
            else:
                raise ValueError("Format JSON invalide reçu de l'IA.")

        return parsed_json

    except Exception as e:
        print(f"Error parsing resume: {str(e)}")
        raise HTTPException(status_code=500, detail=f"Erreur lors de l'analyse du CV : {str(e)}")
