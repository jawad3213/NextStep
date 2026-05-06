# ============================================================
# app/domain/job/agents/cv_formatter/agent.py
# Agent 5 — CV Formatter (Algorithme pur, SANS LLM)
# ============================================================
import logging
from datetime import datetime
from langchain_core.messages import AIMessage
from app.domain.job.schemas.state import JobState
from app.domain.job.agents.cv_formatter.builder import (
    build_entete,
    build_competences,
    build_experiences,
    build_formations,
    build_projets,
)

logger = logging.getLogger(__name__)


async def cv_formatter_node(state: JobState) -> dict:
    """
    Nœud LangGraph — Agent 5 : Construction du JSON CV pour QuestPDF.

    ┌────────────────────────────────────────────────────────────┐
    │  Entrées state  │  profile_data, offer_data, match_result │
    │                 │  template_id                            │
    │  Sorties state  │  cv_template_json, messages             │
    │  LLM            │  aucun — formatage algorithmique        │
    └────────────────────────────────────────────────────────────┘
    """
    logger.info("📄 Agent 5 [CV Formatter] — Démarrage")

    profile      = state.get("profile_data")  or {}
    offer        = state.get("offer_data")    or {}
    match_result = state.get("match_result")  or {}
    template_id  = state.get("template_id", 1)

    cv_json = {
        "sections": {
            "entete":        build_entete(profile),
            "resume":        profile.get("resume") or profile.get("resume_professionnel", ""),
            "competences":   build_competences(profile, match_result),
            "experiences":   build_experiences(profile),
            "formations":    build_formations(profile),
            "certifications": [
                {"nom": c.get("nom", ""), "organisme": c.get("organisme", "")}
                for c in (profile.get("certifications", []) or [])
                if isinstance(c, dict)
            ],
            "projets": build_projets(profile),
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
