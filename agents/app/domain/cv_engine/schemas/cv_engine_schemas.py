# ============================================================
# app/domain/cv_engine/schemas/cv_engine_schemas.py
# Modèles Pydantic du domaine CV_ENGINE
# ============================================================
from pydantic import BaseModel, Field
from typing import Optional


# ─── Requêtes API ─────────────────────────────────────────────

class CvEngineRequest(BaseModel):
    """Requête pour le pipeline CV Engine complet."""
    user_id: str = Field(..., description="Identifiant Keycloak de l'utilisateur")
    template_slug: str = Field(
        default="modern",
        description="Slug du template CV (modern, classic, executive, pro, elegant)",
    )
    offer_data: Optional[dict] = Field(
        default=None,
        description="Offre analysée par Agent 1 (optionnel — sans offre = CV générique)",
    )
    match_result: Optional[dict] = Field(
        default=None,
        description="Scores de matching Agent 4 (optionnel)",
    )


class SkillOptimizeRequest(BaseModel):
    """Requête pour l'optimisation des compétences uniquement."""
    user_id: str
    match_result: dict = Field(..., description="Scores et matching du domaine OFFER")


# ─── Sous-modèles du résultat ────────────────────────────────

class CvEntete(BaseModel):
    """Section en-tête du CV."""
    nom: str = ""
    prenom: str = ""
    titre: str = ""
    email: str = ""
    telephone: str = ""
    ville: str = ""
    pays: str = ""
    linkedin: str = ""
    github: str = ""
    portfolio: str = ""


class CvCompetence(BaseModel):
    """Compétence enrichie avec flag de matching."""
    nom: str
    niveau: int = 1
    type_competence: str = "Technical"
    matched: bool = False


class CvExperience(BaseModel):
    """Expérience professionnelle formatée."""
    titre: str = ""
    entreprise: str = ""
    date_debut: str = ""
    date_fin: str = ""
    description: str = ""
    type: str = ""


class CvFormation(BaseModel):
    """Formation / diplôme."""
    diplome: str = ""
    etablissement: str = ""
    annee: Optional[int] = None
    annee_fin: Optional[int] = None


class CvCertification(BaseModel):
    """Certification professionnelle."""
    nom: str = ""
    organisme: str = ""


class CvProjet(BaseModel):
    """Projet personnel / académique."""
    titre: str = ""
    description: str = ""
    technologies: str = ""


class CvSections(BaseModel):
    """Toutes les sections du CV structuré."""
    entete: CvEntete = CvEntete()
    resume: str = ""
    competences: list[CvCompetence] = []
    experiences: list[CvExperience] = []
    formations: list[CvFormation] = []
    certifications: list[CvCertification] = []
    projets: list[CvProjet] = []


class CvMetadata(BaseModel):
    """Métadonnées de génération du CV."""
    template_slug: str = "modern"
    score_ats: int = 0
    score_matching: int = 0
    ats_coverage_pct: float = 0.0
    poste_vise: str = ""
    entreprise: str = ""
    generated_at: str = ""


class AtsCoverage(BaseModel):
    """Couverture des mots-clés ATS."""
    covered: int = 0
    total: int = 0
    percentage: float = 0.0
    keywords_present: list[str] = []
    keywords_missing: list[str] = []


# ─── Résultats API ────────────────────────────────────────────

class CvEngineResult(BaseModel):
    """Résultat complet du pipeline CV Engine."""
    sections: CvSections = CvSections()
    metadata: CvMetadata = CvMetadata()
    ats_coverage: AtsCoverage = AtsCoverage()


class SkillOptimizeResult(BaseModel):
    """Résultat de l'optimisation des compétences seule."""
    skills: list[CvCompetence] = []
    ats_coverage: AtsCoverage = AtsCoverage()
