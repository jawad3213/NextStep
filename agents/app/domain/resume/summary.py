# ============================================================
# app/domain/resume/summary.py
# Writes the "résumé professionnel" of a profile (POST /generate-resume).
#
# Input: the profile as the frontend holds it (personal, experience, education,
# skills, projets, certifications). Only facts from the profile are given to the LLM.
# ============================================================
import json
import logging
from typing import Any

from langchain_core.prompts import ChatPromptTemplate

from app.core.config import get_llm

logger = logging.getLogger(__name__)

_MAX_ITEMS = 6
_MAX_TEXT = 300

SYSTEM_PROMPT_EN = """\
You write the professional summary ("Profile" section) of a resume, in English.

Rules:
- 3 to 4 sentences, 60 to 90 words, first person without repeating "I" every sentence.
- Only facts present in the provided data: never invent a company, a number, a skill or a
  number of years of experience.
- Highlight the targeted role, the strongest experiences and skills, and what the candidate
  brings.
- Reply ONLY with the summary text: no title, no quotes, no markdown.
"""

SYSTEM_PROMPT_FR = """\
Tu rédiges le résumé professionnel (section "Profil") d'un CV, en français.

Règles :
- 3 à 4 phrases, 60 à 90 mots, à la première personne sans "je" répété à chaque phrase.
- Uniquement des faits présents dans les données fournies : n'invente aucune entreprise,
  aucun chiffre, aucune compétence, aucune année d'expérience.
- Mets en avant le poste visé, les expériences et compétences les plus fortes, et ce que
  le candidat apporte.
- Réponds UNIQUEMENT avec le texte du résumé : pas de titre, pas de guillemets, pas de markdown.
"""

# Kept for backward compatibility with anything importing the old name directly.
SYSTEM_PROMPT = SYSTEM_PROMPT_EN

HUMAN_PROMPT = "Profile data:\n{profile}"


def _text(value: Any) -> str:
    return " ".join(str(value or "").split())[:_MAX_TEXT]


def profile_facts(profile: dict) -> dict:
    """The parts of the profile the summary may use (no contact details)."""
    profile = profile if isinstance(profile, dict) else {}
    personal = profile.get("personal") or {}
    return {
        "poste_vise": _text(personal.get("jobTitle")),
        "resume_actuel": _text(profile.get("resume")),
        "experiences": [
            {
                "poste": _text(e.get("title")),
                "entreprise": _text(e.get("company")),
                "debut": _text(e.get("startDate")),
                "fin": "en cours" if e.get("current") else _text(e.get("endDate")),
                "description": _text(e.get("description")),
                "taches": [_text(t) for t in (e.get("taches") or [])[:4]],
            }
            for e in (profile.get("experience") or [])[:_MAX_ITEMS]
            if isinstance(e, dict)
        ],
        "formations": [
            {"diplome": _text(f.get("degree")), "etablissement": _text(f.get("institution")), "fin": _text(f.get("endYear"))}
            for f in (profile.get("education") or [])[:_MAX_ITEMS]
            if isinstance(f, dict)
        ],
        "competences": [_text(s.get("name")) for s in (profile.get("skills") or []) if isinstance(s, dict)][:20],
        "projets": [
            {"titre": _text(p.get("title")), "description": _text(p.get("description")), "stack": (p.get("stack") or [])[:8]}
            for p in (profile.get("projets") or [])[:_MAX_ITEMS]
            if isinstance(p, dict)
        ],
        "certifications": [_text(c.get("name")) for c in (profile.get("certifications") or []) if isinstance(c, dict)][:_MAX_ITEMS],
    }


def has_content(facts: dict) -> bool:
    return any(facts.get(k) for k in ("poste_vise", "experiences", "formations", "competences", "projets"))


def _select_system_prompt(language: str | None) -> str:
    return SYSTEM_PROMPT_FR if (language or "en").strip().lower() == "fr" else SYSTEM_PROMPT_EN


async def generate_summary(profile: dict, language: str = "en") -> str:
    """The summary text, written in `language` ("en" or "fr"). Raises ValueError when the
    profile has nothing to summarise."""
    facts = profile_facts(profile)
    if not has_content(facts):
        raise ValueError("Profil vide : ajoutez un poste, une expérience ou des compétences.")

    prompt = ChatPromptTemplate.from_messages([("system", _select_system_prompt(language)), ("human", HUMAN_PROMPT)])
    chain = prompt | get_llm(temperature=0.3, agent_name="resume")
    response = await chain.ainvoke({"profile": json.dumps(facts, ensure_ascii=False)})
    text = str(getattr(response, "content", response) or "").strip().strip('"').strip()
    if not text:
        raise RuntimeError("Réponse vide du modèle.")
    return text
