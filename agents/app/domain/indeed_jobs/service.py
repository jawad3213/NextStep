import logging

from app.domain.indeed_jobs.graph.workflow import build_indeed_jobs_workflow
from app.domain.indeed_jobs.schemas.state import IndeedJobsState

logger = logging.getLogger(__name__)


class IndeedJobsService:
    def __init__(self):
        self.graph = build_indeed_jobs_workflow()

    async def search_jobs(
        self,
        keywords: str | None = None,
        location: str | None = None,
        limit: int = 20,
        search_url: str | None = None,
        fetch_details: bool = True,
        it_only: bool = True,
        country_code: str | None = "ma",
    ) -> dict:
        logger.info(
            "IndeedJobsService.search_jobs - keywords=%s location=%s limit=%s country=%s",
            keywords,
            location,
            limit,
            country_code,
        )
        initial_state: IndeedJobsState = {
            "keywords": keywords,
            "location": location,
            "limit": limit,
            "search_url": search_url,
            "fetch_details": fetch_details,
            "it_only": it_only,
            "country_code": country_code,
            "messages": [],
            "errors": [],
            "raw_jobs": [],
            "enriched_jobs": [],
            "filtered_jobs": [],
        }
        final_state = await self.graph.ainvoke(initial_state)
        return final_state.get("result") or {
            "keywords": keywords,
            "location": location,
            "total_found": 0,
            "total_returned": 0,
            "it_only": it_only,
            "search_urls": [],
            "jobs": [],
            "errors": final_state.get("errors", []),
        }


indeed_jobs_service = IndeedJobsService()
