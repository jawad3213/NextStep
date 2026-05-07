# ============================================================
# app/domain/company/graph/company_graph.py
# Définition du workflow LangGraph pour le domaine COMPANY.
# ============================================================
from langgraph.graph import StateGraph, END
from app.domain.company.schemas.state import CompanyState
from app.domain.company.agents.intelligence_agent import researcher_node, analyst_node

def create_company_graph():
    """
    Crée le graphe d'intelligence entreprise.
    """
    workflow = StateGraph(CompanyState)

    # Ajout des nœuds
    workflow.add_node("researcher", researcher_node)
    workflow.add_node("analyst", analyst_node)

    # Définition des arêtes
    workflow.set_entry_point("researcher")
    workflow.add_edge("researcher", "analyst")
    workflow.add_edge("analyst", END)

    return workflow.compile()
