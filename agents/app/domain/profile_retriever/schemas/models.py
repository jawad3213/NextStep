from typing import List, Optional
from pydantic import BaseModel, Field

# --- Sub-models for Profile components ---

class Skill(BaseModel):
    nom: str = Field(..., description="Nom de la compétence (ex: React, Python)")
    niveau: Optional[int] = Field(None, description="Niveau de maîtrise (1-5)")
    type_competence: Optional[str] = Field(None, description="Catégorie (Hard Skill, Soft Skill, Langue)")

class Experience(BaseModel):
    titre: str = Field(..., description="Intitulé du poste")
    entreprise: str = Field(..., description="Nom de l'entreprise")
    description: Optional[str] = Field(None, description="Description des missions et réalisations")
    date_debut: Optional[str] = Field(None, description="Date de début (ex: 2020-01-01)")
    date_fin: Optional[str] = Field(None, description="Date de fin ou 'Présent'")
    ville: Optional[str] = Field(None, description="Localisation de l'expérience")
    type: Optional[str] = Field(None, description="Type d'expérience (Professionnel, Académique, Extra-scolaire)")

class Formation(BaseModel):
    diplome: str = Field(..., description="Nom du diplôme ou de la certification")
    etablissement: str = Field(..., description="Nom de l'école ou université")
    annee: Optional[int] = Field(None, description="Année d'obtention ou de fin")
    ville: Optional[str] = Field(None, description="Ville de l'établissement")

class Certification(BaseModel):
    nom: str = Field(..., description="Nom de la certification")
    organisme: str = Field(..., description="Organisme délivreur (ex: AWS, Google)")
    date_obtention: Optional[str] = Field(None, description="Date d'obtention")

class Project(BaseModel):
    titre: str = Field(..., description="Titre du projet")
    description: Optional[str] = Field(None, description="Description du projet et des objectifs")
    technologies: List[str] = Field(default_factory=list, description="Liste des technos utilisées (ex: ['FastAPI', 'Docker'])")

# --- Main Domain Models ---

class UserProfile(BaseModel):
    """Représentation complète du profil utilisateur dans la base de données."""
    user_id: str = Field(..., description="Identifiant unique de l'utilisateur")
    nom: str = Field(..., description="Nom de famille")
    prenom: str = Field(..., description="Prénom")
    titre: Optional[str] = Field(None, description="Titre professionnel actuel (ex: Développeur Fullstack)")
    resume: Optional[str] = Field(None, description="Résumé ou biographie professionnelle")
    telephone: Optional[str] = Field(None, description="Numéro de téléphone")
    ville: Optional[str] = Field(None, description="Ville de résidence")
    
    competences: List[Skill] = Field(default_factory=list)
    experiences: List[Experience] = Field(default_factory=list)
    formations: List[Formation] = Field(default_factory=list)
    certifications: List[Certification] = Field(default_factory=list)
    projets: List[Project] = Field(default_factory=list)

class ProfileRetrieverInput(BaseModel):
    """Entrée nécessaire pour déclencher la récupération du profil."""
    user_id: str = Field(..., description="ID de l'utilisateur cible")

class ProfileRetrieverOutput(BaseModel):
    """Sortie structurée produite par l'agent Profile Retriever."""
    profile_data: UserProfile
    
