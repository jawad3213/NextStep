# ============================================================
# app/domain/company/schemas/state.py
# État LangGraph du domaine COMPANY (Analyse entreprise)
# ============================================================
import operator
from typing import Annotated, Optional, TypedDict
from langchain_core.messages import BaseMessage
from langgraph.graph.message import add_messages


class CompanyState(TypedDict, total=False):
    """
    État du domaine COMPANY.

    Le domaine COMPANY gère :
    - Enrichissement des informations sur l'entreprise
    - Analyse de la culture d'entreprise (LLM)
    - Score de compatibilité candidat ↔ entreprise
    - Recommandations personnalisées (angle entreprise)

    ┌──────────────────────────────────────────────────────────┐
    │  Champ                  │  Type        │  Reducer        │
    ├──────────────────────────────────────────────────────────┤
    │  messages               │  list[Msg]   │ add_messages    │
    │  errors                 │  list[str]   │ operator.add    │
    │  company_name           │  str         │  replace        │
    │  user_id                │  str         │  replace        │
    │  offer_data             │  dict|None   │  replace        │
    │  company_info           │  dict|None   │  replace        │
    │  company_culture_score  │  int|None    │  replace        │
    │  company_insights       │  list[str]   │  replace        │
    │  next_agent             │  str|None    │  replace        │
    │  pipeline_version       │  str         │  replace        │
    └──────────────────────────────────────────────────────────┘
    """

    # ── Accumulatifs ──────────────────────────────────────────
    messages: Annotated[list[BaseMessage], add_messages]
    errors: Annotated[list[str], operator.add]

    # ── Entrées ───────────────────────────────────────────────
    company_name: str
    """Nom de l'entreprise (extrait de analyzed_offer)."""

    user_id: str
    """Identifiant Keycloak de l'utilisateur."""

    offer_data: Optional[dict]
    """Données de l'offre analysée (Agent 1)."""

    profile_data: Optional[dict]
    """Profil du candidat (Agent 2) — pour le score de compatibilité."""

    # ── Résultats ─────────────────────────────────────────────
    company_info: Optional[dict]
    """
    Informations enrichies sur l'entreprise :
    {
        "nom":        str,
        "secteur":    str,
        "taille":     str,       # "startup" | "pme" | "grand_groupe"
        "localisation": str,
        "description": str,
        "technologies_stack": list[str],   # stack technique déduit
        "type_contrat_dominant": str,
        "remote_policy": str | null,       # "full_remote" | "hybride" | "presentiel"
    }
    """

    company_culture_score: Optional[int]
    """
    Score de compatibilité culture candidat ↔ entreprise (0-100).
    Calculé à partir des signaux de l'offre (type de contrat, remote,
    technologies, formulation de l'offre).
    """

    company_insights: list[str]
    """
    Insights actionnables pour le candidat :
    - "Cette entreprise valorise l'autonomie (remote mentionné)"
    - "Stack technique alignée avec votre profil"
    - "Premier poste junior détecté — idéal pour débuter"
    """

    # ── Routeur ──────────────────────────────────────────────
    next_agent: Optional[str]

    # ── Méta-données ─────────────────────────────────────────
    pipeline_version: str
