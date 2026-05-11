from pydantic import BaseModel, Field
from typing import List, Optional

class PersonalSchema(BaseModel):
    nom: str = Field(description="Nom de famille")
    prenom: str = Field(description="Prénom")
    email: str = Field(description="Adresse email")
    telephone: str = Field(description="Numéro de téléphone")
    ville: str = Field(description="Ville de résidence")
    pays: str = Field(description="Pays de résidence")
    titrePoste: str = Field(description="Titre de poste actuel ou recherché")
    resumeProfessionnel: str = Field(description="Court résumé présentant le profil")

class ExperienceSchema(BaseModel):
    entreprise: str = Field(description="Nom de l'entreprise")
    poste: str = Field(description="Titre du poste ou rôle")
    dateDebut: str = Field(description="Date de début au format YYYY-MM-DD")
    dateFin: Optional[str] = Field(None, description="Date de fin au format YYYY-MM-DD, ou null")
    missions: str = Field(description="Description textuelle des missions et tâches réalisées")
    ville: str = Field(description="Ville")
    type: str = Field(description="Type de contrat: Stage, Alternance, CDI, CDD, Freelance")

class EducationSchema(BaseModel):
    etablissement: str = Field(description="Nom de l'établissement d'enseignement")
    diplome: str = Field(description="Intitulé du diplôme")
    annee: str = Field(description="Année de début")
    anneeFin: str = Field(description="Année de fin ou Present")
    ville: str = Field(description="Ville")
    specialisation: str = Field(description="Domaine d'étude ou spécialisation")

class ProjectSchema(BaseModel):
    titre: str = Field(description="Nom du projet")
    description: str = Field(description="Description détaillée du projet")
    technologies: str = Field(description="Technologies utilisées, séparées par des virgules")
    lien: Optional[str] = Field(None, description="Lien de démo, GitHub, ou null")

class ExtracurricularSchema(BaseModel):
    titre: str = Field(description="Rôle ou titre de l'activité")
    organisation: str = Field(description="Nom de l'organisation ou de l'association")
    dateDebut: str = Field(description="Date de début (YYYY-MM-DD ou YYYY)")
    dateFin: Optional[str] = Field(None, description="Date de fin (YYYY-MM-DD, YYYY ou null)")
    description: str = Field(description="Description des activités et réalisations")

class CertificationSchema(BaseModel):
    titre: str = Field(description="Intitulé de la certification")
    organisation: str = Field(description="Organisme de certification")
    date: str = Field(description="Date d'obtention (YYYY-MM-DD ou YYYY)")
    lien: Optional[str] = Field(None, description="Lien de validation ou null")

class SkillSchema(BaseModel):
    nom: str = Field(description="Nom de la compétence (ex: Java, Docker, Anglais, Agile)")
    niveau: str = Field(description="Niveau: A1/A2/B1/B2/C1/C2/Native pour les langues; Debutant/Intermediaire/Avancé/Expert pour les compétences")
    typeCompetence: str = Field(description="STRICTEMENT ET UNIQUEMENT l'une de ces 3 valeurs: 'Technical', 'Soft Skill', ou 'Language'")

class ResumeParsedSchema(BaseModel):
    personal: PersonalSchema
    experience: List[ExperienceSchema] = Field(default_factory=list)
    education: List[EducationSchema] = Field(default_factory=list)
    projects: List[ProjectSchema] = Field(default_factory=list)
    extracurricular: List[ExtracurricularSchema] = Field(default_factory=list)
    certifications: List[CertificationSchema] = Field(default_factory=list)
    skills: List[SkillSchema] = Field(default_factory=list)
