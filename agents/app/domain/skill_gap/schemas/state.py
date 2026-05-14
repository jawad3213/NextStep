import operator
from typing import Annotated, Optional, TypedDict
from langchain_core.messages import BaseMessage
from langgraph.graph.message import add_messages

class SkillGapState(TypedDict, total=False):
    """
    État du domaine SKILL GAP — Analyse d'adéquation CV/Offre.
    """
    # ── Accumulatifs (Reducers) ───────────────────────────────
    messages: Annotated[list[BaseMessage], add_messages]
    errors: Annotated[list[str], operator.add]

    # ── Entrées ───────────────────────────────────────────────
    candidate_cv: Optional[dict]
    """CV du candidat structuré."""
    
    job_offer: Optional[dict]
    """Offre d'emploi structurée."""

    skill_gap: Optional[dict]
    """Analyse finale de l'écart de compétences."""

    iteration_count: int
    """Nombre de tentatives d'analyse (max 3)."""
