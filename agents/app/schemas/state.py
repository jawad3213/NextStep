# ============================================================
# app/schemas/state.py — État partagé du graphe LangGraph
#
# Pattern LangGraph :
# - Les champs de type list[BaseMessage] utilisent Annotated + add_messages
#   pour ACCUMULER les messages (reducer) au lieu de les remplacer.
# - Les autres champs sont des valeurs scalaires remplacées à chaque mise à jour.
# ============================================================
import operator
from typing import Annotated, Optional, TypedDict
from langchain_core.messages import BaseMessage
from langgraph.graph.message import add_messages


class AgentState(TypedDict, total=False):
    """
    État unique circulant entre tous les nœuds du graphe LangGraph.

    Cycle de vie :
        START → (raw_offer_text, user_id, template_id fournis)
              → Agent 1 : analyzed_offer enrichi
              → Agent 2 : profile_data enrichi
              → Agent 3 : normalized_* enrichi
              → Agent 4 : match_result enrichi
              → Agent 5 : cv_template_json enrichi  [stub M3]
              → Agent 6 : email_draft enrichi        [stub M4]
              → END

    Champs messages :
        Utilisent `Annotated[list, add_messages]` — LangGraph accumulera
        automatiquement les messages sans écraser les précédents.
    """

    # ─── Historique des messages (accumulatif via reducer) ───
    messages: Annotated[list[BaseMessage], add_messages]

    # ─── Entrées initiales (fournies au START) ───
    raw_offer_text: str
    user_id: str
    template_id: int

    # ─── Agent 1 : Analyseur d'offre (LLM) ───
    analyzed_offer: Optional[dict]
    """JSON structuré : titre, compétences, keywords_ats, etc."""

    # ─── Agent 2 : Profile Retriever (DB / RAG) ───
    profile_data: Optional[dict]
    """Profil complet du candidat depuis PostgreSQL."""

    # ─── Agent 3 : Normalisateur (Algorithme) ───
    normalized_offer_skills: Annotated[list[str], operator.add]
    normalized_profile_skills: Annotated[list[str], operator.add]
    normalized_keywords: Annotated[list[str], operator.add]
    profile_full_text: str

    # ─── Agent 4 : Scorer (Calcul mathématique) ───
    match_result: Optional[dict]
    """Scores matching + ATS + recommandations."""

    # ─── Agent 5 : Formatteur Template [stub M3] ───
    cv_template_json: Optional[dict]
    """Données formatées pour QuestPDF."""

    # ─── Agent 6 : Email Composer [stub M4] ───
    email_draft: Optional[dict]
    """Email de candidature rédigé par le LLM."""

    # ─── Décision du Router ───
    next_agent: Optional[str]
    """Nom du prochain agent à appeler (pour les arêtes conditionnelles)."""

    # ─── Méta-données pipeline ───
    errors: Annotated[list[str], operator.add]
    pipeline_version: str
