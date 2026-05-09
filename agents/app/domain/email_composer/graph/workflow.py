# ============================================================
# app/domain/email_composer/graph/workflow.py
# LangGraph subgraph for the email_composer domain.
# Same pattern as offer_analyzer/graph/workflow.py.
# ============================================================
from langgraph.graph import StateGraph, END
from app.domain.email_composer.schemas.state import EmailComposerState
from app.domain.email_composer.agents.agent import email_composer_node


def build_email_composer_workflow() -> StateGraph:
    """
    Build the email_composer subgraph.

    Single node (no retry loop — generation either succeeds or
    surfaces a graceful error to state).
    """
    graph = StateGraph(EmailComposerState)
    graph.add_node("email_composer", email_composer_node)
    graph.set_entry_point("email_composer")
    graph.add_edge("email_composer", END)
    return graph.compile()
