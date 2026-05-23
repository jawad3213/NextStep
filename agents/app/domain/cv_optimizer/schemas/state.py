from typing import Annotated, Optional, TypedDict
import operator
from langchain_core.messages import BaseMessage
from langgraph.graph.message import add_messages

class CVOptimizerState(TypedDict, total=False):
    """
    État interne du workflow d'optimisation de CV.
    """
    candidate_cv: dict
    job_offer: dict
    match_result: Optional[dict]
    
    optimized_cv: Optional[dict]
    
    messages: Annotated[list[BaseMessage], add_messages]
    errors: Annotated[list[str], operator.add]
    iteration_count: int
