# ============================================================
# app/domain/job/agents/cv_formatter.py
# Agent 5 — CV Formatter (Algorithme pur, SANS LLM)
#
# Organise les données profil + offre dans le schéma JSON
# attendu par QuestPDF côté .NET (M3).
#
# Entrée  : profile_data, offer_data, match_result, template_id
# Sortie  : cv_template_json (dict prêt pour QuestPDF)
# ============================================================
import logging
from datetime import datetime
from langchain_core.messages import AIMessage
from app.domain.job.schemas.state import JobState

logger = logging.getLogger(__name__)


def _build_entete(profile: dict) -> dict:
    """Construit la section en-tête du CV."""
    return {
        "nom":       profile.get("nom", ""),
        "prenom":    profile.get("prenom", ""),
        "titre":     profile.get("titre", ""),
        "email":     profile.get("email", ""),
        "telephone": profile.get("telephone", ""),
        "ville":     profile.get("ville", ""),
        "linkedin":  profile.get("lien_linkedin", ""),
        "github":    profile.get("lien_github", ""),
        "portfolio": profile.get("lien_portfolio", ""),
    }


def _build_competences(profile: dict, match_result: dict) -> list[dict]:
    """
    Trie les compétences : celles qui matchent l'offre en premier.
    Ajoute un flag `matched` pour que le template puisse les mettre en valeur.
    """
    matching = set(match_result.get("competences_matching", []))
    comps = profile.get("competences", []) or []

    sorted_comps = sorted(
        comps,
        key=lambda c: (0 if c.get("nom", "").lower() in matching else 1),
    )
    return [
        {
            "nom":     c.get("nom", ""),
            "niveau":  c.get("niveau", 1),
            "matched": c.get("nom", "").lower() in matching,
        }
        for c in sorted_comps
        if isinstance(c, dict) and c.get("nom")
    ]


def _build_experiences(profile: dict) -> list[dict]:
    return [
        {
            "titre":      e.get("titre", ""),
            "entreprise": e.get("entreprise", ""),
            "date_debut": e.get("date_debut", ""),
            "date_fin":   e.get("date_fin", ""),
            "description":e.get("description", ""),
        }
        for e in (profile.get("experiences", []) or [])
        if isinstance(e, dict)
    ]


def _build_formations(profile: dict) -> list[dict]:
    return [
        {
            "diplome":       f.get("diplome", ""),
            "etablissement": f.get("etablissement", ""),
            "annee":         f.get("annee"),
        }
        for f in (profile.get("formations", []) or [])
        if isinstance(f, dict)
    ]


def _build_projets(profile: dict) -> list[dict]:
    return [
        {
            "titre":        p.get("titre", ""),
            "description":  p.get("description", ""),
            "technologies": p.get("technologies", []) or [],
        }
        for p in (profile.get("projets", []) or [])
        if isinstance(p, dict)
    ]


async def cv_formatter_node(state: JobState) -> dict:
    """
    Nœud LangGraph — Agent 5 : Construction du JSON CV pour QuestPDF.

    ┌────────────────────────────────────────────────────────────┐
    │  Entrées state  │  profile_data, offer_data, match_result │
    │                 │  template_id                            │
    │  Sorties state  │  cv_template_json, messages             │
    │  LLM            │  aucun — formatage algorithmique        │
    └────────────────────────────────────────────────────────────┘

    Structure de cv_template_json :
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
            "template_id": int,
            "score_ats": int,
            "score_matching": int,
            "poste_vise": str,
            "entreprise": str,
            "generated_at": str (ISO)
        }
    }
    """
    logger.info("📄 Agent 5 [CV Formatter] — Démarrage")

    profile      = state.get("profile_data")  or {}
    offer        = state.get("offer_data")    or {}
    match_result = state.get("match_result")  or {}
    template_id  = state.get("template_id", 1)

    cv_json = {
        "sections": {
            "entete":        _build_entete(profile),
            "resume":        profile.get("resume") or profile.get("resume_professionnel", ""),
            "competences":   _build_competences(profile, match_result),
            "experiences":   _build_experiences(profile),
            "formations":    _build_formations(profile),
            "certifications": [
                {"nom": c.get("nom", ""), "organisme": c.get("organisme", "")}
                for c in (profile.get("certifications", []) or [])
                if isinstance(c, dict)
            ],
            "projets":       _build_projets(profile),
        },
        "metadata": {
            "template_id":    template_id,
            "score_ats":      match_result.get("score_ats", 0),
            "score_matching": match_result.get("score_matching", 0),
            "poste_vise":     offer.get("titre", ""),
            "entreprise":     offer.get("entreprise", ""),
            "generated_at":   datetime.utcnow().isoformat(),
        },
    }

    nb_comp = len(cv_json["sections"]["competences"])
    nb_exp  = len(cv_json["sections"]["experiences"])
    logger.info("Agent 5 ✅ — %d compétences | %d expériences | template=%d", nb_comp, nb_exp, template_id)

    summary = (
        f"[Agent 5] CV formatté : {nb_comp} compétences, "
        f"{nb_exp} expériences, template={template_id}"
    )
    return {
        "cv_template_json": cv_json,
        "messages": [AIMessage(content=summary, name="cv_formatter")],
    }
