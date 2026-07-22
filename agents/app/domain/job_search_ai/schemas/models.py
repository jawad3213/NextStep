from typing import List, Optional

from pydantic import BaseModel, Field


class CandidateProfile(BaseModel):
    title: Optional[str] = None
    summary: Optional[str] = None
    location: Optional[str] = None
    country: Optional[str] = None
    level: Optional[str] = None
    sector: Optional[str] = None
    skills: List[str] = Field(default_factory=list)
    experience_titles: List[str] = Field(default_factory=list)
    experience_summaries: List[str] = Field(default_factory=list)
    project_titles: List[str] = Field(default_factory=list)
    project_technologies: List[str] = Field(default_factory=list)
    education: List[str] = Field(default_factory=list)
    target_keywords: List[str] = Field(default_factory=list)
    preferred_contract_types: List[str] = Field(default_factory=list)


class JobSearchOffer(BaseModel):
    id: str
    provider: str
    provider_job_id: Optional[str] = None
    external_url: Optional[str] = None
    title: str
    company: Optional[str] = None
    location: Optional[str] = None
    description: Optional[str] = None
    posted_at_text: Optional[str] = None
    posted_window: Optional[str] = None
    normalized_contract_type: Optional[str] = None
    employment_type: Optional[str] = None
    seniority_level: Optional[str] = None
    matched_it_terms: List[str] = Field(default_factory=list)


class RankedJobOffer(BaseModel):
    offer_id: str
    score_total: int = Field(ge=0, le=100)
    score_skills: int = Field(ge=0, le=50)
    score_title: int = Field(ge=0, le=20)
    score_location: int = Field(ge=0, le=10)
    score_contract: int = Field(ge=0, le=10)
    score_freshness: int = Field(ge=0, le=10)
    confidence: float = Field(ge=0.0, le=1.0)
    matched_skills: List[str] = Field(default_factory=list)
    missing_skills: List[str] = Field(default_factory=list)
    reasons: List[str] = Field(default_factory=list)
    summary: Optional[str] = None


class JobSearchRankRequest(BaseModel):
    profile: CandidateProfile
    offers: List[JobSearchOffer] = Field(default_factory=list)
    ai_refine_limit: int = Field(default=12, ge=0, le=30)


class JobSearchRankResponse(BaseModel):
    ranked_offers: List[RankedJobOffer] = Field(default_factory=list)
    warnings: List[str] = Field(default_factory=list)
