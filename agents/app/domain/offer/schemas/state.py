# ============================================================
# app/domain/offer/schemas/state.py
#
# État LangGraph du domaine OFFER (Agents 1-4).
#
# Champs Annotated + operator.add → accumulés (pas remplacés).
# Tous les autres champs → remplacés à chaque mise à jour.
# ============================================================
import operator
from typing import Annotated, Optional, TypedDict
from langchain_core.messages import BaseMessage
from langgraph.graph.message import add_messages


class OfferState(TypedDict, total=False):
    """
    État unique circulant entre les nœuds du graphe OFFER.

    Cycle de vie :
        START (raw_offer_text, user_id, template_id)
          → Agent 1 : analyzed_offer enrichi
          → Agent 2 : profile_data enrichi
          → Agent 3 : normalized_* enrichi
          → Agent 4 : match_result enrichi
          → Agent 5 : cv_template_json enrichi  [stub M3]
          → Agent 6 : email_draft enrichi        [stub M4]
          → END

    ┌──────────────────────────────────────────────────────────┐
    │  Champ                    │  Type          │  Reducer    │
    ├──────────────────────────────────────────────────────────┤
    │  messages                 │  list[Msg]     │ add_messages│
    │  errors                   │  list[str]     │ operator.add│
    │  normalized_offer_skills  │  list[str]     │ operator.add│
    │  normalized_profile_skills│  list[str]     │ operator.add│
    │  normalized_keywords      │  list[str]     │ operator.add│
    │  raw_offer_text           │  str           │  replace    │
    │  user_id                  │  str           │  replace    │
    │  template_id              │  int           │  replace    │
    │  analyzed_offer           │  dict | None   │  replace    │
    │  profile_data             │  dict | None   │  replace    │
    │  profile_full_text        │  str           │  replace    │
    │  match_result             │  dict | None   │  replace    │
    │  cv_template_json         │  dict | None   │  replace    │
    │  email_draft              │  dict | None   │  replace    │
    │  next_agent               │  str  | None   │  replace    │
    │  pipeline_version         │  str           │  replace    │
    └──────────────────────────────────────────────────────────┘
    """

    # ── Accumulatifs (reducer LangGraph) ──────────────────────
    messages: Annotated[list[BaseMessage], add_messages]
    """Historique des messages des agents — jamais écrasé."""

    errors: Annotated[list[str], operator.add]
    """Erreurs accumulées tout au long du pipeline."""

    normalized_offer_skills: Annotated[list[str], operator.add]
    """Compétences requises de l'offre — normalisées par Agent 3."""

    normalized_profile_skills: Annotated[list[str], operator.add]
    """Compétences du profil candidat — normalisées par Agent 3."""

    normalized_keywords: Annotated[list[str], operator.add]
    """Mots-clés ATS normalisés — utilisés par Agent 4 pour le score ATS."""

    # ── Entrées initiales (fournies au START) ─────────────────
    raw_offer_text: str
    """Texte brut de l'offre d'emploi (copié-collé par l'utilisateur)."""

    user_id: str
    """Identifiant Keycloak de l'utilisateur connecté."""

    template_id: int
    """ID du template CV sélectionné (1=Modern, 2=Classic, 3=Creative)."""

    # ── Agent 1 — Offer Analyzer (LLM) ───────────────────────
    analyzed_offer: Optional[dict]
    """
    JSON structuré produit par le LLM :
    {
        "titre": str,
        "entreprise": str | null,
        "type_contrat": "CDI|CDD|Stage|Alternance|Freelance|null",
        "localisation": str | null,
        "competences_requises": list[str],
        "competences_souhaitees": list[str],
        "keywords_ats": list[str],      # 10-20 mots-clés ATS
        "annees_experience": int | null,
        "niveau_etudes": str | null,
        "description_poste": str
    }
    """

    # ── Agent 2 — Profile Retriever (DB) ─────────────────────
    profile_data: Optional[dict]
    """
    Profil complet du candidat depuis PostgreSQL :
    {
        "user_id": str,
        "nom": str, "prenom": str,
        "titre": str, "resume": str,
        "telephone": str, "ville": str,
        "competences": [{"nom": str, "niveau": int}],
        "experiences": [{"titre": str, "entreprise": str, ...}],
        "formations": [{"diplome": str, "etablissement": str, "annee": int}],
        "certifications": [{"nom": str, "organisme": str}],
        "projets": [{"titre": str, "description": str, "technologies": list[str]}]
    }
    """

    # ── Agent 3 — Normalizer (Algorithme) ────────────────────
    profile_full_text: str
    """Texte complet normalisé du profil (titre + résumé + compétences + expériences)."""

    # ── Agent 4 — Scorer (Calcul mathématique) ───────────────
    match_result: Optional[dict]
    """
    Scores et recommandations :
    {
        "score_matching": int,        # 0-100 (Jaccard pondéré 70/30)
        "score_ats": int,             # 0-100 (pondération positionnelle)
        "keywords_presents": list[str],
        "keywords_manquants": list[str],
        "recommandations": list[str],
        "competences_matching": list[str],
        "competences_manquantes": list[str]
    }
    """

    # ── Agent 5 — CV Formatter (stub M3) ─────────────────────
    cv_template_json: Optional[dict]
    """Données formatées pour QuestPDF (implémenté par M3)."""

    # ── Agent 6 — Email Composer (stub M4) ───────────────────
    email_draft: Optional[dict]
    """Email de candidature rédigé (implémenté par M4)."""

    # ── Routeur ──────────────────────────────────────────────
    next_agent: Optional[str]
    """
    Nom du prochain nœud décidé par le router :
    "offer_analyzer" | "profile_retriever" | "normalizer" |
    "scorer" | "cv_formatter" | "email_composer" | "end"
    """

    # ── Méta-données ─────────────────────────────────────────
    pipeline_version: str
    """Version du pipeline (ex: "2.1")."""
