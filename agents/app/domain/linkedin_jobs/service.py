import logging

from app.domain.linkedin_jobs.graph.workflow import build_linkedin_jobs_workflow
from app.domain.linkedin_jobs.schemas.state import LinkedInJobsState

logger = logging.getLogger(__name__)


class LinkedInJobsService:
    def __init__(self):
        self.graph = build_linkedin_jobs_workflow()

    async def search_jobs(
        self,
        keywords: str | None = None,
        location: str | None = None,
        limit: int = 20,
        posted_since_seconds: int | None = None,
        search_url: str | None = None,
        fetch_details: bool = True,
        it_only: bool = True,
    ) -> dict:
        logger.info(
            "LinkedInJobsService.search_jobs - keywords=%s location=%s limit=%s",
            keywords,
            location,
            limit,
        )
        initial_state: LinkedInJobsState = {
            "keywords": keywords,
            "location": location,
            "limit": limit,
            "posted_since_seconds": posted_since_seconds,
            "search_url": search_url,
            "fetch_details": fetch_details,
            "it_only": it_only,
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


linkedin_jobs_service = LinkedInJobsService()
