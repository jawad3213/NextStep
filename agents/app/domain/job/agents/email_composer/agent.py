# ============================================================
# app/domain/job/agents/email_composer/agent.py
# Agent 6 — Email Composer (LLM)
# ============================================================
import logging
from langchain_core.prompts import ChatPromptTemplate
from langchain_core.output_parsers import JsonOutputParser
from langchain_core.messages import AIMessage
from app.core.config import get_llm
from app.domain.job.schemas.state import JobState
from app.domain.job.agents.email_composer.prompt import (
    SYSTEM_CANDIDATURE,
    SYSTEM_RELANCE,
    HUMAN_PROMPT,
)

logger = logging.getLogger(__name__)


async def email_composer_node(state: JobState) -> dict:
    """
    Nœud LangGraph — Agent 6 : Génération de l'email via LLM.

    ┌────────────────────────────────────────────────────────────┐
    │  Entrées state  │  profile_data, offer_data              │
    │                 │  email_type ("candidature"|"relance")  │
    │  Sorties state  │  email_draft, messages                 │
    │  LLM utilisé    │  Groq llama-3.3-70b | OpenAI gpt-4o  │
    └────────────────────────────────────────────────────────────┘
    """
    logger.info("✉️  Agent 6 [Email Composer] — Démarrage")

    profile      = state.get("profile_data") or {}
    offer        = state.get("offer_data")   or {}
    email_type   = state.get("email_type", "candidature")
    langue       = "fr"

    prenom       = profile.get("prenom", "")
    nom          = profile.get("nom", "")
    titre        = profile.get("titre", "")
    poste        = offer.get("titre", "")
    entreprise   = offer.get("entreprise", "")
    type_contrat = offer.get("type_contrat", "")

    comps = [c.get("nom", "") for c in profile.get("competences", []) if isinstance(c, dict)]
    competences_str = ", ".join(comps[:5]) if comps else "Non renseignées"

    if not prenom or not poste:
        logger.warning("Agent 6 — Données insuffisantes pour générer l'email")
        return {
            "email_draft": {
                "objet": f"Candidature — {poste} — {prenom} {nom}",
                "corps": (
                    f"Bonjour,\n\nJe vous adresse ma candidature pour le poste de {poste}."
                    f"\n\nCordialement,\n{prenom} {nom}"
                ),
                "type": email_type,
                "langue": langue,
                "_note": "Données insuffisantes — email généré en mode fallback",
            },
            "errors": ["Agent6: profil ou offre incomplets pour la génération LLM"],
        }

    try:
        llm = get_llm(temperature=0.4)
        system = SYSTEM_CANDIDATURE if email_type == "candidature" else SYSTEM_RELANCE
        prompt = ChatPromptTemplate.from_messages([
            ("system", system.format(langue=langue)),
            ("human", HUMAN_PROMPT),
        ])
        chain = prompt | llm | JsonOutputParser()

        result: dict = await chain.ainvoke({
            "prenom":       prenom,
            "nom":          nom,
            "titre":        titre,
            "competences":  competences_str,
            "poste":        poste,
            "entreprise":   entreprise,
            "type_contrat": type_contrat,
        })

        logger.info(
            "Agent 6 ✅ — Email %s généré | objet='%s'",
            email_type, result.get("objet", "?"),
        )

        summary = f"[Agent 6] Email '{email_type}' rédigé : {result.get('objet', '')}"
        return {
            "email_draft": result,
            "messages": [AIMessage(content=summary, name="email_composer")],
        }

    except Exception as e:
        logger.error("Agent 6 ❌ — Erreur LLM : %s", str(e))
        return {
            "email_draft": {
                "objet": f"Candidature — {poste} — {prenom} {nom}",
                "corps": (
                    f"Bonjour,\n\nJe vous adresse ma candidature pour le poste de {poste}."
                    f"\n\nCordialement,\n{prenom} {nom}"
                ),
                "type": email_type,
                "langue": langue,
            },
            "errors": [f"Agent6: {str(e)}"],
            "messages": [AIMessage(content=f"[Agent 6] Erreur LLM : {str(e)}", name="email_composer")],
        }
