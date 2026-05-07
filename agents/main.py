# ============================================================
# main.py — Point d'entrée FastAPI (Architecture Domain-Driven)
#
# Structure :
#   app/
#   ├── core/              — config.py, database.py (partagés)
#   ├── domain/
#   │   ├── offer_analyzer/
#   │   ├── profile_retriever/
#   │   ├── skill_gap/
#   │   ├── cv_optimizer/
#   │   ├── cv_engine/
#   │   └── company/       — Analyse entreprise + score culture
#   └── api/               — Routes FastAPI par domaine
#
# main.py                  ← CE FICHIER (monte les routers)
# ============================================================
import logging
from fastapi import FastAPI
from resume.router import router as resume_router
from fastapi.middleware.cors import CORSMiddleware

from app.api.offer_routes import router as offer_router
from app.api.company_routes import router as company_router
from app.api.cv_optimizer_routes import router as cv_optimizer_router
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
| **offer_analyzer** | 1 | Analyse de l'offre (LLM) |
| **profile_retriever**| 2 | Récupération du profil depuis BDD |
| **skill_gap**      | 3, 4 | Normalisation + Scoring ATS et Gap Analysis |
| **cv_optimizer**   | — | Optimisation et réécriture du CV (STAR) |
| **cv_engine**      | — | Formateur algorithmique pour QuestPDF JSON |
| **company**        | — | Analyse entreprise + score culture |
""",
    version="3.0.0",
    docs_url="/docs",
    redoc_url="/redoc",
    openapi_tags=[
        {"name": "Health"},
        {"name": "M2 — Offer Pipeline"},
        {"name": "CV Engine — Préparation données CV"},
        {"name": "Company — Analyse Entreprise"},
        {"name": "CV Optimizer"},
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
app.include_router(offer_router, prefix="/offer")
app.include_router(company_router, prefix="/company")
app.include_router(cv_optimizer_router)
app.include_router(cv_engine_router)


# ─── Router email_engine M4 (compatibilité) ───────────────────
if _email_router_available:
    app.include_router(email_router)
    logger.info("✅ email_engine router monté (M4 autonome)")
else:
    logger.info("ℹ️  email_engine non disponible")


# ─── Health check ─────────────────────────────────────────────
@app.get("/health", tags=["Health"], summary="Vérifier l'état du service")
async def health_check():
    """Retourne l'état du service et le statut de chaque agent."""
    return {
        "status": "ok",
        "service": "nextstep-agents",
        "version": "3.0.0",
        "architecture": "Domain-Driven + LangGraph StateGraph",
        "endpoints": {
            "pipeline":        "POST /offer/run-pipeline",
            "analyze_offer":   "POST /offer/analyze-offer",
            "match":           "POST /offer/match",
            "format_questpdf": "POST /cv-engine/format-questpdf",
            "analyze_company": "POST /company/analyze-company",
            "optimize_cv":     "POST /cv-optimizer/optimize",
        },
    }


logger.info("✅ NextStep Agents v3.0 démarré — Architecture Domain-Driven + LangGraph")
