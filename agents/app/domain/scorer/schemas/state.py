# ============================================================
# app/domain/scorer/schemas/state.py
# ============================================================
import operator
from typing import Annotated, Optional, TypedDict
from langchain_core.messages import BaseMessage
from langgraph.graph.message import add_messages


class ScorerState(TypedDict, total=False):
    """État interne de l'agent scorer."""
    normalized_offer_skills: list[str]
    normalized_profile_skills: list[str]
    normalized_keywords: list[str]
    profile_full_text: str
    analyzed_offer: Optional[dict]
    profile_data: Optional[dict]
    
    match_result: Optional[dict]
    
    messages: Annotated[list[BaseMessage], add_messages]
    errors: Annotated[list[str], operator.add]
