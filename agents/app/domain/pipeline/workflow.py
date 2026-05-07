# ============================================================
# app/domain/pipeline/workflow.py
# Orchestrateur (Pipeline) — Communication par SERVICES
# ============================================================
import logging
from typing import Literal
from langchain_core.messages import AIMessage
from langgraph.graph import StateGraph, END

from app.domain.offer.schemas.state import OfferState

# Import des SERVICES (Communication inter-domaines)
from app.domain.offer_analyzer.service import offer_analyzer_service
from app.domain.profile_retriever.service import profile_retriever_service
from app.domain.scorer.service import scorer_service

logger = logging.getLogger(__name__)

_NextNode = Literal[
    "offer_analyzer_node", "profile_retriever_node",
    "scorer_node", "cv_formatter_node", "email_composer_node", "__end__"
]

# ─── NŒUDS DU PIPELINE (Appellent les Services) ───────────────

async def offer_analyzer_node(state: OfferState) -> dict:
    """Nœud appelant le service d'analyse d'offre."""
    logger.info("Pipeline -- Calling OfferAnalyzerService")
    result = await offer_analyzer_service.analyze(state["raw_offer_text"])
    
    return {
        "analyzed_offer":          result.get("analyzed_offer"),
        "normalized_offer_skills": result.get("normalized_offer_skills"),
        "normalized_keywords":     result.get("normalized_keywords"),
        "errors":                  result.get("errors", []),
        "messages": [AIMessage(content="[Pipeline] Offre analysée via Service", name="orchestrator")],
    }

async def profile_retriever_node(state: OfferState) -> dict:
    """Nœud appelant le service de récupération de profil."""
    logger.info("Pipeline -- Calling ProfileRetrieverService")
    result = await profile_retriever_service.get_profile(state["user_id"])
    
    # On délègue aussi la normalisation au service du profil si nécessaire
    # (Ici Agent 2 dans le service s'en charge déjà)
    return {
        "profile_data":              result.get("profile_data"),
        "normalized_profile_skills": result.get("normalized_profile_skills", []),
        "profile_full_text":         result.get("profile_full_text", ""),
        "errors":                    result.get("errors", []),
        "messages": [AIMessage(content="[Pipeline] Profil récupéré via Service", name="orchestrator")],
    }

async def scorer_node(state: OfferState) -> dict:
    """Nœud appelant le service de scoring."""
    logger.info("Pipeline -- Calling ScorerService")
    result = await scorer_service.calculate_scores(
        normalized_offer_skills=state.get("normalized_offer_skills", []),
        normalized_profile_skills=state.get("normalized_profile_skills", []),
        normalized_keywords=state.get("normalized_keywords", []),
        profile_full_text=state.get("profile_full_text", ""),
        analyzed_offer=state.get("analyzed_offer", {}),
        profile_data=state.get("profile_data", {})
    )
    return {
        "match_result": result.get("match_result"),
        "errors":       result.get("errors", []),
        "messages": [AIMessage(content="[Pipeline] Scores calculés via Service", name="orchestrator")],
    }

# ─── ROUTER & STUBS ───────────────────────────────────────────

def _decide_next(state: OfferState) -> str:
    """Logique de routage conditionnel."""
    if not state.get("analyzed_offer"):
        return "offer_analyzer_node"
    if not state.get("profile_data"):
        return "profile_retriever_node"
    if not state.get("match_result"):
        return "scorer_node"
    return END

# ─── CONSTRUCTION DU GRAPHE ───────────────────────────────────

def build_offer_pipeline() -> StateGraph:
    workflow = StateGraph(OfferState)

    workflow.add_node("offer_analyzer_node",    offer_analyzer_node)
    workflow.add_node("profile_retriever_node", profile_retriever_node)
    workflow.add_node("scorer_node",            scorer_node)

    workflow.set_entry_point("offer_analyzer_node")

    workflow.add_edge("offer_analyzer_node",    "profile_retriever_node")
    workflow.add_edge("profile_retriever_node", "scorer_node")
    workflow.add_edge("scorer_node",            END)

    return workflow.compile()

_pipeline = None

def get_offer_pipeline():
    global _pipeline
    if _pipeline is None:
        _pipeline = build_offer_pipeline()
    return _pipeline
