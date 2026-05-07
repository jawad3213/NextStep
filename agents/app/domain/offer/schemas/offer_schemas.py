# ============================================================
# app/domain/offer/schemas/offer_schemas.py
#
# Modèles Pydantic du domaine OFFER :
#   - Entrées API (requêtes depuis le backend .NET)
#   - Sorties API (réponses vers le backend .NET)
#   - Sous-modèles de profil candidat
# ============================================================
from pydantic import BaseModel, Field
from typing import Optional


# ─────────────────────────────────────────────────────────────
# INPUT — Requêtes entrantes depuis le backend .NET
# ─────────────────────────────────────────────────────────────

class OfferInput(BaseModel):
    """Payload pour démarrer le pipeline complet."""
    raw_text: str = Field(
        ..., min_length=50,
        description="Texte brut de l'offre d'emploi (copié-collé)"
    )
    user_id: str = Field(
        ..., description="ID Keycloak de l'utilisateur connecté"
    )
    url: Optional[str] = Field(
        None, description="URL optionnelle de l'offre d'emploi"
    )
    template_id: int = Field(
        default=1, ge=1, le=3,
        description="Template CV : 1=Modern · 2=Classic · 3=Creative"
    )

    model_config = {"json_schema_extra": {
        "example": {
            "raw_text": "Nous recherchons un développeur Full-Stack Python/React...",
            "user_id": "550e8400-e29b-41d4-a716-446655440000",
            "template_id": 1,
        }
    }}


class MatchRequest(BaseModel):
    """Requête pour le scoring seul (Agents 2-3-4 sans Agent 1)."""
    user_id: str = Field(..., description="ID Keycloak de l'utilisateur")
    analyzed_offer: dict = Field(
        ..., description="JSON d'offre déjà analysé par Agent 1"
    )


# ─────────────────────────────────────────────────────────────
# SOUS-MODÈLES — Profil candidat (Agent 2)
# ─────────────────────────────────────────────────────────────

class CompetenceProfile(BaseModel):
    nom: str
    niveau: Optional[int] = Field(None, ge=1, le=5)


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
    technologies: list[str] = Field(default_factory=list)


# ─────────────────────────────────────────────────────────────
# OUTPUT — Réponses vers le backend .NET
# ─────────────────────────────────────────────────────────────

class AnalyzedOffer(BaseModel):
    """
    Résultat Agent 1 — JSON structuré de l'offre (produit par le LLM).

    Champs :
        titre               : Intitulé du poste
        entreprise          : Nom de l'entreprise (null si non mentionné)
        type_contrat        : CDI | CDD | Stage | Alternance | Freelance
        localisation        : Ville / pays / remote
        competences_requises: Compétences obligatoires extraites
        competences_souhaitees: Compétences optionnelles (« un plus »)
        keywords_ats        : 10-20 mots-clés pour le scoring ATS
        annees_experience   : Nombre d'années minimum (null si non précisé)
        niveau_etudes       : Bac+3 / Bac+5 / etc.
        description_poste   : Résumé en 2-3 phrases
    """
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
    """
    Résultat Agent 2 — Profil candidat chargé depuis PostgreSQL.

    Correspond à l'agrégat de :
        utilisateurs → profils → compétences | expériences | formations |
                                 certifications | projets
    """
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
    """
    Résultat Agent 4 — Scores de matching et ATS avec recommandations.

    score_matching : Jaccard pondéré 70% compétences requises + 30% souhaitées
    score_ats      : Pondération positionnelle (titre +5, résumé +3,
                     compétences +2, expériences +1) → base 100
    """
    score_matching: int = Field(..., ge=0, le=100)
    score_ats: int = Field(..., ge=0, le=100)
    keywords_presents: list[str] = Field(default_factory=list)
    keywords_manquants: list[str] = Field(default_factory=list)
    recommandations: list[str] = Field(default_factory=list)
    competences_matching: list[str] = Field(default_factory=list)
    competences_manquantes: list[str] = Field(default_factory=list)


class PipelineResult(BaseModel):
    """
    Résultat complet du pipeline multi-agent — retourné au backend .NET.

    Contient le résultat de chaque agent + les erreurs cumulées.
    pipeline_version permet de versionner le format JSON pour .NET.
    """
    user_id: str
    analyzed_offer: Optional[dict] = None
    """Agent 1 — Offre structurée"""
    profile_data: Optional[dict] = None
    """Agent 2 — Profil candidat"""
    match_result: Optional[dict] = None
    """Agent 4 — Scores matching + ATS"""
    cv_template_json: Optional[dict] = None
    """Agent 5 [stub M3] — Données pour QuestPDF"""
    email_draft: Optional[dict] = None
    """Agent 6 [stub M4] — Email de candidature"""
    messages: list[dict] = Field(default_factory=list)
    """Historique des actions effectuées par chaque agent"""
    errors: list[str] = Field(default_factory=list)
    """Liste des erreurs accumulées durant le pipeline"""
    pipeline_version: str = "2.1"
