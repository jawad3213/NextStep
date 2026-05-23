import logging

from fastapi import APIRouter, HTTPException

from app.domain.linkedin_jobs.schemas.models import (
    LinkedInJobSearchRequest,
    LinkedInJobSearchResponse,
)
from app.domain.linkedin_jobs.service import linkedin_jobs_service

logger = logging.getLogger(__name__)
router = APIRouter(tags=["LinkedIn Jobs"])


@router.post(
    "/search",
    response_model=LinkedInJobSearchResponse,
    summary="Scrape LinkedIn guest job offers with Scrapling",
    description=(
        "Collects LinkedIn guest job search results, enriches each offer from the "
        "public job detail endpoint, and filters the output to IT offers by default."
    ),
)
async def scrape_linkedin_jobs(payload: LinkedInJobSearchRequest) -> LinkedInJobSearchResponse:
    logger.info("POST /linkedin-jobs/search - keywords=%s", payload.keywords)
    try:
        result = await linkedin_jobs_service.search_jobs(
            keywords=payload.keywords,
            location=payload.location,
            limit=payload.limit,
            posted_since_seconds=payload.posted_since_seconds,
            posted_window=payload.posted_window,
            search_url=payload.search_url,
            fetch_details=payload.fetch_details,
            it_only=payload.it_only,
            contract_types=payload.contract_types,
        )
        return LinkedInJobSearchResponse(**result)
    except Exception as exc:
        logger.error("POST /linkedin-jobs/search failed: %s", exc)
        raise HTTPException(status_code=500, detail=str(exc))
