# ============================================================
# app/domain/pipeline/workflow.py
# Orchestrateur (Pipeline) — Communication par SERVICES
#
# Flow:
#   offer_analyzer_node
#   → profile_retriever_node
#   → skill_gap_node
#   → company_node         (optional enrichment — fails gracefully)
#   → email_composer_node  (initial application email only)
#   → END
# ============================================================
import logging
from langchain_core.messages import AIMessage
from langgraph.graph import StateGraph, END

from app.domain.pipeline.state import PipelineState

# Import des SERVICES (Communication inter-domaines)
from app.domain.offer_analyzer.service import offer_analyzer_service
from app.domain.profile_retriever.service import profile_retriever_service
from app.domain.skill_gap.service import skill_gap_service
from app.domain.company.service import company_service
from app.domain.email_composer.agents.agent import email_composer_node as _email_composer_node

logger = logging.getLogger(__name__)


# ─── NŒUDS DU PIPELINE ────────────────────────────────────────

async def offer_analyzer_node(state: PipelineState) -> dict:
    """Nœud appelant le service d'analyse d'offre."""
    logger.info("Pipeline — Calling OfferAnalyzerService")
    result = await offer_analyzer_service.analyze(state["raw_offer_text"])
    return {
        "analyzed_offer":          result.get("analyzed_offer"),
        "normalized_offer_skills": result.get("normalized_offer_skills") or [],
        "normalized_keywords":     result.get("normalized_keywords") or [],
        "errors":                  result.get("errors") or [],
        "messages": [AIMessage(content="[Pipeline] Offre analysée via Service", name="orchestrator")],
    }


async def profile_retriever_node(state: PipelineState) -> dict:
    """Nœud appelant le service de récupération de profil."""
    logger.info("Pipeline — Calling ProfileRetrieverService")
    result = await profile_retriever_service.get_profile(str(state["user_id"]))
    return {
        "profile_data": result.get("profile_data"),
        "errors":       result.get("errors") or [],
        "messages": [AIMessage(content="[Pipeline] Profil récupéré via Service", name="orchestrator")],
    }


async def skill_gap_node(state: PipelineState) -> dict:
    """Nœud appelant le service de skill gap."""
    logger.info("Pipeline — Calling SkillGapService")
    if not state.get("profile_data") or not state.get("analyzed_offer"):
        return {"errors": ["SkillGap: données manquantes pour l'analyse d'écart"]}

    result = await skill_gap_service.analyze_skill_gap(
        candidate_cv=state["profile_data"],
        job_offer=state["analyzed_offer"]
    )
    return {
        "match_result": result.skill_gap.model_dump() if result.skill_gap else None,
        "errors":       result.errors or [],
        "messages": [AIMessage(content="[Pipeline] Skill Gap analysé via Service", name="orchestrator")],
    }


async def company_node(state: PipelineState) -> dict:
    """
    Nœud appelant le service d'intelligence entreprise.

    Optional enrichment — if it fails, the pipeline continues without
    company_intelligence (email_composer will use generic motivation).
    """
    logger.info("Pipeline — Calling CompanyService")
    analyzed_offer = state.get("analyzed_offer") or {}
    company_name = (
        analyzed_offer.get("entreprise")
        or analyzed_offer.get("company_name")
        or ""
    )
    job_title = (
        analyzed_offer.get("titre")
        or analyzed_offer.get("job_title")
        or "Poste"
    )

    if not company_name:
        logger.warning("Pipeline — company_node: no company_name found, skipping company research.")
        return {
            "warnings": ["Pipeline: company_name introuvable — intelligence entreprise ignorée."],
            "messages": [AIMessage(content="[Pipeline] Company skipped (no company name)", name="orchestrator")],
        }

    try:
        result = await company_service.get_company_intelligence(
            company_name=company_name,
            job_title=job_title,
            user_id=str(state.get("user_id", "")),
            candidate_cv=state.get("profile_data"),
            job_offer=analyzed_offer,
        )
        return {
            "company_intelligence": result,
            "messages": [AIMessage(
                content=f"[Pipeline] Intelligence entreprise récupérée pour '{company_name}'",
                name="orchestrator",
            )],
        }
    except Exception as e:
        logger.warning("Pipeline — company_node failed (non-fatal): %s", e)
        return {
            "warnings": [f"Pipeline: company agent failed — {str(e)}"],
            "messages": [AIMessage(content="[Pipeline] Company agent failed (non-fatal)", name="orchestrator")],
        }


async def email_composer_node(state: PipelineState) -> dict:
    """
    Adaptor node — bridges PipelineState → EmailComposerState.

    Maps match_result → skill_gap for the email_composer agent.
    Calls the domain agent node and merges its output back into pipeline state.
    """
    logger.info("Pipeline — Calling EmailComposerNode")

    # Build EmailComposerState-compatible sub-dict from PipelineState
    email_state = {
        "user_id":            str(state.get("user_id", "")),
        "profile_data":       state.get("profile_data"),
        "analyzed_offer":     state.get("analyzed_offer"),
        "raw_offer_text":     state.get("raw_offer_text"),
        "skill_gap":          state.get("match_result"),         # rename for email composer
        "company_intelligence": state.get("company_intelligence"),
        "generation_options": state.get("generation_options"),
        "messages":           [],
        "errors":             [],
        "warnings":           [],
        "iteration_count":    0,
    }

    result = await _email_composer_node(email_state)

    # Surface output back to pipeline state
    output: dict = {}
    if result.get("email_draft"):
        output["email_draft"] = result["email_draft"]
    if result.get("errors"):
        output["errors"] = result["errors"]
    if result.get("warnings"):
        output["warnings"] = result["warnings"]
    if result.get("messages"):
        output["messages"] = result["messages"]

    return output


# ─── CONSTRUCTION DU GRAPHE ───────────────────────────────────

def build_offer_pipeline() -> StateGraph:
    workflow = StateGraph(PipelineState)

    workflow.add_node("offer_analyzer_node",    offer_analyzer_node)
    workflow.add_node("profile_retriever_node", profile_retriever_node)
    workflow.add_node("skill_gap_node",         skill_gap_node)
    workflow.add_node("company_node",           company_node)
    workflow.add_node("email_composer_node",    email_composer_node)

    workflow.set_entry_point("offer_analyzer_node")

    workflow.add_edge("offer_analyzer_node",    "profile_retriever_node")
    workflow.add_edge("profile_retriever_node", "skill_gap_node")
    workflow.add_edge("skill_gap_node",         "company_node")
    workflow.add_edge("company_node",           "email_composer_node")
    workflow.add_edge("email_composer_node",    END)

    return workflow.compile()


_pipeline = None

def get_offer_pipeline():
    global _pipeline
    if _pipeline is None:
        _pipeline = build_offer_pipeline()
    return _pipeline
