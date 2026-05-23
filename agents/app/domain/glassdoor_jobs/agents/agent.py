import logging

from app.domain.glassdoor_jobs.tools.scrapling_tool import (
    build_search_urls,
    enrich_jobs_with_details,
    fetch_listing_jobs,
    filter_jobs,
)

logger = logging.getLogger(__name__)


def _effective_fetch_limit(state: dict) -> int:
    requested_limit = max(1, int(state.get("limit", 20) or 20))
    overfetch_limit = max(requested_limit * 3, requested_limit + 20, 30)
    return min(overfetch_limit, 150)


async def build_search_urls_node(state: dict) -> dict:
    fetch_limit = _effective_fetch_limit(state)
    urls = build_search_urls(
        keywords=state.get("keywords"),
        location=state.get("location"),
        limit=fetch_limit,
        search_url=state.get("search_url"),
    )
    if not urls:
        return {"errors": ["No Glassdoor search URL could be built from the provided input."]}
    logger.info("Glassdoor jobs graph built %s search urls for fetch limit %s", len(urls), fetch_limit)
    return {"search_urls": urls, "fetch_limit": fetch_limit}


async def scrape_listing_jobs_node(state: dict) -> dict:
    fetch_limit = state.get("fetch_limit") or _effective_fetch_limit(state)
    jobs = await fetch_listing_jobs(
        search_urls=state.get("search_urls", []),
        limit=fetch_limit,
    )
    logger.info("Glassdoor jobs graph collected %s listing jobs from fetch limit %s", len(jobs), fetch_limit)
    return {"raw_jobs": jobs}


async def enrich_jobs_node(state: dict) -> dict:
    enriched = await enrich_jobs_with_details(
        jobs=state.get("raw_jobs", []),
        enabled=state.get("fetch_details", False),
    )
    logger.info("Glassdoor jobs graph enriched %s jobs", len(enriched))
    return {"enriched_jobs": enriched}


async def finalize_jobs_node(state: dict) -> dict:
    raw_jobs = state.get("raw_jobs", [])
    enriched_jobs = state.get("enriched_jobs", [])
    filtered = filter_jobs(
        jobs=enriched_jobs,
        it_only=state.get("it_only", True),
        limit=state.get("limit", 20),
        location=state.get("location"),
        posted_window=state.get("posted_window"),
        contract_types=state.get("contract_types", []),
    )
    errors = list(state.get("errors", []))
    errors.append("Glassdoor market routing is best-effort. Results may ignore the requested location.")
    if state.get("posted_window") and state.get("posted_window") != "any":
        errors.append("Glassdoor recency filtering is best-effort and inferred from public snippets when available.")
    return {
        "filtered_jobs": filtered,
        "result": {
            "keywords": state.get("keywords"),
            "location": state.get("location"),
            "total_found": len(raw_jobs),
            "total_returned": len(filtered),
            "it_only": state.get("it_only", True),
            "search_urls": state.get("search_urls", []),
            "jobs": filtered,
            "errors": errors,
        },
    }
