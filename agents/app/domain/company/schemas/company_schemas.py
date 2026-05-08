# ============================================================
# app/domain/company/schemas/company_schemas.py
# Modèles Pydantic du domaine COMPANY
# ============================================================
from pydantic import BaseModel, Field
from typing import Optional


class SalaryInfo(BaseModel):
    """Informations sur les salaires."""
    job_title: str
    location: Optional[str] = None
    min_salary: Optional[int] = None
    max_salary: Optional[int] = None
    avg_salary: Optional[int] = None
    currency: str = "EUR"
    source: str = "Glassdoor/SalaryData"


class CultureInsights(BaseModel):
    """Détails sur la culture d'entreprise."""
    culture_score: int = Field(ge=0, le=100)
    turnover_rate: Optional[str] = None # low | medium | high
    work_life_balance: Optional[float] = None # 0-5
    glassdoor_rating: Optional[float] = None # 0-5
    key_values: list[str] = Field(default_factory=list)
    top_reviews: list[str] = Field(default_factory=list)

class CompanyIntelligence(BaseModel):
    """Fiche complète d'intelligence sur l'entreprise."""
    nom: str
    summary: str = Field(..., description="Résumé global généré par l'agent")
    sector: str
    hq_location: Optional[str] = None
    linkedin_url: Optional[str] = None
    
    # Intelligence Data
    culture: CultureInsights
    salaries: list[SalaryInfo] = Field(default_factory=list)
    actualites: list[str] = Field(default_factory=list)
    
    # Interview Insights
    interview_difficulty: Optional[str] = "medium"  # easy | medium | hard
    interview_questions: list[str] = Field(default_factory=list)
    
    # Analysis
    pros: list[str] = Field(default_factory=list)
    cons: list[str] = Field(default_factory=list)
    career_opportunities: Optional[str] = None

class CompanyAnalysisResult(BaseModel):
    """Résultat complet de l'analyse entreprise (Output final)."""
    intelligence: CompanyIntelligence
    compatibility_score: int = Field(ge=0, le=100)
    recommendations: list[str] = Field(default_factory=list)


class SkillGapResult(BaseModel):
    """Analyse de l'écart de compétences (Skill Gap) entre un candidat et une offre."""
    candidate_name: str
    job_title: str
    relevance_score: float = Field(..., description="Score de pertinence de 0.0 à 1.0")
    matched_skills: list[str] = Field(default_factory=list, description="Compétences communes du candidat et de l'offre")
    missing_skills: list[str] = Field(default_factory=list, description="Compétences requises par l'offre mais absentes du CV")
    required_certs: list[str] = Field(default_factory=list, description="Certifications exigées ou recommandées par l'offre")
    cert_match: bool = Field(..., description="Vrai si le candidat possède les certifications requises")
    experience_years: float = Field(..., description="Années d'expérience du candidat")
    required_years: float = Field(..., description="Années d'expérience requises par l'offre")
    experience_gap_years: float = Field(..., description="Écart d'années d'expérience")
    flag: str = Field(..., description="Niveau d'alerte, ex: perfect_match, minor_gap, critical_gap")
    revision_hints: list[str] = Field(default_factory=list, description="Suggestions concrètes pour améliorer le CV face à l'offre")


class CompanyAnalyzeRequest(BaseModel):
    """Requête pour l'analyse d'une entreprise."""
    company_name: str
    offer_data: dict
    profile_data: dict
    user_id: int

