# ============================================================
# app/schemas/api_schemas.py — Modèles Pydantic pour les API
# (Requests entrants & Responses sortants de FastAPI)
# ============================================================
from pydantic import BaseModel, Field
from typing import Optional


# ─────────────────────────────────────────────────────
# API Requests (entrées FastAPI → GraphQL)
# ─────────────────────────────────────────────────────

class OfferInput(BaseModel):
    """Payload envoyé par le backend .NET pour démarrer le pipeline."""
    raw_text: str = Field(..., min_length=50, description="Texte brut de l'offre d'emploi")
    user_id: str = Field(..., description="ID Keycloak de l'utilisateur")
    template_id: int = Field(default=1, ge=1, le=3, description="ID template CV (1=Modern, 2=Classic, 3=Creative)")


class MatchRequest(BaseModel):
    """Requête pour scoring uniquement (sans analyse LLM)."""
    user_id: str
    analyzed_offer: dict = Field(..., description="JSON d'offre déjà analysé par Agent 1")


# ─────────────────────────────────────────────────────
# Sous-modèles pour les réponses
# ─────────────────────────────────────────────────────

class CompetenceProfile(BaseModel):
    nom: str
    niveau: Optional[str] = None


class ExperienceProfile(BaseModel):
    titre: str
    entreprise: Optional[str] = None
    date_debut: Optional[str] = None
    date_fin: Optional[str] = None
    description: Optional[str] = None


class FormationProfile(BaseModel):
    diplome: str
    etablissement: Optional[str] = None
    annee: Optional[int] = None


class CertificationProfile(BaseModel):
    nom: str
    organisme: Optional[str] = None


class ProjetProfile(BaseModel):
    titre: str
    description: Optional[str] = None
    technologies: Optional[list[str]] = Field(default_factory=list)


# ─────────────────────────────────────────────────────
# API Responses (sorties FastAPI → .NET)
# ─────────────────────────────────────────────────────

class AnalyzedOffer(BaseModel):
    """Résultat Agent 1 — JSON structuré de l'offre (via LLM)."""
    titre: str
    entreprise: Optional[str] = None
    type_contrat: Optional[str] = None
    localisation: Optional[str] = None
    competences_requises: list[str] = Field(default_factory=list)
    competences_souhaitees: list[str] = Field(default_factory=list)
    keywords_ats: list[str] = Field(default_factory=list)
    annees_experience: Optional[int] = None
    niveau_etudes: Optional[str] = None
    description_poste: Optional[str] = None


class ProfileJson(BaseModel):
    """Résultat Agent 2 — Profil candidat depuis la DB."""
    user_id: str
    nom: Optional[str] = None
    prenom: Optional[str] = None
    titre: Optional[str] = None
    resume: Optional[str] = None
    telephone: Optional[str] = None
    ville: Optional[str] = None
    competences: list[CompetenceProfile] = Field(default_factory=list)
    experiences: list[ExperienceProfile] = Field(default_factory=list)
    formations: list[FormationProfile] = Field(default_factory=list)
    certifications: list[CertificationProfile] = Field(default_factory=list)
    projets: list[ProjetProfile] = Field(default_factory=list)


class MatchResult(BaseModel):
    """Résultat Agent 4 — Scores matching + ATS."""
    score_matching: int = Field(..., ge=0, le=100)
    score_ats: int = Field(..., ge=0, le=100)
    keywords_presents: list[str] = Field(default_factory=list)
    keywords_manquants: list[str] = Field(default_factory=list)
    recommandations: list[str] = Field(default_factory=list)
    competences_matching: list[str] = Field(default_factory=list)
    competences_manquantes: list[str] = Field(default_factory=list)


class PipelineResult(BaseModel):
    """Résultat complet du pipeline — retourné au backend .NET."""
    user_id: str
    analyzed_offer: Optional[dict] = None
    profile_data: Optional[dict] = None
    match_result: Optional[dict] = None
    cv_template_json: Optional[dict] = None
    email_draft: Optional[dict] = None
    errors: list[str] = Field(default_factory=list)
    pipeline_version: str = "2.0"
