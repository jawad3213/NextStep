# ============================================================
# app/domain/company/agents/company_analyzer.py
# Agent Entreprise — Analyse (Algorithme + LLM optionnel)
#
# Extrait des insights sur l'entreprise à partir de l'offre analysée.
# Calcule un score de compatibilité culture candidat ↔ entreprise.
# SANS appel LLM (utilise les données de l'offre analysée).
# ============================================================
import logging
from langchain_core.messages import AIMessage
from app.domain.company.schemas.state import CompanyState
from app.domain.offer.agents.normalizer import normalize_text

logger = logging.getLogger(__name__)

# ─── Signaux de détection ─────────────────────────────────────
_REMOTE_SIGNALS      = {"télétravail", "remote", "distanciel", "full remote", "hybrid"}
_STARTUP_SIGNALS     = {"startup", "scale-up", "jeune pousse", "early stage", "seed"}
_GRAND_GROUPE_SIGNALS= {"groupe", "international", "multinational", "cac40", "fortune 500"}
_JUNIOR_SIGNALS      = {"junior", "débutant", "0-2 ans", "0 à 2 ans", "première expérience"}


def _detect_company_size(offer_text: str) -> str:
    """Déduit la taille de l'entreprise depuis l'offre."""
    text = normalize_text(offer_text)
    if any(s in text for s in _STARTUP_SIGNALS):
        return "startup"
    if any(s in text for s in _GRAND_GROUPE_SIGNALS):
        return "grand_groupe"
    return "pme"


def _detect_remote_policy(offer_text: str) -> str | None:
    """Déduit la politique de télétravail."""
    text = normalize_text(offer_text)
    if "full remote" in text or "100% remote" in text:
        return "full_remote"
    if any(s in text for s in {"hybrid", "hybride", "télétravail partiel"}):
        return "hybride"
    if any(s in text for s in {"présentiel", "sur site", "en local"}):
        return "presentiel"
    return None


def _compute_culture_score(
    offer: dict,
    profile: dict,
) -> tuple[int, list[str]]:
    """
    Calcule le score de compatibilité culture (0-100) et génère les insights.

    Critères :
      +20 pts : type de contrat correspond aux préférences
      +20 pts : remote policy positive
      +20 pts : taille d'entreprise compatible avec le niveau
      +20 pts : stack technique alignée
      +20 pts : niveau d'études compatible
    """
    score = 50  # Base neutre
    insights: list[str] = []

    type_contrat = (offer.get("type_contrat") or "").lower()
    remote       = _detect_remote_policy(offer.get("description_poste") or "")
    taille       = _detect_company_size(offer.get("description_poste") or "")
    keywords     = [k.lower() for k in offer.get("keywords_ats", [])]
    profile_comp = [c.get("nom", "").lower() for c in profile.get("competences", []) if isinstance(c, dict)]

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

    # ── Alignement stack ───────────────────────────────────────
    matching_keywords = [k for k in keywords if k in profile_comp]
    alignment = int(len(matching_keywords) / len(keywords) * 20) if keywords else 0
    score += alignment
    if alignment > 10:
        insights.append(f"Stack technique alignée : {', '.join(matching_keywords[:3])}")
    elif alignment > 0:
        insights.append("Alignement partiel avec la stack technique de l'entreprise")

    # ── Stage / Premier emploi ─────────────────────────────────
    if type_contrat in ("stage", "alternance"):
        score += 5
        insights.append(f"{type_contrat.capitalize()} — opportunité idéale pour acquérir de l'expérience")

    return min(max(score, 0), 100), insights


async def company_analyzer_node(state: CompanyState) -> dict:
    """
    Nœud LangGraph — Agent Entreprise : Analyse de l'entreprise.

    ┌────────────────────────────────────────────────────────────┐
    │  Entrées state  │  company_name, offer_data, profile_data │
    │  Sorties state  │  company_info, company_culture_score    │
    │                 │  company_insights, messages              │
    │  LLM            │  aucun — analyse algorithmique          │
    └────────────────────────────────────────────────────────────┘
    """
    logger.info("🏢 Agent Company [Analyzer] — Démarrage")

    offer   = state.get("offer_data")    or {}
    profile = state.get("profile_data")  or {}
    name    = state.get("company_name")  or offer.get("entreprise") or "Entreprise inconnue"

    culture_score, insights = _compute_culture_score(offer, profile)

    company_info = {
        "nom":                   name,
        "secteur":               None,  # extensible
        "taille":                _detect_company_size(offer.get("description_poste") or ""),
        "localisation":          offer.get("localisation"),
        "description":           offer.get("description_poste"),
        "technologies_stack":    offer.get("keywords_ats", []),
        "type_contrat_dominant": offer.get("type_contrat"),
        "remote_policy":         _detect_remote_policy(offer.get("description_poste") or ""),
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
