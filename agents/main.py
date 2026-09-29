# ============================================================
# main.py - FastAPI entrypoint for the agents service
# ============================================================
import hmac
import logging
from contextlib import asynccontextmanager

from fastapi import FastAPI, Request
from fastapi.responses import JSONResponse

from app.api.chatbot_routes import router as chatbot_router
<<<<<<< HEAD
from app.api.glassdoor_jobs_routes import router as glassdoor_jobs_router
from app.api.indeed_jobs_routes import router as indeed_jobs_router
from app.api.linkedin_jobs_routes import router as linkedin_jobs_router
from app.api.job_search_ai_routes import router as job_search_ai_router
from app.api.resume_routes import router as resume_router
=======
from app.api.company_routes import router as company_router
from app.api.cv_engine_routes import router as cv_engine_router
from app.api.job_board_routes import glassdoor_router, indeed_router, linkedin_router
from app.api.offer_routes import router as offer_router
from app.api.resume_routes import router as resume_router, summary_router
from app.api.sn_routes import router as sn_router
from app.core.config import settings
from app.core.schema_bootstrap import ensure_agent_runtime_schema
from app.core.error_handlers import register_exception_handlers
>>>>>>> chore/repo-cleanup

# Email Composer (M4)
try:
    from app.domain.email_composer.router import router as email_router

    _email_router_available = True
except ImportError:
    _email_router_available = False

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s | %(levelname)-8s | %(name)s - %(message)s",
)
logger = logging.getLogger(__name__)


@asynccontextmanager
async def lifespan(app: FastAPI):
    logger.info("Checking agent database schema...")
    try:
        await ensure_agent_runtime_schema()
    except Exception as e:
        logger.warning("Agent schema bootstrap skipped: %s", e)
    yield


app = FastAPI(
    lifespan=lifespan,
    title="NextStep - Agents IA",
    description="""\
## Architecture Agents IA (Domain-Driven + LangGraph)

### Domaines
| Domaine | Agents | Description |
|---------|--------|-------------|
| **offer_analyzer** | 1 | Analyse de l'offre (LLM) |
<<<<<<< HEAD
| **profile_retriever**| 2 | Récupération du profil depuis BDD |
| **skill_gap**      | 3, 4 | Normalisation + Scoring ATS et Gap Analysis |
| **cv_optimizer**      | — | Optimisation et réécriture du CV (STAR) |
| **cv_engine**         | — | Formateur algorithmique pour QuestPDF JSON |
| **company**           | — | Analyse entreprise + score culture |
| **job_search_ai**     | — | Ranking profil/offres apres sourcing Scrapling |
| **email_composer**    | M4 | Génération d'emails de candidature (pipeline + direct) |
=======
| **profile_retriever** | 2 | Recuperation du profil depuis BDD |
| **skill_gap** | 3, 4 | Normalisation + scoring ATS et gap analysis |
| **cv_optimizer** | - | Optimisation et reecriture du CV |
| **cv_engine** | - | Formateur algorithmique pour QuestPDF JSON |
| **company** | - | Analyse entreprise + score culture |
| **email_composer** | M4 | Generation d'emails de candidature |
>>>>>>> chore/repo-cleanup
""",
    version="3.0.0",
    docs_url="/docs",
    redoc_url="/redoc",
    openapi_tags=[
        {"name": "Health"},
        {"name": "M2 - Offer Pipeline"},
        {"name": "CV Engine - Preparation donnees CV"},
        {"name": "Company - Analyse Entreprise"},
        {"name": "CV Optimizer"},
        {"name": "Email Agent", "description": "Module M4 autonome"},
    ],
)

# Normalise toutes les erreurs (validation, HTTP, internes) vers un contrat
# commun sans fuite de détails internes.
register_exception_handlers(app)

app.include_router(resume_router, prefix="/resume", tags=["Resume Parsing"])
app.include_router(summary_router)

# Only the NextStep backend may call the agents: every request must carry the shared
# secret AGENTS_API_KEY. No CORS: browsers must never call this service directly.
_PUBLIC_PATHS = {"/health", "/docs", "/redoc", "/openapi.json"}
if not settings.AGENTS_API_KEY:
    logger.error("AGENTS_API_KEY is not set: every agents request will be rejected (401). Set it in .env.")


@app.middleware("http")
async def require_internal_api_key(request: Request, call_next):
    if request.url.path in _PUBLIC_PATHS:
        return await call_next(request)
    expected = settings.AGENTS_API_KEY
    provided = request.headers.get("X-Internal-Api-Key", "")
    if not expected or not hmac.compare_digest(provided.encode(), expected.encode()):
        return JSONResponse(status_code=401, content={"error": "Unauthorized", "type": "AgentAuthError"})
    return await call_next(request)

app.include_router(offer_router, prefix="/offer")
app.include_router(company_router, prefix="/company")
<<<<<<< HEAD
app.include_router(glassdoor_jobs_router, prefix="/glassdoor-jobs")
app.include_router(indeed_jobs_router, prefix="/indeed-jobs")
app.include_router(linkedin_jobs_router, prefix="/linkedin-jobs")
app.include_router(job_search_ai_router, prefix="/job-search-ai")
app.include_router(cv_optimizer_router)
=======
app.include_router(glassdoor_router, prefix="/glassdoor-jobs")
app.include_router(indeed_router, prefix="/indeed-jobs")
app.include_router(linkedin_router, prefix="/linkedin-jobs")
>>>>>>> chore/repo-cleanup
app.include_router(cv_engine_router)
app.include_router(chatbot_router)
app.include_router(sn_router)

if _email_router_available:
    app.include_router(email_router)
    logger.info("email_composer router mounted")
else:
    logger.warning("email_composer router unavailable - /email/* routes disabled")


@app.get("/health", tags=["Health"], summary="Verifier l'etat du service")
async def health_check():
    return {
        "status": "ok",
        "service": "nextstep-agents",
        "version": "3.0.0",
        "architecture": "Domain-Driven + LangGraph StateGraph",
        "endpoints": {
            "pipeline": "POST /offer/run-pipeline",
            "analyze_offer": "POST /offer/analyze-offer",
            "match": "POST /offer/match",
            "glassdoor_jobs": "POST /glassdoor-jobs/search",
            "indeed_jobs": "POST /indeed-jobs/search",
            "linkedin_jobs": "POST /linkedin-jobs/search",
<<<<<<< HEAD
            "job_search_rank": "POST /job-search-ai/rank",

            "format_questpdf": "POST /cv-engine/format-questpdf",
=======
>>>>>>> chore/repo-cleanup
            "analyze_company": "POST /company/analyze-company",
            "generate_email": "POST /email/generate",
            "generate_followup": "POST /email/generate-follow-up",
            "classify_response": "POST /email/classify-response",
            "generate_reply": "POST /email/generate-reply",
        },
    }


logger.info("NextStep Agents started - Domain-Driven + LangGraph")
