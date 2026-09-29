# ============================================================
# app/domain/job_boards/service.py
# Job search on a board: build search URLs → fetch listings → fetch details → filter.
# ============================================================
import logging
from dataclasses import dataclass, field
from typing import Awaitable, Callable

from app.domain.job_boards import glassdoor, indeed, linkedin
from app.domain.job_boards.common import start_fetch_tracking
from app.domain.job_boards.schemas import JobSearchRequest

logger = logging.getLogger(__name__)

# Listings fetched before filtering (IT-only, contract, recency drop many of them).
_MAX_FETCH = 150
# Smallest batch of listings whose detail pages are fetched at once.
_MIN_DETAIL_BATCH = 10


@dataclass(frozen=True)
class JobBoard:
    name: str
    build_urls: Callable[[JobSearchRequest, int], list[str]]
    fetch_listings: Callable[[JobSearchRequest, list[str], int], Awaitable[list[dict]]]
    enrich: Callable[[list[dict], bool], Awaitable[list[dict]]]
    filter: Callable[[JobSearchRequest, list[dict]], list[dict]]
    warnings: Callable[[JobSearchRequest], list[str]] = field(default=lambda request: [])


def _fetch_limit(limit: int) -> int:
    requested = max(1, int(limit or 20))
    return min(max(requested * 3, requested + 20, 30), _MAX_FETCH)


LINKEDIN = JobBoard(
    name="LinkedIn",
    build_urls=lambda r, n: linkedin.build_search_urls(
        keywords=r.keywords, location=r.location, limit=n,
        posted_since_seconds=getattr(r, "posted_since_seconds", None),
        posted_window=r.posted_window, search_url=r.search_url,
    ),
    fetch_listings=lambda r, urls, n: linkedin.fetch_listing_jobs(search_urls=urls, limit=n),
    enrich=lambda jobs, enabled: linkedin.enrich_jobs_with_details(jobs=jobs, enabled=enabled),
    filter=lambda r, jobs: linkedin.filter_jobs(
        jobs=jobs, it_only=r.it_only, limit=r.limit, posted_window=r.posted_window, contract_types=r.contract_types,
    ),
)

INDEED = JobBoard(
    name="Indeed",
    build_urls=lambda r, n: indeed.build_search_urls(
        keywords=r.keywords, location=r.location, limit=n, search_url=r.search_url,
        country_code=getattr(r, "country_code", "ma"),
    ),
    fetch_listings=lambda r, urls, n: indeed.fetch_listing_jobs(
        search_urls=urls, limit=n, country_code=getattr(r, "country_code", "ma"),
    ),
    enrich=lambda jobs, enabled: indeed.enrich_jobs_with_details(jobs=jobs, enabled=enabled),
    filter=lambda r, jobs: indeed.filter_jobs(
        jobs=jobs, it_only=r.it_only, limit=r.limit, posted_window=r.posted_window, contract_types=r.contract_types,
    ),
)

GLASSDOOR = JobBoard(
    name="Glassdoor",
    build_urls=lambda r, n: glassdoor.build_search_urls(
        keywords=r.keywords, location=r.location, limit=n, search_url=r.search_url,
    ),
    fetch_listings=lambda r, urls, n: glassdoor.fetch_listing_jobs(
        search_urls=urls, limit=n, location=None if r.search_url else r.location,
    ),
    enrich=lambda jobs, enabled: glassdoor.enrich_jobs_with_details(jobs=jobs, enabled=enabled),
    filter=lambda r, jobs: glassdoor.filter_jobs(
        jobs=jobs, it_only=r.it_only, limit=r.limit, location=r.location,
        posted_window=r.posted_window, contract_types=r.contract_types,
    ),
    warnings=lambda r: [
        *(
            [f"Glassdoor does not recognise the location '{r.location}'; results may be from other places."]
            if r.location and not r.search_url and not glassdoor.location_was_resolved(r.location) else []
        ),
        *(
            ["Glassdoor recency filtering is best-effort and inferred from public snippets when available."]
            if r.posted_window and r.posted_window != "any" else []
        ),
    ],
)


async def search_jobs(board: JobBoard, request: JobSearchRequest) -> dict:
    """Search result (see JobSearchResponse)."""
    logger.info("%s jobs search - keywords=%s location=%s limit=%s", board.name, request.keywords, request.location, request.limit)
    result = {
        "keywords": request.keywords,
        "location": request.location,
        "total_found": 0,
        "total_returned": 0,
        "it_only": request.it_only,
        "search_urls": [],
        "jobs": [],
        "errors": [],
    }

    fetch_limit = _fetch_limit(request.limit)
    urls = board.build_urls(request, fetch_limit)
    if not urls:
        result["errors"].append(f"No {board.name} search URL could be built from the provided input.")
        return result

    fetch_issues = start_fetch_tracking()
    try:
        raw_jobs = await board.fetch_listings(request, urls, fetch_limit)
    except Exception as exc:
        logger.warning("%s listing fetch failed: %s", board.name, exc)
        result.update(search_urls=urls, errors=[_unavailable_message(board.name, fetch_issues, exc)])
        return result

    # Details are fetched batch by batch and stop once enough offers pass the filters:
    # a search for 10 offers no longer opens the detail page of every listing found.
    batch_size = max(_MIN_DETAIL_BATCH, request.limit * 2)
    enriched: list[dict] = []
    jobs: list[dict] = []
    for start in range(0, len(raw_jobs), batch_size):
        enriched += await board.enrich(raw_jobs[start:start + batch_size], request.fetch_details)
        jobs = board.filter(request, enriched)
        if len(jobs) >= request.limit:
            break
    logger.info("%s jobs search: %s listings, %s detailed, %s returned", board.name, len(raw_jobs), len(enriched), len(jobs))

    warnings = board.warnings(request)
    if not raw_jobs and fetch_issues:
        warnings = [_unavailable_message(board.name, fetch_issues), *warnings]

    result.update(
        total_found=len(raw_jobs),
        total_returned=len(jobs),
        search_urls=urls,
        jobs=jobs,
        errors=warnings,
    )
    return result


def _unavailable_message(board_name: str, issues: list[str], exc: Exception | None = None) -> str:
    reason = ", ".join(issues[:2]) if issues else (type(exc).__name__ if exc else "unknown error")
    return f"{board_name} returned no listings: {reason}. Showing cached offers for this provider."
