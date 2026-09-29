"""Web searches used by the interview coach (Tavily).

The async client is used so a search never blocks the event loop: the agents serve
every other request while a search is in flight.
"""
import logging

from tavily import AsyncTavilyClient

from app.core.config import get_settings

logger = logging.getLogger(__name__)

_client: AsyncTavilyClient | None = None


def _get_tavily() -> AsyncTavilyClient | None:
    """Shared client, or None when no TAVILY_API_KEY is configured (searches are skipped)."""
    global _client
    api_key = get_settings().TAVILY_API_KEY
    if not api_key:
        return None
    if _client is None:
        _client = AsyncTavilyClient(api_key=api_key)
    return _client


async def _search(query: str, max_results: int) -> list[dict]:
    client = _get_tavily()
    if client is None:
        logger.info("Tavily search skipped (no TAVILY_API_KEY): %s", query)
        return []
    results = await client.search(query=query, max_results=max_results)
    return results.get("results", [])


def _question_lines(results: list[dict], per_result: int) -> list[str]:
    questions: list[str] = []
    for r in results:
        lines = [l.strip() for l in r.get("content", "").split("\n") if "?" in l and len(l.strip()) > 20]
        questions.extend(lines[:per_result])
    return questions


async def search_interview_questions(company: str, job_title: str) -> list[str]:
    try:
        query = f'interview questions "{company}" "{job_title}" glassdoor 2024 2025'
        return _question_lines(await _search(query, max_results=5), per_result=3)[:10]
    except Exception as e:
        logger.error(f"search_interview_questions: {e}")
        return []


async def search_salary_data(job_title: str, location: str) -> dict:
    try:
        query = f'salary "{job_title}" "{location}" glassdoor linkedin rekrute 2025 2026 average range'
        results = await _search(query, max_results=5)
        if not results:
            return {}
        return {
            "job_title": job_title,
            "location": location,
            "raw_data": [r.get("content", "")[:400] for r in results],
        }
    except Exception as e:
        logger.error(f"search_salary_data: {e}")
        return {}


async def search_arena_questions(domain: str, level: str, focus: list[str]) -> list[str]:
    try:
        focus_str = ", ".join(focus) if focus else domain
        query = f'interview questions {domain} {level} {focus_str} 2024'
        return _question_lines(await _search(query, max_results=4), per_result=3)[:8]
    except Exception as e:
        logger.error(f"search_arena_questions: {e}")
        return []
