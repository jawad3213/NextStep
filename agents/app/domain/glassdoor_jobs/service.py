import logging

from app.domain.glassdoor_jobs.graph.workflow import build_glassdoor_jobs_workflow
from app.domain.glassdoor_jobs.schemas.state import GlassdoorJobsState

logger = logging.getLogger(__name__)


class GlassdoorJobsService:
    def __init__(self):
        self.graph = build_glassdoor_jobs_workflow()

    async def search_jobs(
        self,
        keywords: str | None = None,
        location: str | None = None,
        limit: int = 20,
        posted_window: str | None = None,
        search_url: str | None = None,
        fetch_details: bool = False,
        it_only: bool = True,
        contract_types: list[str] | None = None,
    ) -> dict:
        logger.info(
            "GlassdoorJobsService.search_jobs - keywords=%s location=%s limit=%s",
            keywords,
            location,
            limit,
        )
        initial_state: GlassdoorJobsState = {
            "keywords": keywords,
            "location": location,
            "limit": limit,
            "posted_window": posted_window,
            "search_url": search_url,
            "fetch_details": fetch_details,
            "it_only": it_only,
            "contract_types": contract_types or [],
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


glassdoor_jobs_service = GlassdoorJobsService()
