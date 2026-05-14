from enum import Enum
from typing import List, Optional
from pydantic import BaseModel, Field, field_validator

class GapFlag(str, Enum):
    """Types de drapeaux d'adéquation CV/Offre."""
    PERFECT = "perfect_match"
    MINOR = "minor_gap"
    CRITICAL = "critical_gap"

class SkillGapInput(BaseModel):
    """
    Entrée pour l'analyse d'écart de compétences.
    """
    candidate_cv: dict = Field(..., description="Données structurées du CV")
    job_offer: dict = Field(..., description="Données structurées de l'offre")

class RecommendationType(str, Enum):
    COURSE = "course"
    CERTIFICATION = "certification"
    PROJECT = "project"
    CONTENT = "cv_content"

class Recommendation(BaseModel):
    """Une recommandation concrète pour combler un écart."""
    type: RecommendationType = Field(..., description="Type de recommandation")
    title: str = Field(..., description="Nom du cours, de la certif ou du projet")
    description: str = Field(..., description="Pourquoi c'est important et comment faire")
    priority: str = Field("medium", description="high, medium, low")

class SkillGapResult(BaseModel):
    """
    Résultat structuré et validé de l'analyse Skill Gap.
    """
    candidate_name: str = Field(..., description="Nom du candidat")
    job_title: str = Field(..., description="Titre de poste de l'offre")
    
    relevance_score: float = Field(..., ge=0.0, le=1.0, description="Score de pertinence global")
    
    matched_skills: List[str] = Field(default_factory=list)
    missing_skills: List[str] = Field(default_factory=list)
    required_certs: List[str] = Field(default_factory=list)
    
    cert_match: bool = Field(False)
    experience_years: float = Field(0.0, ge=0.0)
    required_years: float = Field(0.0, ge=0.0)
    experience_gap_years: float = Field(0.0, ge=0.0)
    
    flag: GapFlag = Field(..., description="Niveau de l'écart")
    
    # Nouvelles recommandations structurées
    recommendations: List[Recommendation] = Field(
        default_factory=list, 
        min_length=3,
        description="Actions concrètes (cours, certifs, projets) pour booster le CV"
    )
    
    # On garde revision_hints pour la compatibilité ou comme résumé
    revision_hints: List[str] = Field(default_factory=list, description="Conseils rapides")

    @field_validator('relevance_score')
    @classmethod
    def round_score(cls, v: float) -> float:
        return round(v, 2)

class SkillGapOutput(BaseModel):
    """
    Sortie finale du domaine Skill Gap.
    """
    skill_gap: Optional[SkillGapResult] = None
    errors: List[str] = Field(default_factory=list)
