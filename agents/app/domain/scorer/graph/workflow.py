# ============================================================
# app/domain/scorer/graph/workflow.py
# ============================================================
from langgraph.graph import StateGraph, END
from app.domain.scorer.schemas.state import ScorerState
from app.domain.scorer.agents.agent import scorer_node


def build_scorer_workflow() -> StateGraph:
    """Construit le sous-graphe pour scorer."""
    graph = StateGraph(ScorerState)
    graph.add_node("scorer", scorer_node)
    graph.set_entry_point("scorer")
    graph.add_edge("scorer", END)
    return graph.compile()
