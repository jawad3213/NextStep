import operator
from typing import Annotated, Optional, TypedDict, Dict, Any
from langchain_core.messages import BaseMessage
from langgraph.graph.message import add_messages

class PipelineState(TypedDict, total=False):
    """État global du pipeline NextStep."""
    
    # -- Entrées --
    raw_offer_text: str
    user_id: str
    template_id: int
    offer_id: str

    # -- Données intermédiaires --
    analyzed_offer: Optional[Dict[str, Any]]
    normalized_offer_skills: Annotated[list[str], operator.add]
    normalized_keywords: Annotated[list[str], operator.add]
    
    profile_data: Optional[Dict[str, Any]]
    profile_full_text: str
    normalized_profile_skills: Annotated[list[str], operator.add]
    
    match_result: Optional[Dict[str, Any]]
    
    # -- Sorties Finales (CV) --
    cv_optimized_content: Optional[Dict[str, Any]]
    cv_engine_result: Optional[Dict[str, Any]]

    # -- Métadonnées --
    messages: Annotated[list[BaseMessage], add_messages]
    errors: Annotated[list[str], operator.add]
