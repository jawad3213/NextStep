# ============================================================
# email_engine/models.py — Pydantic schemas for the Email Agent
# ============================================================
from typing import List, Optional, Dict, Any
from pydantic import BaseModel, Field


class CandidateInput(BaseModel):
    full_name: str
    email: Optional[str] = None
    phone: Optional[str] = None
    current_title: Optional[str] = None
    skills: List[str] = []
    experiences: List[str] = []
    education: List[str] = []
    projects: List[str] = []
    certifications: List[str] = []


class JobOfferInput(BaseModel):
    job_title: str
    company_name: Optional[str] = None
    location: Optional[str] = None
    required_skills: List[str] = []
    preferred_skills: List[str] = []
    missions: List[str] = []
    requirements: List[str] = []
    raw_text: Optional[str] = None
    analysis_json: Optional[Dict[str, Any]] = None


class EmailOptions(BaseModel):
    language: str = "fr"
    tone: str = "professionnel"
    include_motivation_letter: bool = False


class GenerateEmailRequest(BaseModel):
    candidature_id: str
    candidate: CandidateInput
    job_offer: JobOfferInput
    options: EmailOptions = Field(default_factory=EmailOptions)


class GenerateEmailResponse(BaseModel):
    subject: str
    body: str
    language: str
    tone: str
