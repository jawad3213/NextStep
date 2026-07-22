from app.domain.job_search_ai.agents.ranker import rank_job_search_offers
from app.domain.job_search_ai.schemas.models import JobSearchRankRequest, JobSearchRankResponse


class JobSearchAIService:
    async def rank(self, request: JobSearchRankRequest) -> JobSearchRankResponse:
        return await rank_job_search_offers(request)


job_search_ai_service = JobSearchAIService()
