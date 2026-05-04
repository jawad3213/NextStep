# ============================================================
# app/domain/job/schemas/state.py
# État LangGraph du domaine JOB (Candidature & Suivi)
# ============================================================
import operator
from typing import Annotated, Optional, TypedDict
from langchain_core.messages import BaseMessage
from langgraph.graph.message import add_messages


class JobState(TypedDict, total=False):
    """
    État circulant dans le pipeline JOB.

    Le domaine JOB gère tout ce qui concerne la candidature :
    - Préparation des données CV pour QuestPDF (Agent 5)
    - Génération de l'email de candidature (Agent 6, LLM)
    - Suivi des statuts de candidature
    - Relances automatiques (J+7)

    ┌──────────────────────────────────────────────────────────┐
    │  Champ               │  Type          │  Reducer        │
    ├──────────────────────────────────────────────────────────┤
    │  messages            │  list[Msg]     │ add_messages    │
    │  errors              │  list[str]     │ operator.add    │
    │  candidature_id      │  str           │  replace        │
    │  user_id             │  str           │  replace        │
    │  offer_data          │  dict | None   │  replace        │
    │  profile_data        │  dict | None   │  replace        │
    │  match_result        │  dict | None   │  replace        │
    │  template_id         │  int           │  replace        │
    │  cv_template_json    │  dict | None   │  replace        │
    │  email_draft         │  dict | None   │  replace        │
    │  email_type          │  str           │  replace        │
    │  statut_candidature  │  str           │  replace        │
    │  next_agent          │  str | None    │  replace        │
    │  pipeline_version    │  str           │  replace        │
    └──────────────────────────────────────────────────────────┘
    """

    # ── Accumulatifs ──────────────────────────────────────────
    messages: Annotated[list[BaseMessage], add_messages]
    """Historique des messages agents."""

    errors: Annotated[list[str], operator.add]
    """Erreurs accumulées."""

    # ── Entrées initiales ─────────────────────────────────────
    candidature_id: str
    """UUID de la candidature dans PostgreSQL."""

    user_id: str
    """Identifiant Keycloak de l'utilisateur."""

    offer_data: Optional[dict]
    """
    Offre analysée (issu du domaine OFFER — Agent 1) :
    {
        "titre": str,
        "entreprise": str,
        "type_contrat": str,
        "competences_requises": list[str],
        "keywords_ats": list[str],
        ...
    }
    """

    profile_data: Optional[dict]
    """Profil candidat complet (issu du domaine OFFER — Agent 2)."""

    match_result: Optional[dict]
    """Résultat du scoring (issu du domaine OFFER — Agent 4)."""

    template_id: int
    """Template CV sélectionné (1=Modern, 2=Classic, 3=Creative)."""

    # ── Agent 5 — CV Formatter ────────────────────────────────
    cv_template_json: Optional[dict]
    """
    Données formatées pour QuestPDF (.NET) :
    {
        "sections": {
            "entete":        {...},   # nom, titre, contact
            "resume":        str,
            "competences":   [...],
            "experiences":   [...],
            "formations":    [...],
            "certifications":[...],
            "projets":       [...],
        },
        "metadata": {
            "template_id": int,
            "score_ats":   int,
            "generated_at": str,
        }
    }
    """

    # ── Agent 6 — Email Composer ──────────────────────────────
    email_draft: Optional[dict]
    """
    Email de candidature ou relance rédigé par le LLM :
    {
        "objet": str,
        "corps": str,
        "type":  "candidature" | "relance",
        "langue": "fr" | "en"
    }
    """

    email_type: str
    """Type d'email à générer : "candidature" ou "relance"."""

    # ── Suivi candidature ─────────────────────────────────────
    statut_candidature: str
    """
    Statut courant de la candidature :
    "EN_ATTENTE" | "ENVOYE" | "VU" | "REPONDU" | "RELANCE"
    """

    # ── Routeur ──────────────────────────────────────────────
    next_agent: Optional[str]

    # ── Méta-données ─────────────────────────────────────────
    pipeline_version: str
