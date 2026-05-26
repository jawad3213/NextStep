import logging

from fastapi import APIRouter, HTTPException

from app.domain.glassdoor_jobs.schemas.models import (
    GlassdoorJobSearchRequest,
    GlassdoorJobSearchResponse,
)
from app.domain.glassdoor_jobs.service import glassdoor_jobs_service

logger = logging.getLogger(__name__)
router = APIRouter(tags=["Glassdoor Jobs"])


@router.post(
    "/search",
    response_model=GlassdoorJobSearchResponse,
    summary="Scrape Glassdoor job offers with Scrapling",
    description=(
        "Collects Glassdoor search results and filters them to IT offers by default. "
        "Location handling is best-effort because Glassdoor's public routing is inconsistent."
    ),
)
async def scrape_glassdoor_jobs(payload: GlassdoorJobSearchRequest) -> GlassdoorJobSearchResponse:
    logger.info("POST /glassdoor-jobs/search - keywords=%s", payload.keywords)
    try:
        result = await glassdoor_jobs_service.search_jobs(
            keywords=payload.keywords,
            location=payload.location,
            limit=payload.limit,
            posted_window=payload.posted_window,
            search_url=payload.search_url,
            fetch_details=payload.fetch_details,
            it_only=payload.it_only,
            contract_types=payload.contract_types,
        )
        return GlassdoorJobSearchResponse(**result)
    except Exception as exc:
        logger.error("POST /glassdoor-jobs/search failed: %s", exc)
        raise HTTPException(status_code=500, detail=str(exc))
