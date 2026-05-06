# ============================================================
# app/domain/offer/graph/workflow.py
# Graphe LangGraph du domaine OFFER
#
# Flux (simplifié — normalizer intégré dans les agents) :
#   START → router ──[conditional]──► offer_analyzer      (Agent 1 + normalise offre)
#                                   ► profile_retriever   (Agent 2 + normalise profil)
#                                   ► scorer              (Agent 4 — ATS + Matching)
#                                   ► cv_formatter        [stub M3]
#                                   ► email_composer      [stub M4]
#                                   ► END
#   Chaque agent → router (boucle) jusqu'à END
# ============================================================
import logging
from typing import Literal
from langchain_core.messages import AIMessage
from langgraph.graph import StateGraph, END

from app.domain.offer.schemas.state import OfferState
from app.domain.offer_analyzer.graph.workflow import build_offer_analyzer_workflow
from app.domain.profile_retriever.graph.workflow import build_profile_retriever_workflow
from app.domain.scorer.graph.workflow import build_scorer_workflow

logger = logging.getLogger(__name__)

# ─── Types pour les arêtes conditionnelles ────────────────────
_NextNode = Literal[
    "offer_analyzer", "profile_retriever",
    "scorer", "cv_formatter", "email_composer", "__end__"
]


# ─────────────────────────────────────────────────────────────
# NŒUD ROUTER
# ─────────────────────────────────────────────────────────────

async def router_node(state: OfferState) -> dict:
    """
    Nœud Router — Analyse l'état et décide quel agent appeler.

    Logique de priorité (dans l'ordre) :
      1. analyzed_offer manquant        → offer_analyzer  (+ normalise offre)
      2. profile_data manquant          → profile_retriever (+ normalise profil)
      3. match_result manquant          → scorer
      4. cv_template_json manquant      → cv_formatter (stub M3)
      5. email_draft manquant           → email_composer (stub M4)
      6. Tout présent                   → end
    """
    logger.info("🔀 Router — Décision routing")

    if not state.get("analyzed_offer"):
        next_agent = "offer_analyzer"
    elif not state.get("profile_data"):
        next_agent = "profile_retriever"
    elif not state.get("match_result"):
        next_agent = "scorer"
    elif not state.get("cv_template_json"):
        next_agent = "cv_formatter"
    elif not state.get("email_draft"):
        next_agent = "email_composer"
    else:
        next_agent = "end"

    logger.info("🔀 Router → %s", next_agent)
    return {
        "next_agent": next_agent,
        "messages": [AIMessage(
            content=f"[Router] → {next_agent}",
            name="router",
        )],
    }


def _decide_next(state: OfferState) -> _NextNode:
    """
    Fonction d'arête conditionnelle — lit `next_agent` et retourne
    le nom du prochain nœud pour LangGraph.
    """
    next_agent = state.get("next_agent", "end")
    return "__end__" if next_agent == "end" else next_agent  # type: ignore


# ─────────────────────────────────────────────────────────────
# STUBS — Agents M3 / M4 (implémentés par d'autres membres)
# ─────────────────────────────────────────────────────────────

async def cv_formatter_node(state: OfferState) -> dict:
    """
    Agent 5 [STUB M3] — Formate les données pour QuestPDF.
    À implémenter par M3 (cv_engine).
    """
    logger.info("📄 Agent 5 [CV Formatter] — Stub M3")
    profile      = state.get("profile_data")    or {}
    match_result = state.get("match_result")     or {}

    cv_json = {
        "profile":        profile,
        "match_score":    match_result.get("score_matching", 0),
        "ats_score":      match_result.get("score_ats", 0),
        "template_id":    state.get("template_id", 1),
        "analyzed_offer": state.get("analyzed_offer"),
        "_note": "Stub M3 — implémenté par cv_engine",
    }
    return {
        "cv_template_json": cv_json,
        "messages": [AIMessage(content="[Agent 5] CV formatté (stub M3)", name="cv_formatter")],
    }


async def email_composer_node(state: OfferState) -> dict:
    """
    Agent 6 [STUB M4] — Rédige l'email de candidature.
    À implémenter par M4 (email_engine).
    """
    logger.info("✉️  Agent 6 [Email Composer] — Stub M4")
    profile = state.get("profile_data")   or {}
    offer   = state.get("analyzed_offer") or {}

    email_stub = {
        "objet": (
            f"Candidature — {offer.get('titre', 'Poste')} — "
            f"{profile.get('prenom', '')} {profile.get('nom', '')}"
        ),
        "corps": (
            f"Bonjour,\n\n"
            f"Je vous adresse ma candidature pour le poste de {offer.get('titre', '')}.\n"
            f"[Email généré par LangChain — implémenté par M4]\n\n"
            f"Cordialement,\n{profile.get('prenom', '')} {profile.get('nom', '')}"
        ),
        "type": "candidature",
        "_note": "Stub M4 — implémenté par email_engine",
    }
    return {
        "email_draft": email_stub,
        "messages": [AIMessage(content="[Agent 6] Email rédigé (stub M4)", name="email_composer")],
    }


# ─────────────────────────────────────────────────────────────
# CONSTRUCTION DU GRAPHE
# ─────────────────────────────────────────────────────────────

def build_offer_workflow() -> StateGraph:
    """
    Construit et compile le graphe LangGraph du domaine OFFER.

    Topologie (sans nœud normalizer) :
      START → router ─[conditional]─► offer_analyzer ─► router
                                    ► profile_retriever ─► router
                                    ► scorer ─► router
                                    ► cv_formatter ─► router
                                    ► email_composer ─► router
                                    ► END
    """
    graph = StateGraph(OfferState)

    # ── Sous-graphes ───────────────────────────────────────────
    offer_analyzer_graph    = build_offer_analyzer_workflow()
    profile_retriever_graph = build_profile_retriever_workflow()
    scorer_graph            = build_scorer_workflow()

    # ── Nœuds ──────────────────────────────────────────────────
    graph.add_node("router",            router_node)
    graph.add_node("offer_analyzer",    offer_analyzer_graph)
    graph.add_node("profile_retriever", profile_retriever_graph)
    graph.add_node("scorer",            scorer_graph)
    graph.add_node("cv_formatter",      cv_formatter_node)
    graph.add_node("email_composer",    email_composer_node)

    # ── Point d'entrée ─────────────────────────────────────────
    graph.set_entry_point("router")

    # ── Arête conditionnelle depuis le Router ──────────────────
    graph.add_conditional_edges(
        "router",
        _decide_next,
        {
            "offer_analyzer":    "offer_analyzer",
            "profile_retriever": "profile_retriever",
            "scorer":            "scorer",
            "cv_formatter":      "cv_formatter",
            "email_composer":    "email_composer",
            "__end__":           END,
        },
    )

    # ── Arêtes de retour au Router (après chaque agent) ────────
    for node in [
        "offer_analyzer", "profile_retriever",
        "scorer",
        "cv_formatter", "email_composer",
    ]:
        graph.add_edge(node, "router")

    return graph.compile()


# ── Singleton compilé ─────────────────────────────────────────
_workflow = None


def get_offer_workflow():
    """Retourne le graphe compilé (singleton — évite la recompilation)."""
    global _workflow
    if _workflow is None:
        _workflow = build_offer_workflow()
        logger.info("✅ Graphe OFFER compilé (sans nœud normalizer)")
    return _workflow
