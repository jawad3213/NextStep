# ============================================================
# app/domain/offer/agents/normalizer/agent.py
# Agent 3 — Normalisateur (Algorithme pur, SANS LLM)
#
# Nettoie accents, synonymes techniques, mise en minuscules.
# Prépare les données pour une comparaison équitable (Agent 4).
# ============================================================
import re
import logging
from unidecode import unidecode
from langchain_core.messages import AIMessage
from app.domain.offer.schemas.state import OfferState
from app.domain.offer.agents.normalizer.synonyms import SYNONYMES

logger = logging.getLogger(__name__)


def _normalize_token(token: str) -> str:
    """
    Normalise un token individuel :
      1. unidecode (supprime accents : é→e, ç→c, ü→u…)
      2. lower()
      3. supprime caractères non significatifs (sauf . / # - +)
      4. remplace via le dictionnaire de synonymes
    """
    token = unidecode(token.strip().lower())
    token = re.sub(r"[^\w\s./#\-+]", "", token)
    return SYNONYMES.get(token, token)


def normalize_skills(skills: list[str]) -> list[str]:
    """
    Normalise et déduplique une liste de compétences.
    Gère le découpage automatique (ex: "JS / TS" -> ["javascript", "typescript"])
    """
    seen: set[str] = set()
    result = []

    # Séparateurs courants dans les CV/Offres
    separators = r"[,/&|]"

    for s in skills:
        tokens = re.split(separators, s)
        for token in tokens:
            n = _normalize_token(token)
            if n and n not in seen:
                seen.add(n)
                result.append(n)
    return result


def normalize_text(text: str) -> str:
    """
    Normalise un texte libre (résumé, description, titre…).
    Utilisé pour le calcul du score ATS positionnel.
    """
    text = unidecode(text.lower())
    return re.sub(r"[^\w\s./#\-+]", " ", text).strip()


async def normalizer_node(state: OfferState) -> dict:
    """
    Nœud LangGraph — Agent 3 : Normalisation des compétences et textes.

    ┌────────────────────────────────────────────────────────────┐
    │  Entrées state  │  analyzed_offer, profile_data           │
    │  Sorties state  │  normalized_offer_skills (Annotated +)  │
    │                 │  normalized_profile_skills (Annotated +)│
    │                 │  normalized_keywords (Annotated +)      │
    │                 │  profile_full_text, messages             │
    │  LLM            │  aucun — algorithme pur                 │
    └────────────────────────────────────────────────────────────┘
    """
    logger.info("🔧 Agent 3 [Normalizer] — Démarrage")

    offer   = state.get("analyzed_offer") or {}
    profile = state.get("profile_data")   or {}

    # ── Normalisation des compétences de l'offre ──────────────
    offer_skills   = normalize_skills(offer.get("competences_requises", []))
    offer_optional = normalize_skills(offer.get("competences_souhaitees", []))
    keywords       = normalize_skills(offer.get("keywords_ats", []))

    # ── Normalisation des compétences du profil ───────────────
    profile_skills = normalize_skills([
        c["nom"] for c in profile.get("competences", [])
        if isinstance(c, dict) and c.get("nom")
    ])

    # ── Construction du texte complet normalisé du profil ─────
    profile_titre  = normalize_text(profile.get("titre") or "")
    profile_resume = normalize_text(profile.get("resume") or "")

    exp_text = normalize_text(" ".join(
        f"{e.get('titre', '')} {e.get('description', '')}"
        for e in profile.get("experiences", [])
        if isinstance(e, dict)
    ))
    proj_text = normalize_text(" ".join(
        f"{p.get('titre', '')} {p.get('description', '')} "
        f"{' '.join(p.get('technologies') or [])}"
        for p in profile.get("projets", [])
        if isinstance(p, dict)
    ))
    cert_text = normalize_text(" ".join(
        c.get("nom", "") for c in profile.get("certifications", [])
        if isinstance(c, dict)
    ))
    form_text = normalize_text(" ".join(
        f"{f.get('diplome', '')} {f.get('etablissement', '')}"
        for f in profile.get("formations", [])
        if isinstance(f, dict)
    ))

    profile_full_text = " ".join(filter(None, [
        profile_titre, profile_resume,
        " ".join(profile_skills),
        exp_text, proj_text, cert_text, form_text,
    ]))

    logger.info(
        "Agent 3 ✅ — %d comp. offre | %d comp. optionnelles | "
        "%d comp. profil | %d keywords ATS normalisés",
        len(offer_skills), len(offer_optional),
        len(profile_skills), len(keywords),
    )

    summary = (
        f"[Agent 3] Normalisation terminée : "
        f"{len(offer_skills)} compétences requises, "
        f"{len(profile_skills)} compétences profil, "
        f"{len(keywords)} keywords ATS"
    )
    return {
        "normalized_offer_skills":   offer_skills,
        "normalized_profile_skills": profile_skills,
        "normalized_keywords":       keywords,
        "profile_full_text":         profile_full_text,
        "messages": [AIMessage(content=summary, name="normalizer")],
    }
