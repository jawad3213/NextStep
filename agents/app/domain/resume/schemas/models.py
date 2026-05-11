from pydantic import BaseModel, Field
from typing import List, Optional

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
