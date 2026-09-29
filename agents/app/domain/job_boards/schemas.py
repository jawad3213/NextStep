from typing import List, Optional

from pydantic import BaseModel, Field, model_validator


class JobSearchRequest(BaseModel):
    """Search parameters shared by every job board."""

    keywords: Optional[str] = Field(
        default=None,
        description="Job keywords, for example 'software engineer' or 'data analyst'.",
    )
    location: Optional[str] = Field(
        default=None,
        description="Optional location filter, for example 'Morocco' or 'Casablanca'.",
    )
    limit: int = Field(default=20, ge=1, le=100, description="Maximum number of jobs to return.")
    posted_window: Optional[str] = Field(
        default=None,
        description="Normalized recency bucket: 24h, 3d, 7d, 14d, 30d, or any.",
    )
    search_url: Optional[str] = Field(
        default=None,
        description="Optional full search URL of the board. When present it overrides generated search URLs.",
    )
    fetch_details: bool = Field(
        default=True,
        description="When true, fetch each job's detail page for richer metadata.",
    )
    it_only: bool = Field(
        default=True,
        description="When true, keep only job offers that match the built-in IT keyword heuristic.",
    )
    contract_types: List[str] = Field(
        default_factory=list,
        description="Optional normalized contract type filters, such as internship, cdi, cdd, or freelance.",
    )

    @model_validator(mode="after")
    def validate_input(self):
        if not self.search_url and not self.keywords:
            raise ValueError("Provide either keywords or search_url.")
        return self


class LinkedInJobSearchRequest(JobSearchRequest):
    posted_since_seconds: Optional[int] = Field(
        default=7 * 24 * 60 * 60,
        ge=3600,
        description="LinkedIn guest API recency filter in seconds.",
    )


class IndeedJobSearchRequest(JobSearchRequest):
    country_code: Optional[str] = Field(
        default="ma",
        description="Indeed market prefix, for example 'ma', 'fr', 'uk', or 'www'.",
    )


class GlassdoorJobSearchRequest(JobSearchRequest):
    fetch_details: bool = Field(
        default=False,
        description="Glassdoor detail fetching is limited. Disabled by default.",
    )


class JobOffer(BaseModel):
    job_id: Optional[str] = None
    title: str
    company: Optional[str] = None
    location: Optional[str] = None
    posted_at_text: Optional[str] = None
    url: Optional[str] = None
    description: Optional[str] = None
    employment_type: Optional[str] = None
    normalized_contract_type: Optional[str] = None
    seniority_level: Optional[str] = None
    job_function: Optional[str] = None
    industries: List[str] = Field(default_factory=list)
    source: str = ""
    search_url: Optional[str] = None
    is_it_offer: bool = False
    matched_it_terms: List[str] = Field(default_factory=list)


class JobSearchResponse(BaseModel):
    keywords: Optional[str] = None
    location: Optional[str] = None
    total_found: int = 0
    total_returned: int = 0
    it_only: bool = True
    search_urls: List[str] = Field(default_factory=list)
    jobs: List[JobOffer] = Field(default_factory=list)
    errors: List[str] = Field(default_factory=list)
