# ============================================================
# app/domain/profile_retriever/schemas/state.py
# ============================================================
import operator
from typing import Annotated, Optional, TypedDict
from langchain_core.messages import BaseMessage
from langgraph.graph.message import add_messages


class ProfileRetrieverState(TypedDict, total=False):
    """État interne de l'agent profile_retriever.
    
    Mission : Récupérer et structurer les données brutes du profil.
    """
    user_id: str
    profile_data: Optional[dict]

    messages: Annotated[list[BaseMessage], add_messages]
    errors: Annotated[list[str], operator.add]
