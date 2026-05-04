# ============================================================
# app/domain/offer/agents/offer_analyzer.py
# Agent 1 — Analyseur d'offre d'emploi (LLM)
#
# Pattern : Prompt → LLM (Groq/OpenAI) → JsonOutputParser
# Entrée  : raw_offer_text (texte brut de l'offre)
# Sortie  : analyzed_offer (dict JSON structuré)
# ============================================================
import logging
from langchain_core.prompts import ChatPromptTemplate
from langchain_core.output_parsers import JsonOutputParser
from langchain_core.messages import AIMessage
from app.core.config import get_llm
from app.domain.offer.schemas.state import OfferState

logger = logging.getLogger(__name__)

# ─── Prompt système ───────────────────────────────────────────
_SYSTEM = """\
Tu es un expert RH spécialisé dans l'analyse d'offres d'emploi tech.

Ta mission : analyser l'offre fournie et extraire les informations structurées.
Tu dois retourner UNIQUEMENT un JSON valide (sans markdown, sans backticks).

Schéma attendu :
{{
  "titre": "string",
  "entreprise": "string ou null",
  "type_contrat": "CDI | CDD | Stage | Alternance | Freelance | null",
  "localisation": "string ou null",
  "competences_requises": ["liste des compétences obligatoires"],
  "competences_souhaitees": ["liste des compétences bonus"],
  "keywords_ats": ["10 à 20 mots-clés ATS"],
  "annees_experience": "entier ou null",
  "niveau_etudes": "string ou null",
  "description_poste": "résumé en 2-3 phrases"
}}

Règles :
- keywords_ats : technologies, frameworks, certifications, méthodologies clés
- Ne jamais inventer d'informations absentes de l'offre
- Sois exhaustif sur les compétences (liste complète)
"""

_HUMAN = "Offre à analyser :\n\n{raw_offer_text}"


async def offer_analyzer_node(state: OfferState) -> dict:
    """
    Nœud LangGraph — Agent 1 : Analyse du texte brut via LLM.

    ┌─────────────────────────────────────────────────────────┐
    │  Entrées state  │  raw_offer_text                       │
    │  Sorties state  │  analyzed_offer, messages             │
    │  Erreurs state  │  errors (accumulés)                   │
    │  LLM utilisé    │  Groq llama-3.3-70b | OpenAI gpt-4o  │
    └─────────────────────────────────────────────────────────┘
    """
    logger.info("🤖 Agent 1 [Offer Analyzer] — Démarrage")
    raw_text = state.get("raw_offer_text", "")

    if not raw_text:
        logger.warning("Agent 1 — raw_offer_text vide")
        return {
            "analyzed_offer": None,
            "errors": ["Agent1: raw_offer_text est vide"],
        }

    try:
        llm = get_llm()
        prompt = ChatPromptTemplate.from_messages([
            ("system", _SYSTEM),
            ("human", _HUMAN),
        ])
        chain = prompt | llm | JsonOutputParser()

        result: dict = await chain.ainvoke({"raw_offer_text": raw_text})

        logger.info(
            "Agent 1 ✅ — '%s' | %d compétences requises | %d keywords ATS",
            result.get("titre", "?"),
            len(result.get("competences_requises", [])),
            len(result.get("keywords_ats", [])),
        )

        summary = (
            f"[Agent 1] Offre analysée : {result.get('titre')} "
            f"({result.get('type_contrat', '?')}) chez {result.get('entreprise', '?')}"
        )
        return {
            "analyzed_offer": result,
            "messages": [AIMessage(content=summary, name="offer_analyzer")],
        }

    except Exception as e:
        logger.error("Agent 1 ❌ — Erreur LLM : %s", str(e))
        return {
            "analyzed_offer": None,
            "errors": [f"Agent1: {str(e)}"],
            "messages": [AIMessage(content=f"[Agent 1] Erreur : {str(e)}", name="offer_analyzer")],
        }
