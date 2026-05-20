import logging

from app.domain.indeed_jobs.tools.scrapling_tool import (
    build_search_urls,
    enrich_jobs_with_details,
    fetch_listing_jobs,
    filter_jobs,
)

logger = logging.getLogger(__name__)


async def build_search_urls_node(state: dict) -> dict:
    urls = build_search_urls(
        keywords=state.get("keywords"),
        location=state.get("location"),
        limit=state.get("limit", 20),
        search_url=state.get("search_url"),
        country_code=state.get("country_code", "ma"),
    )
    if not urls:
        return {"errors": ["No Indeed search URL could be built from the provided input."]}
    logger.info("Indeed jobs graph built %s search urls", len(urls))
    return {"search_urls": urls}


async def scrape_listing_jobs_node(state: dict) -> dict:
    jobs = await fetch_listing_jobs(
        search_urls=state.get("search_urls", []),
        limit=state.get("limit", 20),
        country_code=state.get("country_code", "ma"),
    )
    logger.info("Indeed jobs graph collected %s listing jobs", len(jobs))
    return {"raw_jobs": jobs}


async def enrich_jobs_node(state: dict) -> dict:
    enriched = await enrich_jobs_with_details(
        jobs=state.get("raw_jobs", []),
        enabled=state.get("fetch_details", True),
    )
    logger.info("Indeed jobs graph enriched %s jobs", len(enriched))
    return {"enriched_jobs": enriched}


async def finalize_jobs_node(state: dict) -> dict:
    raw_jobs = state.get("raw_jobs", [])
    enriched_jobs = state.get("enriched_jobs", [])
    filtered = filter_jobs(
        jobs=enriched_jobs,
        it_only=state.get("it_only", True),
        limit=state.get("limit", 20),
    )
    logger.info("Indeed jobs graph returning %s jobs after filtering", len(filtered))
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
            "errors": state.get("errors", []),
        },
    }
