# ============================================================
# app/domain/offer_analyzer/agents/agent.py
# Agent 1 — Analyseur d'offre d'emploi (LLM)
#
# Pattern : Prompt → LLM (Groq/OpenAI) → JsonOutputParser
# Entrée  : raw_offer_text (texte brut de l'offre)
# Sortie  : analyzed_offer (dict JSON structuré)
#           + normalized_offer_skills + normalized_keywords
# ============================================================
import logging
from langchain_core.prompts import ChatPromptTemplate
from langchain_core.output_parsers import JsonOutputParser
from langchain_core.messages import AIMessage
from app.core.config import get_llm
from app.core.utils.normalizer import normalize_skills
from app.domain.offer_analyzer.schemas.state import OfferAnalyzerState
from app.domain.offer_analyzer.agents.prompt import SYSTEM_PROMPT, HUMAN_PROMPT

logger = logging.getLogger(__name__)


async def offer_analyzer_node(state: OfferAnalyzerState) -> dict:
    """
    Nœud LangGraph — Agent 1 : Analyse du texte brut via LLM.

    ┌─────────────────────────────────────────────────────────┐
    │  Entrées state  │  raw_offer_text                       │
    │  Sorties state  │  analyzed_offer                       │
    │                 │  normalized_offer_skills (requis)     │
    │                 │  normalized_keywords (ATS)            │
    │                 │  messages                             │
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
            ("system", SYSTEM_PROMPT),
            ("human", HUMAN_PROMPT),
        ])
        chain = prompt | llm | JsonOutputParser()

        result: dict = await chain.ainvoke({"raw_offer_text": raw_text})

        # ── Normalisation intégrée ──────────────────────────────
        offer_skills = normalize_skills(result.get("competences_requises", []))
        keywords     = normalize_skills(result.get("keywords_ats", []))

        logger.info(
            "Agent 1 ✅ — '%s' | %d compétences requises | %d keywords ATS",
            result.get("titre", "?"),
            len(offer_skills),
            len(keywords),
        )

        summary = (
            f"[Agent 1] Offre analysée : {result.get('titre')} "
            f"({result.get('type_contrat', '?')}) chez {result.get('entreprise', '?')} "
            f"— {len(offer_skills)} compétences, {len(keywords)} keywords ATS normalisés"
        )
        return {
            "analyzed_offer":          result,
            "normalized_offer_skills": offer_skills,
            "normalized_keywords":     keywords,
            "messages": [AIMessage(content=summary, name="offer_analyzer")],
        }

    except Exception as e:
        logger.error("Agent 1 ❌ — Erreur LLM : %s", str(e))
        return {
            "analyzed_offer": None,
            "errors": [f"Agent1: {str(e)}"],
            "messages": [AIMessage(content=f"[Agent 1] Erreur : {str(e)}", name="offer_analyzer")],
        }
