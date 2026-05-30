# ============================================================
# app/domain/email_composer/schemas/models.py
#
# Pydantic request/response schemas for the Email Composer agent.
#
# Backward-compatible with email_engine/models.py.
# All existing fields preserved. New optional enrichment fields
# (skill_gap, company_intelligence) added to GenerateEmailRequest
# without breaking existing .NET backend calls.
# ============================================================
from typing import List, Optional, Dict, Any
from pydantic import BaseModel, Field


# ── Candidate & Offer inputs ──────────────────────────────────────────────────

from typing import List, Optional, Dict, Any, Union

class CandidateInput(BaseModel):
    full_name: str
    email: Optional[str] = None
    phone: Optional[str] = None
    current_title: Optional[str] = None
    skills: List[Union[str, Dict[str, Any]]] = Field(default_factory=list)
    experiences: List[Union[str, Dict[str, Any]]] = Field(default_factory=list)
    education: List[Union[str, Dict[str, Any]]] = Field(default_factory=list)
    projects: List[Union[str, Dict[str, Any]]] = Field(default_factory=list)
    certifications: List[Union[str, Dict[str, Any]]] = Field(default_factory=list)


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


# ── Optional enrichment from pipeline agents ─────────────────────────────────
# These are OPTIONAL. Legacy .NET calls omitting them will still work.

class SkillGapInput(BaseModel):
    """
    Subset of skill_gap agent output used to personalize the email.
    All fields are optional — if the pipeline did not run skill_gap,
    this entire object will be None.
    """
    matching_skills: List[str] = Field(default_factory=list)
    missing_skills: List[str] = Field(default_factory=list)
    important_missing_skills: List[str] = Field(default_factory=list)
    minor_missing_skills: List[str] = Field(default_factory=list)
    recommendations: List[str] = Field(default_factory=list)
    score: Optional[float] = None


class CompanyIntelligenceInput(BaseModel):
    """
    Subset of company agent output used to personalize the motivation
    paragraph. All fields optional — the agent uses only what is reliable.
    """
    summary: Optional[str] = None
    values: List[str] = Field(default_factory=list)
    products_or_services: List[str] = Field(default_factory=list)
    recent_context: Optional[str] = None
    recommendations: List[str] = Field(default_factory=list)
    score: Optional[int] = None


# ── Application email request/response ───────────────────────────────────────

class GenerateEmailRequest(BaseModel):
    candidature_id: str
    candidate: CandidateInput
    job_offer: JobOfferInput
    options: EmailOptions = Field(default_factory=EmailOptions)
    # Optional pipeline enrichment — absent from .NET calls, used by pipeline mode
    skill_gap: Optional[Dict[str, Any]] = None
    company_intelligence: Optional[Dict[str, Any]] = None


class GenerateEmailResponse(BaseModel):
    subject: str = Field(..., description="L'objet de l'email")
    body: str = Field(..., description="Le corps de l'email, structuré impérativement avec des sauts de ligne doubles entre les paragraphes pour la lisibilité.")
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


# ── Response classification models ───────────────────────────────────────────

class ClassifyResponseRequest(BaseModel):
    """Request payload for POST /email/classify-response."""
    candidature_id: str
    candidate_name: Optional[str] = None
    job_title: Optional[str] = None
    company_name: Optional[str] = None
    previous_email_subject: Optional[str] = None
    previous_email_body: Optional[str] = None
    reply_from: Optional[str] = None
    reply_date_utc: Optional[str] = None
    reply_subject: Optional[str] = None
    reply_snippet: str
    language: str = "fr"


class ClassifyResponseResult(BaseModel):
    """
    Result of LLM-based recruiter reply classification.

    response_type must be one of:
      ENTRETIEN_PROPOSE | INFORMATIONS_DEMANDEES | ACCEPTE | REFUSE |
      REPONSE_AUTOMATIQUE | REPONSE_GENERALE | INCONNU
    """
    response_type: str
    confidence: float
    summary: str
    recommended_action: str
    should_generate_reply_draft: bool = False


# ── Reply generation models ───────────────────────────────────────────────────

class RecruiterReplyInput(BaseModel):
    """Context about the recruiter's reply message."""
    from_email: Optional[str] = None
    subject: Optional[str] = None
    snippet: str
    received_at_utc: Optional[str] = None


class GenerateReplyEmailRequest(BaseModel):
    """Request payload for POST /email/generate-reply."""
    candidature_id: str
    candidate: CandidateInput
    job_offer: JobOfferInput
    previous_email: Optional[PreviousEmailInput] = None
    recruiter_reply: RecruiterReplyInput
    response_type: str
    response_summary: Optional[str] = None
    recommended_action: Optional[str] = None
    language: str = "fr"
    tone: str = "professionnel"
    user_instructions: Optional[str] = None
