# ============================================================
# app/domain/profile_retriever/graph/workflow.py
# ============================================================
from langgraph.graph import StateGraph, END
from app.domain.profile_retriever.schemas.state import ProfileRetrieverState
from app.domain.profile_retriever.agents.agent import profile_retriever_node


def build_profile_retriever_workflow() -> StateGraph:
    """Construit le sous-graphe pour profile_retriever."""
    graph = StateGraph(ProfileRetrieverState)
    graph.add_node("profile_retriever", profile_retriever_node)
    graph.set_entry_point("profile_retriever")
    graph.add_edge("profile_retriever", END)
    return graph.compile()
