# ============================================================
# app/domain/cv_engine/schemas/state.py
# État LangGraph du domaine CV_ENGINE
#
# Pipeline :
#   START → router → profile_loader  → router
#                  → skill_optimizer → router
#                  → cv_structurer   → router → END
# ============================================================
import operator
from typing import Annotated, Optional, TypedDict
from langchain_core.messages import BaseMessage
from langgraph.graph.message import add_messages


class CvEngineState(TypedDict, total=False):
    """
    État circulant dans le pipeline CV_ENGINE.

    Le domaine CV_ENGINE prépare les données structurées pour le
    moteur QuestPDF côté .NET. Il charge le profil, optimise les
    compétences en fonction de l'offre (si fournie), et structure
    le tout dans le schéma JSON attendu par les templates.

    ┌──────────────────────────────────────────────────────────┐
    │  Champ               │  Type           │  Reducer       │
    ├──────────────────────────────────────────────────────────┤
    │  messages            │  list[Msg]      │ add_messages   │
    │  errors              │  list[str]      │ operator.add   │
    │  user_id             │  str            │  replace       │
    │  template_slug       │  str            │  replace       │
    │  offer_data          │  dict | None    │  replace       │
    │  match_result        │  dict | None    │  replace       │
    │  raw_profile         │  dict | None    │  replace       │
    │  optimized_skills    │  list[dict]|None│  replace       │
    │  ats_coverage        │  dict | None    │  replace       │
    │  cv_json             │  dict | None    │  replace       │
    │  next_node           │  str  | None    │  replace       │
    │  pipeline_version    │  str            │  replace       │
    └──────────────────────────────────────────────────────────┘
    """

    # ── Accumulatifs ──────────────────────────────────────────
    messages: Annotated[list[BaseMessage], add_messages]
    """Historique des messages agents."""

    errors: Annotated[list[str], operator.add]
    """Erreurs accumulées."""

    # ── Entrées initiales ─────────────────────────────────────
    user_id: str
    """Identifiant Keycloak de l'utilisateur."""

    template_slug: str
    """Slug du template CV sélectionné (e.g. 'modern', 'classic')."""

    offer_data: Optional[dict]
    """
    Offre analysée (provient du domaine OFFER — Agent 1).
    Optionnel : si None, le CV est généré sans ciblage d'offre.
    {
        "titre": str,
        "entreprise": str,
        "competences_requises": list[str],
        "keywords_ats": list[str],
        ...
    }
    """

    match_result: Optional[dict]
    """
    Résultat du scoring (provient du domaine OFFER — Agent 4).
    Optionnel : si None, les compétences sont triées par niveau.
    {
        "score_matching": int,
        "score_ats": int,
        "competences_matching": list[str],
        "competences_manquantes": list[str],
        "keywords_presents": list[str],
        "keywords_manquants": list[str],
        ...
    }
    """

    # ── Node 1 — Profile Loader ──────────────────────────────
    raw_profile: Optional[dict]
    """
    Profil complet du candidat chargé depuis PostgreSQL :
    {
        "user_id": str,
        "nom": str, "prenom": str,
        "titre": str, "resume": str,
        "email": str, "telephone": str, "ville": str,
        "lien_linkedin": str, "lien_github": str, "lien_portfolio": str,
        "competences": [{"nom": str, "niveau": int, "type_competence": str}],
        "experiences": [{"titre": str, "entreprise": str, ...}],
        "formations": [{"diplome": str, "etablissement": str, "annee": int}],
        "certifications": [{"nom": str, "organisme": str}],
        "projets": [{"titre": str, "description": str, "technologies": str}]
    }
    """

    # ── Node 2 — Skill Optimizer ─────────────────────────────
    optimized_skills: Optional[list[dict]]
    """
    Compétences triées et enrichies :
    [
        {"nom": str, "niveau": int, "type_competence": str, "matched": bool},
        ...
    ]
    Triées : matchées en premier, puis par niveau décroissant.
    """

    ats_coverage: Optional[dict]
    """
    Couverture ATS calculée :
    {
        "covered": int,
        "total": int,
        "percentage": float,
        "keywords_present": list[str],
        "keywords_missing": list[str]
    }
    """

    # ── Node 3 — CV Structurer ───────────────────────────────
    cv_json: Optional[dict]
    """
    JSON final structuré pour QuestPDF :
    {
        "sections": {
            "entete": {...},
            "resume": str,
            "competences": [...],
            "experiences": [...],
            "formations": [...],
            "certifications": [...],
            "projets": [...]
        },
        "metadata": {
            "template_slug": str,
            "score_ats": int,
            "score_matching": int,
            "ats_coverage_pct": float,
            "poste_vise": str,
            "entreprise": str,
            "generated_at": str
        }
    }
    """

    # ── Routeur ──────────────────────────────────────────────
    next_node: Optional[str]
    """
    Nom du prochain nœud décidé par le router :
    "profile_loader" | "skill_optimizer" | "cv_structurer" | "end"
    """

    # ── Méta-données ─────────────────────────────────────────
    pipeline_version: str
    """Version du pipeline CV Engine (e.g. '1.0')."""
