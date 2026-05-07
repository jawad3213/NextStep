from langgraph.graph import StateGraph, END
from app.domain.cv_optimizer.schemas.state import CVOptimizerState
from app.domain.cv_optimizer.agents.agent import cv_optimizer_node, cv_validator_node, cv_optimizer_router

def build_cv_optimizer_workflow() -> StateGraph:
    """
    Construit le workflow pour l'optimisation de CV avec boucle de retry.
    """
    workflow = StateGraph(CVOptimizerState)

    workflow.add_node("cv_optimizer", cv_optimizer_node)
    workflow.add_node("validator", cv_validator_node)

    workflow.set_entry_point("cv_optimizer")
    workflow.add_edge("cv_optimizer", "validator")

    workflow.add_conditional_edges(
        "validator",
        cv_optimizer_router,
        {
            "retry": "cv_optimizer",
            "end": END
        }
    )

    return workflow.compile()
