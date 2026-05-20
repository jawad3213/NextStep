from typing import List, Optional

from pydantic import BaseModel, Field, model_validator


class LinkedInJobSearchRequest(BaseModel):
    keywords: Optional[str] = Field(
        default=None,
        description="LinkedIn job keywords, for example 'software engineer' or 'data analyst'.",
    )
    location: Optional[str] = Field(
        default=None,
        description="Optional LinkedIn location filter, for example 'Morocco' or 'Remote'.",
    )
    limit: int = Field(default=20, ge=1, le=100, description="Maximum number of jobs to return.")
    posted_since_seconds: Optional[int] = Field(
        default=7 * 24 * 60 * 60,
        ge=3600,
        description="LinkedIn guest API recency filter in seconds.",
    )
    search_url: Optional[str] = Field(
        default=None,
        description="Optional full LinkedIn jobs search URL. When present it overrides generated search URLs.",
    )
    fetch_details: bool = Field(
        default=True,
        description="When true, fetch individual LinkedIn guest job detail pages for richer metadata.",
    )
    it_only: bool = Field(
        default=True,
        description="When true, keep only job offers that match the built-in IT keyword heuristic.",
    )

    @model_validator(mode="after")
    def validate_input(self):
        if not self.search_url and not self.keywords:
            raise ValueError("Provide either keywords or search_url.")
        return self


class LinkedInJobOffer(BaseModel):
    job_id: Optional[str] = None
    title: str
    company: Optional[str] = None
    location: Optional[str] = None
    posted_at_text: Optional[str] = None
    url: Optional[str] = None
    description: Optional[str] = None
    employment_type: Optional[str] = None
    seniority_level: Optional[str] = None
    job_function: Optional[str] = None
    industries: List[str] = Field(default_factory=list)
    source: str = "linkedin-guest-api"
    search_url: Optional[str] = None
    is_it_offer: bool = False
    matched_it_terms: List[str] = Field(default_factory=list)


class LinkedInJobSearchResponse(BaseModel):
    keywords: Optional[str] = None
    location: Optional[str] = None
    total_found: int = 0
    total_returned: int = 0
    it_only: bool = True
    search_urls: List[str] = Field(default_factory=list)
    jobs: List[LinkedInJobOffer] = Field(default_factory=list)
    errors: List[str] = Field(default_factory=list)
