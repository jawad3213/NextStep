# ============================================================
# app/graphs/main_workflow.py — Graphe LangGraph Principal
#
# Architecture (selon le document) :
#   - Nodes : chaque agent est un nœud
#   - Edges : définissent le flux séquentiel
#   - Conditional Edges : le nœud "router" décide quel agent appeler
#
# Flux :
#   START → router  →  offer_analyzer  →  profile_retriever
#                   →  normalizer  →  scorer  →  cv_formatter(stub)
#                   →  email_composer(stub)  →  END
# ============================================================
import logging
from typing import Literal
from langchain_core.messages import AIMessage
from langgraph.graph import StateGraph, END

from app.schemas.state import AgentState
from app.agents.offer_analyzer import offer_analyzer_node
from app.agents.profile_retriever import profile_retriever_node
from app.agents.normalizer import normalizer_node
from app.agents.scorer import scorer_node

logger = logging.getLogger(__name__)


# ─────────────────────────────────────────────────────────
# NŒUD ROUTER — Décide quel agent appeler selon le contexte
# ─────────────────────────────────────────────────────────
async def router_node(state: AgentState) -> dict:
    """
    Nœud Router — Analyse le contexte et détermine le prochain agent.

    Logique de décision :
    - Si pas d'analyzed_offer → lancer offer_analyzer
    - Si pas de profile_data → lancer profile_retriever
    - Si pas de normalized_offer_skills → lancer normalizer
    - Si pas de match_result → lancer scorer
    - Si pas de cv_template_json → lancer cv_formatter (M3)
    - Si pas de email_draft → lancer email_composer (M4)
    - Sinon → END
    """
    logger.info("🔀 Router — Analyse de l'état actuel")

    if not state.get("analyzed_offer"):
        next_agent = "offer_analyzer"
    elif not state.get("profile_data"):
        next_agent = "profile_retriever"
    elif not state.get("normalized_offer_skills"):
        next_agent = "normalizer"
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
            content=f"[Router] Prochain agent : {next_agent}",
            name="router",
        )],
    }


# ─────────────────────────────────────────────────────────
# Fonction de décision pour les arêtes conditionnelles
# ─────────────────────────────────────────────────────────
def decide_next(
    state: AgentState,
) -> Literal["offer_analyzer", "profile_retriever", "normalizer", "scorer", "cv_formatter", "email_composer", "__end__"]:
    """
    Conditional Edge — Lit `next_agent` depuis le state et retourne
    le nom du prochain nœud à exécuter.

    C'est ici que LangGraph permet la boucle et les branchements.
    """
    next_agent = state.get("next_agent", "end")
    if next_agent == "end":
        return "__end__"
    return next_agent  # type: ignore[return-value]


# ─────────────────────────────────────────────────────────
# STUB — Agent 5 : Formatteur Template CV (M3)
# ─────────────────────────────────────────────────────────
async def cv_formatter_node(state: AgentState) -> dict:
    """Agent 5 [M3 STUB] — Formate les données pour QuestPDF."""
    logger.info("📄 Agent 5 [CV Formatter] — Stub M3")
    profile = state.get("profile_data") or {}
    match_result = state.get("match_result") or {}

    cv_json = {
        "profile": profile,
        "match_score": match_result.get("score_matching", 0),
        "ats_score": match_result.get("score_ats", 0),
        "template_id": state.get("template_id", 1),
        "analyzed_offer": state.get("analyzed_offer"),
        "_note": "Stub M3 — à implémenter par M3",
    }
    return {
        "cv_template_json": cv_json,
        "messages": [AIMessage(content="[Agent 5] CV data formatté (stub M3)", name="cv_formatter")],
    }


# ─────────────────────────────────────────────────────────
# STUB — Agent 6 : Email Composer (M4)
# ─────────────────────────────────────────────────────────
async def email_composer_node(state: AgentState) -> dict:
    """Agent 6 [M4 STUB] — Rédige l'email de candidature via LLM."""
    logger.info("✉️  Agent 6 [Email Composer] — Stub M4")
    profile = state.get("profile_data") or {}
    offer = state.get("analyzed_offer") or {}

    email_stub = {
        "objet": f"Candidature — {offer.get('titre', 'Poste')} — {profile.get('prenom', '')} {profile.get('nom', '')}",
        "corps": (
            f"Bonjour,\n\n"
            f"Je vous adresse ma candidature pour le poste de {offer.get('titre', '')}.\n"
            f"[Email généré par LangChain — implémenté par M4]\n\n"
            f"Cordialement,\n{profile.get('prenom', '')} {profile.get('nom', '')}"
        ),
        "type": "candidature",
        "_note": "Stub M4 — à implémenter par M4",
    }
    return {
        "email_draft": email_stub,
        "messages": [AIMessage(content="[Agent 6] Email rédigé (stub M4)", name="email_composer")],
    }


# ─────────────────────────────────────────────────────────
# CONSTRUCTION DU GRAPHE
# ─────────────────────────────────────────────────────────
def build_main_workflow() -> StateGraph:
    """
    Construit et compile le graphe LangGraph principal.

    Architecture :
        START → router ─[conditional edge]─► offer_analyzer
                                           ► profile_retriever
                                           ► normalizer
                                           ► scorer
                                           ► cv_formatter
                                           ► email_composer
                                           ► END

        Chaque agent retourne au router après exécution.
        Le router décide du prochain nœud (arête conditionnelle).
    """
    graph = StateGraph(AgentState)

    # ─── Enregistrement des nœuds ───
    graph.add_node("router",            router_node)
    graph.add_node("offer_analyzer",    offer_analyzer_node)
    graph.add_node("profile_retriever", profile_retriever_node)
    graph.add_node("normalizer",        normalizer_node)
    graph.add_node("scorer",            scorer_node)
    graph.add_node("cv_formatter",      cv_formatter_node)
    graph.add_node("email_composer",    email_composer_node)

    # ─── Point d'entrée ───
    graph.set_entry_point("router")

    # ─── Arête conditionnelle depuis le Router ───
    # Le Router lit `next_agent` et dirige vers le bon nœud
    graph.add_conditional_edges(
        "router",
        decide_next,
        {
            "offer_analyzer":    "offer_analyzer",
            "profile_retriever": "profile_retriever",
            "normalizer":        "normalizer",
            "scorer":            "scorer",
            "cv_formatter":      "cv_formatter",
            "email_composer":    "email_composer",
            "__end__":           END,
        },
    )

    # ─── Arêtes de retour au Router (boucle) ───
    # Après chaque agent, on revient au Router pour décider la suite
    graph.add_edge("offer_analyzer",    "router")
    graph.add_edge("profile_retriever", "router")
    graph.add_edge("normalizer",        "router")
    graph.add_edge("scorer",            "router")
    graph.add_edge("cv_formatter",      "router")
    graph.add_edge("email_composer",    "router")

    return graph.compile()


# ─── Instance compilée (singleton) ───
_workflow = None


def get_workflow():
    """Retourne le graphe compilé (singleton)."""
    global _workflow
    if _workflow is None:
        _workflow = build_main_workflow()
        logger.info("✅ Graphe LangGraph compilé avec succès")
    return _workflow
