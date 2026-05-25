from typing import List, Literal
from pydantic import BaseModel, Field, field_validator


def _normalize_text(value):
    if value is None:
        return ""
    return str(value).strip()


class OptimizedExperience(BaseModel):
    """Une experience optimisee."""

    titre: str = Field(..., description="Intitule du poste (ne pas modifier le sens original)")
    entreprise: str = Field(default="", description="Nom de l'entreprise si disponible dans la source")
    description_optimisee: str = Field(
        ...,
        description="Description reecrite de maniere claire et naturelle pour presenter l'experience",
    )
    taches_optimisees: List[str] = Field(
        default_factory=list,
        description="Liste structuree de bullets optimises pour l'offre, rediges avec resultats, impact mesurable et chiffres quand c'est possible",
    )
    mots_cles_cibles: List[str] = Field(
        default_factory=list,
        description="Mots-cles de l'offre reellement utilises dans cette experience",
    )
    niveau_pertinence: Literal["high", "medium", "low"] = Field(
        default="medium",
        description="Niveau de pertinence de cette experience pour l'offre",
    )

    @field_validator("titre", "entreprise", "description_optimisee", mode="before")
    @classmethod
    def _normalize_text_fields(cls, value):
        return _normalize_text(value)


class OptimizedProject(BaseModel):
    """Un projet optimise."""

    titre: str = Field(..., description="Titre du projet")
    description_optimisee: str = Field(
        ...,
        description="Description reecrite de maniere claire et naturelle pour presenter le projet",
    )
    technologies: List[str] = Field(
        default_factory=list,
        description="Liste des technos mises en avant si pertinentes",
    )
    taches_optimisees: List[str] = Field(
        default_factory=list,
        description="Liste structuree de bullets optimises pour l'offre, rediges avec resultats, impact mesurable et chiffres quand c'est possible",
    )
    mots_cles_cibles: List[str] = Field(
        default_factory=list,
        description="Mots-cles de l'offre reellement utilises dans ce projet",
    )
    niveau_pertinence: Literal["high", "medium", "low"] = Field(
        default="medium",
        description="Niveau de pertinence de ce projet pour l'offre",
    )

    @field_validator("titre", "description_optimisee", mode="before")
    @classmethod
    def _normalize_project_text_fields(cls, value):
        return _normalize_text(value)


class OptimizedFormation(BaseModel):
    """Une formation optimisee (surtout reordonnee)."""

    diplome: str = Field(..., description="Nom du diplome")
    etablissement: str = Field(..., description="Nom de l'ecole ou universite")

    @field_validator("diplome", "etablissement", mode="before")
    @classmethod
    def _normalize_formation_fields(cls, value):
        return _normalize_text(value)


class OptimizedCertification(BaseModel):
    """Une certification optimisee (surtout reordonnee)."""

    nom: str = Field(..., description="Nom de la certification")
    organisme: str = Field(..., description="Organisme delivreur")

    @field_validator("nom", "organisme", mode="before")
    @classmethod
    def _normalize_certification_fields(cls, value):
        return _normalize_text(value)


class OptimizedSummary(BaseModel):
    """Resume du profil."""

    contenu: str = Field(..., description="Resume reecrit pour accrocher le recruteur")

    @field_validator("contenu", mode="before")
    @classmethod
    def _normalize_summary_field(cls, value):
        return _normalize_text(value)


class OptimizedCVOutput(BaseModel):
    """Sortie finale de l'agent d'optimisation."""

    resume_optimise: OptimizedSummary
    experiences_optimisees: List[OptimizedExperience] = Field(default_factory=list)
    projets_optimises: List[OptimizedProject] = Field(default_factory=list)
    formations_optimisees: List[OptimizedFormation] = Field(default_factory=list)
    certifications_optimisees: List[OptimizedCertification] = Field(default_factory=list)

    competences_reordonnees: List[str] = Field(
        default_factory=list,
        description="Liste des competences triees par pertinence pour l'offre"
    )
    competences_mises_en_avant: List[str] = Field(
        default_factory=list,
        description="Sous-ensemble des competences les plus pertinentes pour l'offre",
    )
