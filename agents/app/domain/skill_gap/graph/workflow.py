from langgraph.graph import StateGraph, END
from app.domain.skill_gap.schemas.state import SkillGapState
from app.domain.skill_gap.agents.agent import skill_gap_node, skill_validator_node, skill_analyzer_router

def build_skill_gap_workflow():
    """
    Construit le workflow pour l'analyse de Skill Gap avec boucle de retry.
    """
    workflow = StateGraph(SkillGapState)

    # Ajout des nœuds
    workflow.add_node("skill_gap_analyzer", skill_gap_node)
    workflow.add_node("validator", skill_validator_node)

    # Définition des arêtes
    workflow.set_entry_point("skill_gap_analyzer")
    workflow.add_edge("skill_gap_analyzer", "validator")

    # Arête conditionnelle pour le retry
    workflow.add_conditional_edges(
        "validator",
        skill_analyzer_router,
        {
            "retry": "skill_gap_analyzer",
            "end": END
        }
    )

    return workflow.compile()
