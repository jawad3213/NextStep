# ============================================================
# main.py — Point d'entrée FastAPI
#
# Structure imposée :
#   agents/
#   ├── app/
#   │   ├── core/      — config.py, database.py
#   │   ├── schemas/   — state.py, api_schemas.py
#   │   ├── tools/     — backend_api.py (@tool)
#   │   ├── agents/    — offer_analyzer, profile_retriever, normalizer, scorer
#   │   └── graphs/    — main_workflow.py (LangGraph StateGraph)
#   ├── main.py        ← CE FICHIER
#   └── requirements.txt
# ============================================================
import logging
from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware

from app.schemas.api_schemas import OfferInput, AnalyzedOffer, MatchResult, PipelineResult
from app.graphs.main_workflow import get_workflow
from app.schemas.state import AgentState

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s | %(levelname)-8s | %(name)s — %(message)s",
)
logger = logging.getLogger(__name__)

# ─────────────────────────────────────────────────────
# Application FastAPI
# ─────────────────────────────────────────────────────
app = FastAPI(
    title="NextStep — Agents IA",
    description="""
## Architecture Agents IA (LangGraph)

**Flux d'exécution :**
1. FastAPI reçoit la requête
2. LangGraph est initialisé avec l'état initial
3. **Router** décide quel agent appeler (arête conditionnelle)
4. Chaque agent met à jour le state et retourne au Router
5. Quand tout est fait → END → réponse retournée au backend .NET

**Agents M2 :**
- 🤖 **Agent 1** — Offer Analyzer (LLM Groq/OpenAI)
- 📊 **Agent 2** — Profile Retriever (PostgreSQL)
- 🔧 **Agent 3** — Normalizer (Algorithme)
- 📈 **Agent 4** — Scorer (ATS + Matching)

**Stubs :**
- 📄 Agent 5 — CV Formatter (M3)
- ✉️  Agent 6 — Email Composer (M4)
    """,
    version="2.0.0",
    docs_url="/docs",
    redoc_url="/redoc",
)

# ─── CORS ───
app.add_middleware(
    CORSMiddleware,
    allow_origins=["http://localhost:4200", "http://localhost:5000"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


# ─────────────────────────────────────────────────────
# ENDPOINTS
# ─────────────────────────────────────────────────────

@app.get("/health", tags=["Health"])
async def health_check():
    """Health check — vérifie que le service est opérationnel."""
    return {
        "status": "ok",
        "service": "nextstep-agents",
        "version": "2.0.0",
        "architecture": "LangGraph StateGraph + Conditional Edges",
        "agents": {
            "M2_offer_analyzer":    "✅ actif",
            "M2_profile_retriever": "✅ actif",
            "M2_normalizer":        "✅ actif",
            "M2_scorer":            "✅ actif",
            "M3_cv_formatter":      "⏳ stub",
            "M4_email_composer":    "⏳ stub",
        },
    }


@app.post(
    "/run-pipeline",
    response_model=PipelineResult,
    tags=["Pipeline"],
    summary="Lancer le pipeline complet (6 agents via LangGraph)",
    description=(
        "Reçoit une offre brute (texte) + user_id + template_id. "
        "Orchestre les 6 agents via LangGraph StateGraph. "
        "Retourne le résultat complet (analyse, profil, scores, CV JSON, email)."
    ),
)
async def run_pipeline(payload: OfferInput) -> PipelineResult:
    """
    POST /run-pipeline — Appelé par le backend .NET.

    1. Initialise l'AgentState avec les données d'entrée
    2. Invoque le graphe LangGraph (Router + 6 agents)
    3. Retourne le state final
    """
    logger.info("POST /run-pipeline — user_id=%s | template=%d", payload.user_id, payload.template_id)

    workflow = get_workflow()

    # ─── État initial ───
    initial_state: AgentState = {
        "raw_offer_text": payload.raw_text,
        "user_id": payload.user_id,
        "template_id": payload.template_id,
        "messages": [],
        "errors": [],
        "pipeline_version": "2.0",
        # Champs optionnels — None par défaut
        "analyzed_offer": None,
        "profile_data": None,
        "normalized_offer_skills": [],
        "normalized_profile_skills": [],
        "normalized_keywords": [],
        "profile_full_text": "",
        "match_result": None,
        "cv_template_json": None,
        "email_draft": None,
        "next_agent": None,
    }

    try:
        # ─── Exécution du graphe LangGraph ───
        final_state: AgentState = await workflow.ainvoke(initial_state)

        logger.info(
            "POST /run-pipeline ✅ — user_id=%s | erreurs=%d | messages=%d",
            payload.user_id,
            len(final_state.get("errors") or []),
            len(final_state.get("messages") or []),
        )

        return PipelineResult(
            user_id=payload.user_id,
            analyzed_offer=final_state.get("analyzed_offer"),
            profile_data=final_state.get("profile_data"),
            match_result=final_state.get("match_result"),
            cv_template_json=final_state.get("cv_template_json"),
            email_draft=final_state.get("email_draft"),
            errors=final_state.get("errors") or [],
        )

    except Exception as e:
        logger.error("POST /run-pipeline ❌ — Erreur fatale : %s", str(e))
        raise HTTPException(status_code=500, detail=f"Erreur pipeline : {str(e)}")


@app.post(
    "/analyze-offer",
    response_model=dict,
    tags=["M2 — AI Agent"],
    summary="Agent 1 uniquement — Analyser une offre via LLM",
    description="Lance uniquement Agent 1 (LLM) sans le pipeline complet.",
)
async def analyze_offer(payload: OfferInput) -> dict:
    """POST /analyze-offer — Agent 1 isolé."""
    logger.info("POST /analyze-offer — user_id=%s", payload.user_id)

    from app.agents.offer_analyzer import offer_analyzer_node

    state: AgentState = {
        "raw_offer_text": payload.raw_text,
        "user_id": payload.user_id,
        "template_id": payload.template_id,
        "messages": [],
        "errors": [],
        "pipeline_version": "2.0",
    }

    try:
        result = await offer_analyzer_node(state)
        if not result.get("analyzed_offer"):
            raise HTTPException(status_code=502, detail="Erreur LLM — analyse échouée")
        return result["analyzed_offer"]
    except HTTPException:
        raise
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))


@app.post(
    "/match",
    response_model=dict,
    tags=["M2 — AI Agent"],
    summary="Agents 2-3-4 — Matching profil ↔ offre",
    description="Lance les agents 2 (profil), 3 (normalisation), 4 (scoring) sans Agent 1.",
)
async def match_profile(payload: dict) -> dict:
    """POST /match — Agents 2-3-4 isolés."""
    user_id = payload.get("user_id", "")
    analyzed_offer = payload.get("analyzed_offer", {})

    logger.info("POST /match — user_id=%s", user_id)

    from app.agents.profile_retriever import profile_retriever_node
    from app.agents.normalizer import normalizer_node
    from app.agents.scorer import scorer_node

    state: AgentState = {
        "user_id": user_id,
        "analyzed_offer": analyzed_offer,
        "messages": [],
        "errors": [],
        "normalized_offer_skills": [],
        "normalized_profile_skills": [],
        "normalized_keywords": [],
        "profile_full_text": "",
        "pipeline_version": "2.0",
    }

    try:
        state.update(await profile_retriever_node(state))
        state.update(await normalizer_node(state))
        state.update(await scorer_node(state))
        return state.get("match_result") or {}
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))


logger.info("✅ NextStep Agents démarré — Architecture LangGraph StateGraph")
