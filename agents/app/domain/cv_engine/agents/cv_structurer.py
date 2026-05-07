# ============================================================
# app/domain/cv_engine/agents/cv_structurer.py
# Node 3 — CV Structurer (Algorithme pur, SANS LLM)
#
# Transforme le profil brut + compétences optimisées dans le
# schéma JSON exact attendu par QuestPDF côté .NET.
#
# Entrée  : raw_profile, optimized_skills, ats_coverage,
#           offer_data, match_result, template_slug
# Sortie  : cv_json (dict prêt pour QuestPDF)
# LLM     : aucun — transformation algorithmique
# ============================================================
import logging
from datetime import datetime
from langchain_core.messages import AIMessage
from app.domain.cv_engine.schemas.state import CvEngineState

logger = logging.getLogger(__name__)


def _build_candidate(profile: dict) -> dict:
    """Construit la section Candidate du CV."""
    nom = profile.get("nom", "")
    prenom = profile.get("prenom", "")
    name = f"{prenom} {nom}".strip()
    
    return {
        "Name":      name,
        "Email":     profile.get("email", ""),
        "Phone":     profile.get("telephone", ""),
        "Location":  f"{profile.get('ville', '')}, {profile.get('pays', '')}".strip(", "),
        "LinkedIn":  profile.get("lien_linkedin", ""),
        "GitHub":    profile.get("lien_github", ""),
        "Portfolio": profile.get("lien_portfolio", ""),
    }


def _build_experience(profile: dict) -> list[dict]:
    """Formate les expériences pour CvData.Experience."""
    def split_bullets(text: str) -> list[str]:
        if not text: return []
        return [b.strip("-•\r\n ") for b in text.split("\n") if b.strip()]

    return [
        {
            "Role":    e.get("titre", ""),
            "Company": e.get("entreprise", ""),
            "Start":   e.get("date_debut", ""),
            "End":     e.get("date_fin", ""),
            "Bullets": split_bullets(e.get("description", "")),
        }
        for e in (profile.get("experiences", []) or [])
        if isinstance(e, dict)
    ]


def _build_education(profile: dict) -> list[dict]:
    """Formate les formations pour CvData.Education."""
    return [
        {
            "Degree":      f.get("diplome", ""),
            "Institution": f.get("etablissement", ""),
            "Year":        str(f.get("annee", "")) + (f" - {f.get('annee_fin')}" if f.get("annee_fin") else ""),
        }
        for f in (profile.get("formations", []) or [])
        if isinstance(f, dict)
    ]


def _build_certifications(profile: dict) -> list[str]:
    """Formate les certifications pour CvData.Certifications."""
    return [
        f"{c.get('nom', '')} — {c.get('organisme', '')}".strip(" —")
        for c in (profile.get("certifications", []) or [])
        if isinstance(c, dict)
    ]


def _build_projects(profile: dict) -> list[dict]:
    """Formate les projets pour CvData.Projects."""
    def split_bullets(text: str) -> list[str]:
        if not text: return []
        return [b.strip("-•\r\n ") for b in text.split("\n") if b.strip()]

    return [
        {
            "Title":       p.get("titre", ""),
            "Description": p.get("technologies", ""),
            "Bullets":     split_bullets(p.get("description", "")),
        }
        for p in (profile.get("projets", []) or [])
        if isinstance(p, dict)
    ]


async def cv_structurer_node(state: CvEngineState) -> dict:
    """
    Nœud LangGraph — Node 3 : Construction du JSON CV pour QuestPDF.

    ┌──────────────────────────────────────────────────────────┐
    │  Entrées state  │  raw_profile, optimized_skills,       │
    │                 │  ats_coverage, offer_data, match_result│
    │                 │  template_slug                         │
    │  Sorties state  │  cv_json, messages                     │
    │  LLM            │  aucun — structuration algorithmique   │
    └──────────────────────────────────────────────────────────┘

    Structure de cv_json :
    {
        "sections": {
            "entete":        {...},
            "resume":        str,
            "competences":   [...],   # Already optimized by Node 2
            "experiences":   [...],
            "formations":    [...],
            "certifications":[...],
            "projets":       [...]
        },
        "metadata": {
            "template_slug":    str,
            "score_ats":        int,
            "score_matching":   int,
            "ats_coverage_pct": float,
            "poste_vise":       str,
            "entreprise":       str,
            "generated_at":     str (ISO)
        }
    }
    """
    logger.info("📄 CvEngine Node 3 [CV Structurer] — Démarrage")

    profile         = state.get("raw_profile")       or {}
    optimized_skills = state.get("optimized_skills") or []
    ats_coverage    = state.get("ats_coverage")      or {}
    offer           = state.get("offer_data")        or {}
    match_result    = state.get("match_result")      or {}
    template_slug   = state.get("template_slug", "modern")

    cv_json = {
        "Candidate":      _build_candidate(profile),
        "Summary":        profile.get("resume") or profile.get("resume_professionnel", ""),
        "Skills":         [
            {"Name": s["nom"], "Level": s.get("niveau", 1), "IsMatched": s.get("matched", False)}
            for s in optimized_skills
        ],
        "Experience":     _build_experience(profile),
        "Education":      _build_education(profile),
        "Certifications": _build_certifications(profile),
        "Projects":       _build_projects(profile),
        "Languages":      [],
        "Activities":     [],
        
        # AI Metadata
        "AtsScore":       match_result.get("score_ats", 0),
        "MatchingScore":  match_result.get("score_matching", 0),
        "AtsCoveragePct": ats_coverage.get("percentage", 0.0),
    }

    nb_comp = len(cv_json["Skills"])
    nb_exp  = len(cv_json["Experience"])
    nb_form = len(cv_json["Education"])
    nb_proj = len(cv_json["Projects"])
    nb_cert = len(cv_json["Certifications"])

    summary = (
        f"[CvEngine:CvStructurer] CV structuré (C# Schema) — template={template_slug} — "
        f"{nb_comp} skills, {nb_exp} experiences, "
        f"{nb_form} education, {nb_proj} projects, {nb_cert} certifications — "
        f"ATS: {match_result.get('score_ats', 0)}% | "
        f"Matching: {match_result.get('score_matching', 0)}%"
    )
    logger.info("Node 3 ✅ — %s", summary)

    return {
        "cv_json": cv_json,
        "messages": [AIMessage(content=summary, name="cv_structurer")],
    }
