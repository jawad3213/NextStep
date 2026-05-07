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
    skills: List[str] = Field(default_factory=list)
    experiences: List[str] = Field(default_factory=list)
    education: List[str] = Field(default_factory=list)
    projects: List[str] = Field(default_factory=list)
    certifications: List[str] = Field(default_factory=list)


class JobOfferInput(BaseModel):
    job_title: str
    company_name: Optional[str] = None
    location: Optional[str] = None
    required_skills: List[str] = Field(default_factory=list)
    preferred_skills: List[str] = Field(default_factory=list)
    missions: List[str] = Field(default_factory=list)
    requirements: List[str] = Field(default_factory=list)
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


# ── Follow-up / Relance models ────────────────────────────────────────────────

class PreviousEmailInput(BaseModel):
    """Context about the previously sent email that received no reply."""
    subject: str
    body: str
    sent_at_utc: Optional[str] = None


class FollowUpOptions(BaseModel):
    """Options for the follow-up email generation."""
    language: str = "fr"
    tone: str = "professionnel"
    days_since_sent: Optional[int] = None


class GenerateFollowUpEmailRequest(BaseModel):
    """Request payload for POST /email/generate-follow-up."""
    candidature_id: str
    candidate: CandidateInput
    job_offer: JobOfferInput
    previous_email: PreviousEmailInput
    options: FollowUpOptions = Field(default_factory=FollowUpOptions)
