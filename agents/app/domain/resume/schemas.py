from pydantic import BaseModel, Field, field_validator
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
    lienLinkedin: Optional[str] = Field(None, description="Lien vers le profil LinkedIn")
    lienGithub: Optional[str] = Field(None, description="Lien vers le profil GitHub")
    lienPortfolio: Optional[str] = Field(None, description="Lien vers le portfolio personnel")

class ExperienceSchema(BaseModel):
    entreprise: str = Field(description="Nom de l'entreprise")
    poste: str = Field(description="Titre du poste ou rôle")
    dateDebut: str = Field(description="Date de début au format YYYY-MM-DD")
    dateFin: Optional[str] = Field(None, description="Date de fin au format YYYY-MM-DD, ou null")
    missions: str = Field(description="Description textuelle des missions et tâches réalisées")
    ville: str = Field(description="Ville")
    type: str = Field(description="Type de contrat: Stage, Alternance, CDI, CDD, Freelance")
    taches: List[str] = Field(default_factory=list, description="Liste détaillée des tâches et responsabilités accomplies")

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
    taches: List[str] = Field(default_factory=list, description="Liste des tâches techniques et fonctionnelles réalisées sur ce projet")

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

FRENCH_LEVEL_MAP = {
    "maternelle": "Maternelle", "langue maternelle": "Maternelle", "langue-maternelle": "Maternelle",
    "natif": "Maternelle", "native": "Maternelle", "natif": "Maternelle",
    "bilingue": "Maternelle", "bilingual": "Maternelle",
    "courant": "Courant", "fluent": "Courant",
    "bonne maitrise": "Courant", "bonne maîtrise": "Courant",
    "lu ecrit parle": "Courant", "lu, ecrit, parle": "Courant",
    "lu écrit parlé": "Courant", "lu, écrit, parlé": "Courant",
    "intermediaire": "Intermédiaire", "intermédiaire": "Intermédiaire",
    "intermediate": "Intermédiaire", "scolaire": "Intermédiaire",
    "debutant": "Débutant", "débutant": "Débutant", "beginner": "Débutant",
    "notions": "Débutant",
    "elementaire": "Débutant", "élémentaire": "Débutant",
    "a1": "A1", "a2": "A2", "b1": "B1", "b2": "B2", "c1": "C1", "c2": "C2",
    "avance": "C1", "avancé": "C1", "advanced": "C1",
    "professionnel": "C2", "proficient": "C2",
}

def normalize_language_level(level: object) -> str:
    if level is None or not isinstance(level, str) or not level.strip():
        return "Intermédiaire"
    clean = level.strip().lower()
    return FRENCH_LEVEL_MAP.get(clean, level.strip())

class LanguageSchema(BaseModel):
    nom: str = Field(description="Nom de la langue (ex: Français, Anglais, Arabe, Espagnol)")
    niveau: str = Field(default="Intermédiaire", description="Niveau: CECRL (A1-C2) ou descripteur français (Courant, Intermédiaire, etc.)")

    @field_validator("niveau", mode="before")
    @classmethod
    def coerce_niveau(cls, v: object) -> str:
        if v is None or (isinstance(v, str) and not v.strip()):
            return "Intermédiaire"
        return str(v) if not isinstance(v, str) else v

class SkillSchema(BaseModel):
    nom: str = Field(description="Nom de la compétence (ex: Java, Docker, Python, Agile)")
    niveau: str = Field(description="Niveau: Debutant, Intermediaire, Avancé, ou Expert")
    typeCompetence: str = Field(description="STRICTEMENT ET UNIQUEMENT l'une de ces 2 valeurs: 'Technical', ou 'Soft Skill'")

class ResumeParsedSchema(BaseModel):
    personal: PersonalSchema
    experience: List[ExperienceSchema] = Field(default_factory=list)
    education: List[EducationSchema] = Field(default_factory=list)
    projects: List[ProjectSchema] = Field(default_factory=list)
    extracurricular: List[ExtracurricularSchema] = Field(default_factory=list)
    certifications: List[CertificationSchema] = Field(default_factory=list)
    skills: List[SkillSchema] = Field(default_factory=list)
    languages: List[LanguageSchema] = Field(default_factory=list)
