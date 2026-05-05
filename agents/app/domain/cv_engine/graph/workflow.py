# ============================================================
# app/domain/cv_engine/graph/workflow.py
# Graphe LangGraph du domaine CV_ENGINE
#
# Flux :
#   START → router ─[conditional]─► profile_loader ─► router
#                                 ► skill_optimizer ─► router
#                                 ► cv_structurer ─► router
#                                 ► END
#   Chaque nœud → router (boucle) jusqu'à END
# ============================================================
import logging
from typing import Literal
from langchain_core.messages import AIMessage
from langgraph.graph import StateGraph, END

from app.domain.cv_engine.schemas.state import CvEngineState
from app.domain.cv_engine.agents.profile_loader import profile_loader_node
from app.domain.cv_engine.agents.skill_optimizer import skill_optimizer_node
from app.domain.cv_engine.agents.cv_structurer import cv_structurer_node

logger = logging.getLogger(__name__)

# ─── Types pour les arêtes conditionnelles ────────────────────
_NextNode = Literal[
    "profile_loader", "skill_optimizer", "cv_structurer", "__end__"
]


# ─────────────────────────────────────────────────────────────
# NŒUD ROUTER
# ─────────────────────────────────────────────────────────────

async def router_node(state: CvEngineState) -> dict:
    """
    Nœud Router — Analyse l'état et décide quel nœud appeler.

    Logique de priorité (dans l'ordre) :
      1. raw_profile manquant     → profile_loader
      2. optimized_skills manquant → skill_optimizer
      3. cv_json manquant         → cv_structurer
      4. Tout présent             → end
    """
    logger.info("🔀 CvEngine Router — Décision routing")

    if not state.get("raw_profile"):
        next_node = "profile_loader"
    elif state.get("optimized_skills") is None:
        next_node = "skill_optimizer"
    elif not state.get("cv_json"):
        next_node = "cv_structurer"
    else:
        next_node = "end"

    logger.info("🔀 CvEngine Router → %s", next_node)
    return {
        "next_node": next_node,
        "messages": [AIMessage(
            content=f"[CvEngine:Router] → {next_node}",
            name="cv_engine_router",
        )],
    }


def _decide_next(state: CvEngineState) -> _NextNode:
    """
    Fonction d'arête conditionnelle — lit `next_node` et retourne
    le nom du prochain nœud pour LangGraph.
    """
    next_node = state.get("next_node", "end")
    return "__end__" if next_node == "end" else next_node  # type: ignore


# ─────────────────────────────────────────────────────────────
# CONSTRUCTION DU GRAPHE
# ─────────────────────────────────────────────────────────────

def build_cv_engine_workflow() -> StateGraph:
    """
    Construit et compile le graphe LangGraph du domaine CV_ENGINE.

    Topologie :
      START → router ─[conditional]─► profile_loader ─► router
                                    ► skill_optimizer ─► router
                                    ► cv_structurer ─► router
                                    ► END
    """
    graph = StateGraph(CvEngineState)

    # ── Nœuds ──────────────────────────────────────────────────
    graph.add_node("router",          router_node)
    graph.add_node("profile_loader",  profile_loader_node)
    graph.add_node("skill_optimizer", skill_optimizer_node)
    graph.add_node("cv_structurer",   cv_structurer_node)

    # ── Point d'entrée ─────────────────────────────────────────
    graph.set_entry_point("router")

    # ── Arête conditionnelle depuis le Router ──────────────────
    graph.add_conditional_edges(
        "router",
        _decide_next,
        {
            "profile_loader":  "profile_loader",
            "skill_optimizer": "skill_optimizer",
            "cv_structurer":   "cv_structurer",
            "__end__":         END,
        },
    )

    # ── Arêtes de retour au Router (après chaque nœud) ────────
    for node in ["profile_loader", "skill_optimizer", "cv_structurer"]:
        graph.add_edge(node, "router")

    return graph.compile()


# ── Singleton compilé ─────────────────────────────────────────
_workflow = None


def get_cv_engine_workflow():
    """Retourne le graphe compilé (singleton — évite la recompilation)."""
    global _workflow
    if _workflow is None:
        _workflow = build_cv_engine_workflow()
        logger.info("✅ Graphe CV_ENGINE compilé")
    return _workflow
