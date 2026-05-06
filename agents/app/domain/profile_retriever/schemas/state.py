# ============================================================
# app/domain/profile_retriever/schemas/state.py
# ============================================================
import operator
from typing import Annotated, Optional, TypedDict
from langchain_core.messages import BaseMessage
from langgraph.graph.message import add_messages


class ProfileRetrieverState(TypedDict, total=False):
    """État interne de l'agent profile_retriever.
    
    Après exécution, cet agent retourne également les champs normalisés
    du profil afin d'éviter un nœud normalizer séparé.
    """
    user_id: str
    analyzed_offer: Optional[dict]
    profile_data: Optional[dict]

    # ── Champs normalisés (calculés en sortie de l'agent) ─────
    normalized_profile_skills: Annotated[list[str], operator.add]
    profile_full_text: str

    messages: Annotated[list[BaseMessage], add_messages]
    errors: Annotated[list[str], operator.add]
