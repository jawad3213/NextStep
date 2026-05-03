# ============================================================
# app/domain/company/schemas/company_schemas.py
# Modèles Pydantic du domaine COMPANY
# ============================================================
from pydantic import BaseModel, Field
from typing import Optional


class CompanyAnalyzeRequest(BaseModel):
    """Requête pour l'analyse d'une entreprise."""
    company_name: str = Field(..., min_length=2)
    user_id: str
    offer_data: dict = Field(..., description="Offre analysée (Agent 1)")
    profile_data: Optional[dict] = None


class CompanyInfo(BaseModel):
    """Informations enrichies sur l'entreprise."""
    nom: str
    secteur: Optional[str] = None
    taille: Optional[str] = None          # startup | pme | grand_groupe
    localisation: Optional[str] = None
    description: Optional[str] = None
    technologies_stack: list[str] = Field(default_factory=list)
    type_contrat_dominant: Optional[str] = None
    remote_policy: Optional[str] = None   # full_remote | hybride | presentiel


class CompanyAnalysisResult(BaseModel):
    """Résultat complet de l'analyse entreprise."""
    company_info: CompanyInfo
    company_culture_score: int = Field(ge=0, le=100)
    company_insights: list[str] = Field(default_factory=list)
