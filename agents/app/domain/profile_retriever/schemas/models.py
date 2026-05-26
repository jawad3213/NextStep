from typing import List, Optional
from pydantic import AliasChoices, BaseModel, Field, field_validator

# --- Sub-models for Profile components ---

class Skill(BaseModel):
    nom: str = Field(..., description="Nom de la competence (ex: React, Python)")
    niveau: Optional[int] = Field(None, description="Niveau de maitrise (1-5)")
    type_competence: Optional[str] = Field(None, description="Categorie (Hard Skill, Soft Skill, Langue)")

class Experience(BaseModel):
    titre: str = Field(..., description="Intitule du poste")
    entreprise: str = Field(..., description="Nom de l'entreprise")
    description: Optional[str] = Field(None, description="Description des missions et realisations")
    date_debut: Optional[str] = Field(None, description="Date de debut (ex: 2020-01-01)")
    date_fin: Optional[str] = Field(None, description="Date de fin ou Present")
    ville: Optional[str] = Field(None, description="Localisation de l'experience")
    type: Optional[str] = Field(None, description="Type d'experience (Professionnel, Academique, Extra-scolaire)")
    taches: List[str] = Field(
        default_factory=list,
        description="Liste des taches et responsabilites de l'experience",
        validation_alias=AliasChoices("taches", "tasks", "missions"),
    )

    @field_validator("taches", mode="before")
    @classmethod
    def _normalize_task_list(cls, value):
        if value is None:
            return []
        if isinstance(value, list):
            return [str(v).strip() for v in value if str(v).strip()]
        if isinstance(value, str):
            return [part.strip() for part in value.replace("\r", "\n").split("\n") if part.strip()] if "\n" in value else [part.strip() for part in value.split(",") if part.strip()]
        return [str(value).strip()] if str(value).strip() else []

class Formation(BaseModel):
    diplome: str = Field(..., description="Nom du diplome ou de la certification")
    etablissement: str = Field(..., description="Nom de l'ecole ou universite")
    annee: Optional[int] = Field(None, description="Annee d'obtention ou de fin")
    ville: Optional[str] = Field(None, description="Ville de l'etablissement")

class Certification(BaseModel):
    nom: str = Field(..., description="Nom de la certification")
    organisme: str = Field(..., description="Organisme delivreur (ex: AWS, Google)")
    date_obtention: Optional[str] = Field(None, description="Date d'obtention")

class Project(BaseModel):
    titre: str = Field(
        ...,
        description="Titre du projet",
        validation_alias=AliasChoices("titre", "title", "titreProjet"),
    )
    description: Optional[str] = Field(None, description="Description du projet et des objectifs")
    technologies: List[str] = Field(
        default_factory=list,
        description="Liste des technos utilisees",
        validation_alias=AliasChoices("technologies", "stack", "technologiesUtilisees"),
    )
    taches: List[str] = Field(
        default_factory=list,
        description="Liste des taches et responsabilites du projet",
        validation_alias=AliasChoices("taches", "tasks", "missions"),
    )

    @field_validator("technologies", "taches", mode="before")
    @classmethod
    def _normalize_list_fields(cls, value):
        if value is None:
            return []
        if isinstance(value, list):
            return [str(v).strip() for v in value if str(v).strip()]
        if isinstance(value, str):
            return [part.strip() for part in value.replace("\r", "\n").split("\n") if part.strip()] if "\n" in value else [part.strip() for part in value.split(",") if part.strip()]
        return [str(value).strip()] if str(value).strip() else []

class Activity(BaseModel):
    title: str = Field(..., description="Titre principal de l'activite")
    role: Optional[str] = Field(None, description="Organisation, evenement ou role secondaire")
    description: Optional[str] = Field(None, description="Ce qui a ete realise dans l'activite")
    date_debut: Optional[str] = Field(None, description="Date de debut")
    date_fin: Optional[str] = Field(None, description="Date de fin")

# --- Main Domain Models ---

class UserProfile(BaseModel):
    """Representation complete du profil utilisateur dans la base de donnees."""
    user_id: str = Field(..., description="Identifiant unique de l'utilisateur")
    nom: Optional[str] = Field(None, description="Nom de famille")
    prenom: Optional[str] = Field(None, description="Prenom")
    email: Optional[str] = Field(None, description="Adresse email")
    titre: Optional[str] = Field(None, description="Titre professionnel actuel")
    resume: Optional[str] = Field(None, description="Resume ou biographie professionnelle")
    telephone: Optional[str] = Field(None, description="Numero de telephone")
    ville: Optional[str] = Field(None, description="Ville de residence")
    photo_url: Optional[str] = Field(None, description="URL de la photo de profil")
    linkedin: Optional[str] = Field(None, description="Lien LinkedIn")
    github: Optional[str] = Field(None, description="Lien GitHub")
    portfolio: Optional[str] = Field(None, description="Lien Portfolio")

    competences: List[Skill] = Field(default_factory=list)
    experiences: List[Experience] = Field(default_factory=list)
    activities: List[Activity] = Field(default_factory=list)
    formations: List[Formation] = Field(default_factory=list)
    certifications: List[Certification] = Field(default_factory=list)
    projets: List[Project] = Field(default_factory=list)

class ProfileRetrieverInput(BaseModel):
    """Entree necessaire pour declencher la recuperation du profil."""
    user_id: str = Field(..., description="ID de l'utilisateur cible")

class ProfileRetrieverOutput(BaseModel):
    """Sortie structuree produite par l'agent Profile Retriever."""
    profile_data: UserProfile
