# ============================================================
# app/domain/company/agents/company_analyzer/agent.py
# Agent Company — Analyse entreprise (Algorithme pur)
# ============================================================
import logging
from langchain_core.messages import AIMessage
from app.domain.company.schemas.state import CompanyState
from app.domain.company.agents.company_analyzer.signals import (
    detect_company_size,
    detect_remote_policy,
)
from app.domain.offer.agents.normalizer import normalize_text

logger = logging.getLogger(__name__)


def _compute_culture_score(offer: dict, profile: dict) -> tuple[int, list[str]]:
    """
    Calcule le score de compatibilité culture (0-100) + insights.

    Critères de scoring :
      +10 pts : full remote
      + 5 pts : politique hybride
      + 5 pts : environnement startup
      +0-20 pts : alignement stack technique (proportionnel)
      + 5 pts : type contrat = stage/alternance
    """
    score    = 50  # Base neutre
    insights: list[str] = []

    type_contrat = (offer.get("type_contrat") or "").lower()
    desc         = offer.get("description_poste") or ""
    remote       = detect_remote_policy(desc)
    taille       = detect_company_size(desc)
    keywords     = [k.lower() for k in offer.get("keywords_ats", [])]
    profile_comp = [
        c.get("nom", "").lower()
        for c in profile.get("competences", [])
        if isinstance(c, dict)
    ]

    # ── Remote policy ──────────────────────────────────────────
    if remote == "full_remote":
        score += 10
        insights.append("Cette offre propose le full remote — idéal pour la flexibilité")
    elif remote == "hybride":
        score += 5
        insights.append("Politique hybride — bon équilibre télétravail / présentiel")
    elif remote == "presentiel":
        insights.append("Poste 100% en présentiel — proximité avec les équipes")

    # ── Taille entreprise ──────────────────────────────────────
    if taille == "startup":
        score += 5
        insights.append("Environnement startup — autonomie et polyvalence valorisées")
    elif taille == "grand_groupe":
        insights.append("Grand groupe — processus structurés et évolution claire")
    else:
        insights.append("PME — proximité hiérarchique et impact direct")

    # ── Alignement stack technique ─────────────────────────────
    matching_kw = [k for k in keywords if k in profile_comp]
    alignment   = int(len(matching_kw) / len(keywords) * 20) if keywords else 0
    score      += alignment
    if alignment > 10:
        insights.append(f"Stack technique alignée : {', '.join(matching_kw[:3])}")
    elif alignment > 0:
        insights.append("Alignement partiel avec la stack technique de l'entreprise")

    # ── Stage / Alternance ─────────────────────────────────────
    if type_contrat in ("stage", "alternance"):
        score += 5
        insights.append(f"{type_contrat.capitalize()} — opportunité idéale pour acquérir de l'expérience")

    return min(max(score, 0), 100), insights


async def company_analyzer_node(state: CompanyState) -> dict:
    """
    Nœud LangGraph — Agent Company : Analyse de l'entreprise.

    ┌────────────────────────────────────────────────────────────┐
    │  Entrées state  │  company_name, offer_data, profile_data │
    │  Sorties state  │  company_info, company_culture_score    │
    │                 │  company_insights, messages              │
    │  LLM            │  aucun — analyse algorithmique          │
    └────────────────────────────────────────────────────────────┘
    """
    logger.info("🏢 Agent Company [Analyzer] — Démarrage")

    offer   = state.get("offer_data")   or {}
    profile = state.get("profile_data") or {}
    name    = state.get("company_name") or offer.get("entreprise") or "Entreprise inconnue"
    desc    = offer.get("description_poste") or ""

    culture_score, insights = _compute_culture_score(offer, profile)

    company_info = {
        "nom":                   name,
        "secteur":               None,
        "taille":                detect_company_size(desc),
        "localisation":          offer.get("localisation"),
        "description":           desc,
        "technologies_stack":    offer.get("keywords_ats", []),
        "type_contrat_dominant": offer.get("type_contrat"),
        "remote_policy":         detect_remote_policy(desc),
    }

    logger.info(
        "Agent Company ✅ — '%s' | score culture=%d%% | %d insights",
        name, culture_score, len(insights),
    )

    summary = f"[Agent Company] '{name}' analysée | score culture={culture_score}%"
    return {
        "company_info":          company_info,
        "company_culture_score": culture_score,
        "company_insights":      insights,
        "messages": [AIMessage(content=summary, name="company_analyzer")],
    }
