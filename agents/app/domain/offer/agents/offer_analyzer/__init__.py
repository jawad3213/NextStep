# ============================================================
# app/domain/offer/agents/offer_analyzer/__init__.py
#
# Expose le nœud LangGraph de l'Agent 1 (Offer Analyzer).
# Import propre depuis le reste du projet :
#   from app.domain.offer.agents.offer_analyzer import offer_analyzer_node
# ============================================================
from app.domain.offer.agents.offer_analyzer.agent import offer_analyzer_node

__all__ = ["offer_analyzer_node"]
