# ============================================================
# main.py — Point d'entrée FastAPI (Architecture Domain-Driven)
#
# Structure :
#   app/
#   ├── core/              — config.py, database.py (partagés)
#   ├── domain/
#   │   ├── offer/         — Agents 1-4 + stubs 5-6 (M2)
#   │   │   ├── agents/    — offer_analyzer, profile_retriever, normalizer, scorer
#   │   │   ├── tools/     — db_tools.py (@tool LangChain)
#   │   │   ├── schemas/   — state.py, offer_schemas.py
#   │   │   ├── graph/     — workflow.py (LangGraph StateGraph)
#   │   │   └── service.py — OfferService (orchestration)
#   │   ├── job/           — Agent 5 (CV Formatter) + Agent 6 (Email Composer)
#   │   │   ├── agents/    — cv_formatter.py, email_composer.py
#   │   │   ├── schemas/   — state.py, job_schemas.py
#   │   │   └── service.py — JobService
#   │   └── company/       — Analyse entreprise + score culture
#   │       ├── agents/    — company_analyzer.py
#   │       ├── schemas/   — state.py, company_schemas.py
#   │       └── service.py — CompanyService
#   └── api/               — Routes FastAPI par domaine
#       ├── offer_routes.py
#       ├── job_routes.py
#       └── company_routes.py
#
# main.py                  ← CE FICHIER (monte les routers)
# ============================================================
import logging
from fastapi import FastAPI
from resume.router import router as resume_router
from fastapi.middleware.cors import CORSMiddleware

from app.api.offer_routes import router as offer_router
from app.api.job_routes import router as job_router
from app.api.company_routes import router as company_router
from app.api.cv_engine_routes import router as cv_engine_router

# Compatibilité : ancien email_engine (M4 autonome)
try:
    from email_engine.router import router as email_router
    _email_router_available = True
except ImportError:
    _email_router_available = False

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s | %(levelname)-8s | %(name)s — %(message)s",
)
logger = logging.getLogger(__name__)

# ─────────────────────────────────────────────────────────────
# Application FastAPI
# ─────────────────────────────────────────────────────────────
app = FastAPI(
    title="NextStep — Agents IA",
    description="""\
## Architecture Agents IA (Domain-Driven + LangGraph)

### Domaines
| Domaine | Agents | Description |
|---------|--------|-------------|
| **offer** | 1, 2, 3, 4 | Analyse offre → Profil → Normalisation → Scoring |
| **job**   | 5, 6       | CV Formatter → Email Composer |
| **cv_engine** | 7, 8, 9 | Profile Loader → Skill Optimizer → CV Structurer |
| **company** | —        | Analyse entreprise + score culture |

### Agents M2 (domaine offer)
- 🤖 **Agent 1** — Offer Analyzer (LLM Groq/OpenAI)
- 📊 **Agent 2** — Profile Retriever (PostgreSQL direct)
- 🔧 **Agent 3** — Normalizer (Algorithme pur)
- 📈 **Agent 4** — Scorer ATS + Matching (Algorithme pur)

### Agents M3/M4 (domaine job)
- 📄 **Agent 5** — CV Formatter (JSON pour QuestPDF)
- ✉️  **Agent 6** — Email Composer (LLM)
""",
    version="3.0.0",
    docs_url="/docs",
    redoc_url="/redoc",
    openapi_tags=[
        {"name": "Health"},
        {"name": "M2 — Offer Pipeline"},
        {"name": "Job — CV & Email"},
        {"name": "CV Engine — Préparation données CV"},
        {"name": "Company — Analyse Entreprise"},
        {"name": "Email Agent", "description": "Module M4 autonome"},
    ],
)

# Enregistrement des routes
app.include_router(resume_router, prefix="/resume", tags=["Resume Parsing"])
# ─── CORS ─────────────────────────────────────────────────────
app.add_middleware(
    CORSMiddleware,
    allow_origins=["http://localhost:4200", "http://localhost:5000"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# ─── Routers domaines ─────────────────────────────────────────
app.include_router(offer_router)
app.include_router(job_router)
app.include_router(cv_engine_router)
app.include_router(company_router)

# ─── Router email_engine M4 (compatibilité) ───────────────────
if _email_router_available:
    app.include_router(email_router)
    logger.info("✅ email_engine router monté (M4 autonome)")
else:
    logger.info("ℹ️  email_engine non disponible — utiliser /generate-email")


# ─── Health check ─────────────────────────────────────────────
@app.get("/health", tags=["Health"], summary="Vérifier l'état du service")
async def health_check():
    """Retourne l'état du service et le statut de chaque agent."""
    return {
        "status": "ok",
        "service": "nextstep-agents",
        "version": "3.0.0",
        "architecture": "Domain-Driven + LangGraph StateGraph",
        "domains": {
            "offer": {
                "Agent1_offer_analyzer":    "✅ actif (LLM)",
                "Agent2_profile_retriever": "✅ actif (DB SQL)",
                "Agent3_normalizer":        "✅ actif (algorithme)",
                "Agent4_scorer":            "✅ actif (algorithme)",
            },
            "job": {
                "Agent5_cv_formatter":  "✅ actif (algorithme)",
                "Agent6_email_composer":"✅ actif (LLM)",
            },
            "cv_engine": {
                "Node1_profile_loader":  "✅ actif (DB SQL)",
                "Node2_skill_optimizer": "✅ actif (algorithme)",
                "Node3_cv_structurer":   "✅ actif (algorithme)",
            },
            "company": {
                "company_analyzer": "✅ actif (algorithme)",
            },
        },
        "endpoints": {
            "pipeline":        "POST /run-pipeline",
            "analyze_offer":   "POST /analyze-offer",
            "match":           "POST /match",
            "prepare_cv":      "POST /prepare-cv",
            "prepare_cv_legacy": "POST /prepare-cv-data",
            "optimize_skills": "POST /optimize-skills",
            "generate_email":  "POST /generate-email",
            "analyze_company": "POST /analyze-company",
        },
    }


logger.info("✅ NextStep Agents v3.0 démarré — Architecture Domain-Driven + LangGraph")
