from typing import List, Optional

from pydantic import BaseModel, Field, model_validator


class IndeedJobSearchRequest(BaseModel):
    keywords: Optional[str] = Field(
        default=None,
        description="Indeed job keywords, for example 'software engineer' or 'data analyst'.",
    )
    location: Optional[str] = Field(
        default=None,
        description="Optional Indeed location filter, for example 'Morocco' or 'Casablanca'.",
    )
    limit: int = Field(default=20, ge=1, le=100, description="Maximum number of jobs to return.")
    search_url: Optional[str] = Field(
        default=None,
        description="Optional full Indeed search URL. When present it overrides generated search URLs.",
    )
    fetch_details: bool = Field(
        default=True,
        description="When true, fetch individual Indeed job detail pages for richer descriptions.",
    )
    it_only: bool = Field(
        default=True,
        description="When true, keep only job offers that match the built-in IT keyword heuristic.",
    )
    country_code: Optional[str] = Field(
        default="ma",
        description="Indeed market prefix, for example 'ma', 'fr', 'uk', or 'www'.",
    )

    @model_validator(mode="after")
    def validate_input(self):
        if not self.search_url and not self.keywords:
            raise ValueError("Provide either keywords or search_url.")
        return self


class IndeedJobOffer(BaseModel):
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
    source: str = "indeed-search"
    search_url: Optional[str] = None
    is_it_offer: bool = False
    matched_it_terms: List[str] = Field(default_factory=list)


class IndeedJobSearchResponse(BaseModel):
    keywords: Optional[str] = None
    location: Optional[str] = None
    total_found: int = 0
    total_returned: int = 0
    it_only: bool = True
    search_urls: List[str] = Field(default_factory=list)
    jobs: List[IndeedJobOffer] = Field(default_factory=list)
    errors: List[str] = Field(default_factory=list)
