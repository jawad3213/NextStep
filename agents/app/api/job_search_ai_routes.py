from fastapi import APIRouter, HTTPException

from app.domain.job_search_ai.schemas.models import JobSearchRankRequest, JobSearchRankResponse
from app.domain.job_search_ai.service import job_search_ai_service

router = APIRouter(tags=["Job Search AI"])


@router.post(
    "/rank",
    response_model=JobSearchRankResponse,
    summary="Rank sourced job offers against a candidate profile",
)
async def rank_job_search(payload: JobSearchRankRequest) -> JobSearchRankResponse:
    try:
        return await job_search_ai_service.rank(payload)
    except Exception as exc:
        raise HTTPException(status_code=500, detail=f"Job search ranking failed: {exc}") from exc
