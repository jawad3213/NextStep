# ============================================================
# app/domain/offer_analyzer/graph/workflow.py
# ============================================================
from langgraph.graph import StateGraph, END
from app.domain.offer_analyzer.schemas.state import OfferAnalyzerState
from app.domain.offer_analyzer.agents.agent import offer_analyzer_node


def build_offer_analyzer_workflow() -> StateGraph:
    """Construit le sous-graphe pour offer_analyzer."""
    graph = StateGraph(OfferAnalyzerState)
    graph.add_node("offer_analyzer", offer_analyzer_node)
    graph.set_entry_point("offer_analyzer")
    graph.add_edge("offer_analyzer", END)
    return graph.compile()
