import re
from typing import List, Optional

from pydantic import BaseModel, Field, field_validator


class _LenientModel(BaseModel):
    """
    Every field is optional: a CV that lacks a phone number or a city must still
    validate (empty value) instead of failing and falling back to unchecked data.
    """

    @field_validator("*", mode="before")
    @classmethod
    def _coerce_scalars(cls, value: object, info) -> object:
        annotation = cls.model_fields[info.field_name].annotation
        if annotation is str:
            if value is None:
                return ""
            if isinstance(value, (int, float)):
                return str(value)
        return value


class PersonalSchema(_LenientModel):
    nom: str = ""
    prenom: str = ""
    email: str = ""
    telephone: str = ""
    ville: str = ""
    pays: str = ""
    titrePoste: str = ""
    resumeProfessionnel: str = ""
    lienLinkedin: Optional[str] = None
    lienGithub: Optional[str] = None
    lienPortfolio: Optional[str] = None


class ExperienceSchema(_LenientModel):
    entreprise: str = ""
    poste: str = ""
    dateDebut: Optional[str] = None
    dateFin: Optional[str] = None
    missions: str = ""
    ville: str = ""
    type: str = ""
    taches: List[str] = Field(default_factory=list)


class EducationSchema(_LenientModel):
    etablissement: str = ""
    diplome: str = ""
    annee: str = ""
    anneeFin: str = ""
    ville: str = ""
    specialisation: str = ""


class ProjectSchema(_LenientModel):
    titre: str = ""
    description: str = ""
    technologies: str = ""
    lien: Optional[str] = None
    taches: List[str] = Field(default_factory=list)


class ExtracurricularSchema(_LenientModel):
    titre: str = ""
    organisation: str = ""
    dateDebut: Optional[str] = None
    dateFin: Optional[str] = None
    description: str = ""


class CertificationSchema(_LenientModel):
    titre: str = ""
    organisation: str = ""
    date: str = ""
    lien: Optional[str] = None


class SkillSchema(_LenientModel):
    nom: str = ""
    niveau: str = ""
    typeCompetence: str = ""


class LanguageSchema(_LenientModel):
    nom: str = ""
    niveau: str = ""


class ResumeParsedSchema(_LenientModel):
    personal: PersonalSchema = Field(default_factory=PersonalSchema)
    experience: List[ExperienceSchema] = Field(default_factory=list)
    education: List[EducationSchema] = Field(default_factory=list)
    projects: List[ProjectSchema] = Field(default_factory=list)
    extracurricular: List[ExtracurricularSchema] = Field(default_factory=list)
    certifications: List[CertificationSchema] = Field(default_factory=list)
    skills: List[SkillSchema] = Field(default_factory=list)
    languages: List[LanguageSchema] = Field(default_factory=list)


FRENCH_LEVEL_MAP = {
    "maternelle": "Maternelle", "maternel": "Maternelle",
    "langue maternelle": "Maternelle", "langue-maternelle": "Maternelle",
    "natif": "Maternelle", "native": "Maternelle", "bilingue": "Maternelle",
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
    """Normalizes a language level written in the CV. Missing level stays empty (never invented)."""
    if level is None or not isinstance(level, str) or not level.strip():
        return ""
    clean = re.sub(r'\s*\(.*?\)\s*', '', level.strip()).lower().strip()
    return FRENCH_LEVEL_MAP.get(clean, level.strip())
