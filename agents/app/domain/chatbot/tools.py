import json, logging, httpx
from tavily import TavilyClient
from app.core.config import get_settings
from app.domain.chatbot.state import OfferContext, OfferData, CompanyData, MatchData

logger = logging.getLogger(__name__)


def get_tavily():
    return TavilyClient(api_key=get_settings().tavily_api_key)


async def search_interview_questions(company: str, job_title: str) -> list[str]:
    try:
        client = get_tavily()
        query = f'interview questions "{company}" "{job_title}" glassdoor 2024 2025'
        results = client.search(query=query, max_results=5)
        questions = []
        for r in results.get("results", []):
            lines = [l.strip() for l in r.get("content","").split("\n") if "?" in l and len(l.strip()) > 20]
            questions.extend(lines[:3])
        return questions[:10]
    except Exception as e:
        logger.error(f"search_interview_questions: {e}")
        return []


async def search_salary_data(job_title: str, location: str) -> dict:
    try:
        client = get_tavily()
        query = f'salary "{job_title}" "{location}" 2024 2025 average range'
        results = client.search(query=query, max_results=4)
        return {
            "job_title": job_title,
            "location": location,
            "raw_data": [r.get("content","")[:400] for r in results.get("results", [])],
        }
    except Exception as e:
        logger.error(f"search_salary_data: {e}")
        return {}


async def search_arena_questions(domain: str, level: str, focus: list[str]) -> list[str]:
    try:
        client = get_tavily()
        focus_str = ", ".join(focus) if focus else domain
        query = f'interview questions {domain} {level} {focus_str} 2024'
        results = client.search(query=query, max_results=4)
        questions = []
        for r in results.get("results", []):
            lines = [l.strip() for l in r.get("content","").split("\n") if "?" in l and len(l.strip()) > 20]
            questions.extend(lines[:3])
        return questions[:8]
    except Exception as e:
        logger.error(f"search_arena_questions: {e}")
        return []


async def get_offer_context(offer_id: str, user_id: str, token: str) -> OfferContext | None:
    if not offer_id:
        return None
    base = get_settings().backend_url
    headers = {"Authorization": f"Bearer {token}"}
    try:
        async with httpx.AsyncClient(timeout=8.0) as client:
            r2 = await client.get(f"{base}/api/offers/{offer_id}/analyzed", headers=headers)
            r3 = await client.get(f"{base}/api/offers/{offer_id}/company-intel", headers=headers)
            r4 = await client.get(f"{base}/api/offers/{offer_id}/match/{user_id}", headers=headers)
            o = r2.json() if r2.status_code == 200 else {}
            c = r3.json() if r3.status_code == 200 else {}
            m = r4.json() if r4.status_code == 200 else {}
            return OfferContext(
                offer=OfferData(
                    offer_id=offer_id,
                    job_title=o.get("titre_poste",""),
                    company_name=o.get("entreprise",""),
                    required_skills=o.get("competences_requises",[]),
                    ats_keywords=o.get("keywords_ats",[]),
                    tech_stack=o.get("stack_technique",[]),
                    experience_years=o.get("annees_experience",0),
                ),
                company=CompanyData(
                    company_name=c.get("nom_entreprise",""),
                    glassdoor_rating=c.get("note_glassdoor",0.0),
                    salary_min=c.get("salaire_min",0),
                    salary_max=c.get("salaire_max",0),
                    company_summary=c.get("resume_entreprise",""),
                    interview_difficulty=c.get("difficulte_entretien","medium"),
                    known_questions=c.get("questions_connues",[]),
                ),
                match=MatchData(
                    score_global=m.get("score_global",0),
                    missing_skills=m.get("competences_manquantes",[]),
                    strengths=m.get("points_forts",[]),
                ),
            )
    except Exception as e:
        logger.error(f"get_offer_context: {e}")
        return None
