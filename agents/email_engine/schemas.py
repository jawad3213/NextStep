from pydantic import BaseModel, Field
from typing import List


class CandidateInput(BaseModel):
    full_name: str = Field(..., min_length=1)
    title: str = Field(..., min_length=1)
    skills: List[str] = []
    highlights: List[str] = []


class JobOfferInput(BaseModel):
    company_name: str = Field(..., min_length=1)
    job_title: str = Field(..., min_length=1)
    summary: str = Field(..., min_length=1)


class EmailGenerateRequest(BaseModel):
    email_type: str = "application"
    language: str = "fr"
    tone: str = "professional"
    candidate: CandidateInput
    job_offer: JobOfferInput


class EmailGenerateResponse(BaseModel):
    subject: str
    body: str
    detected_language: str