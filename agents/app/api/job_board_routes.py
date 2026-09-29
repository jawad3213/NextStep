# ============================================================
# app/api/job_board_routes.py
# POST /linkedin-jobs/search, /indeed-jobs/search, /glassdoor-jobs/search
# ============================================================
import logging

from fastapi import APIRouter, HTTPException

from app.domain.job_boards.schemas import (
    GlassdoorJobSearchRequest,
    IndeedJobSearchRequest,
    JobSearchRequest,
    JobSearchResponse,
    LinkedInJobSearchRequest,
)
from app.domain.job_boards.service import GLASSDOOR, INDEED, LINKEDIN, JobBoard, search_jobs

logger = logging.getLogger(__name__)


def _board_router(board: JobBoard, request_model: type[JobSearchRequest], path_name: str, description: str) -> APIRouter:
    router = APIRouter(tags=[f"{board.name} Jobs"])

    @router.post(
        "/search",
        response_model=JobSearchResponse,
        summary=f"Scrape {board.name} job offers with Scrapling",
        description=description,
    )
    async def search(payload: request_model) -> JobSearchResponse:  # type: ignore[valid-type]
        logger.info("POST /%s/search - keywords=%s", path_name, payload.keywords)
        try:
            return JobSearchResponse(**await search_jobs(board, payload))
        except Exception as exc:
            logger.error("POST /%s/search failed: %s", path_name, exc, exc_info=True)
            raise HTTPException(status_code=500, detail=f"Erreur lors de la recherche d'offres {board.name}.")

    return router


linkedin_router = _board_router(
    LINKEDIN, LinkedInJobSearchRequest, "linkedin-jobs",
    "Collects LinkedIn guest job search results, enriches each offer from the public job "
    "detail endpoint, and filters the output to IT offers by default.",
)
indeed_router = _board_router(
    INDEED, IndeedJobSearchRequest, "indeed-jobs",
    "Collects Indeed search results, enriches each offer from the public job detail page, "
    "and filters the output to IT offers by default.",
)
glassdoor_router = _board_router(
    GLASSDOOR, GlassdoorJobSearchRequest, "glassdoor-jobs",
    "Collects Glassdoor search results and filters them to IT offers by default. "
    "Location handling is best-effort because Glassdoor's public routing is inconsistent.",
)
