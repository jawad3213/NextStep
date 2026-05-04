# ============================================================
# app/domain/job/schemas/job_schemas.py
# Modèles Pydantic du domaine JOB (Candidature)
# ============================================================
from pydantic import BaseModel, Field
from typing import Optional


class CVDataRequest(BaseModel):
    """Requête pour la préparation des données CV (Agent 5)."""
    user_id: str
    offer_data: dict = Field(..., description="Offre analysée par Agent 1")
    profile_data: dict = Field(..., description="Profil candidat (Agent 2)")
    match_result: dict = Field(..., description="Scores (Agent 4)")
    template_id: int = Field(default=1, ge=1, le=3)


class EmailGenerateRequest(BaseModel):
    """Requête pour la génération d'email de candidature (Agent 6)."""
    user_id: str
    candidature_id: str
    offer_data: dict
    profile_data: dict
    email_type: str = Field(default="candidature", pattern="^(candidature|relance)$")
    langue: str = Field(default="fr", pattern="^(fr|en)$")


class CVDataResult(BaseModel):
    """Résultat Agent 5 — Données formatées pour QuestPDF."""
    sections: dict
    metadata: dict
    score_ats: int = Field(ge=0, le=100)


class EmailDraftResult(BaseModel):
    """Résultat Agent 6 — Email rédigé par le LLM."""
    objet: str
    corps: str
    type: str
    langue: str = "fr"


class CandidatureStatusUpdate(BaseModel):
    """Mise à jour du statut d'une candidature."""
    candidature_id: str
    statut: str = Field(..., pattern="^(EN_ATTENTE|ENVOYE|VU|REPONDU|RELANCE)$")
    commentaire: Optional[str] = None
