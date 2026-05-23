import logging

from fastapi import APIRouter, HTTPException

from app.domain.indeed_jobs.schemas.models import (
    IndeedJobSearchRequest,
    IndeedJobSearchResponse,
)
from app.domain.indeed_jobs.service import indeed_jobs_service

logger = logging.getLogger(__name__)
router = APIRouter(tags=["Indeed Jobs"])


@router.post(
    "/search",
    response_model=IndeedJobSearchResponse,
    summary="Scrape Indeed job offers with Scrapling",
    description=(
        "Collects Indeed search results, enriches each offer from the public "
        "job detail page, and filters the output to IT offers by default."
    ),
)
async def scrape_indeed_jobs(payload: IndeedJobSearchRequest) -> IndeedJobSearchResponse:
    logger.info("POST /indeed-jobs/search - keywords=%s", payload.keywords)
    try:
        result = await indeed_jobs_service.search_jobs(
            keywords=payload.keywords,
            location=payload.location,
            limit=payload.limit,
            posted_window=payload.posted_window,
            search_url=payload.search_url,
            fetch_details=payload.fetch_details,
            it_only=payload.it_only,
            country_code=payload.country_code,
            contract_types=payload.contract_types,
        )
        return IndeedJobSearchResponse(**result)
    except Exception as exc:
        logger.error("POST /indeed-jobs/search failed: %s", exc)
        raise HTTPException(status_code=500, detail=str(exc))
