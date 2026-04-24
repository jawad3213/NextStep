# ============================================================
# app/agents/offer_analyzer.py
# Agent 1 — Analyseur d'offre d'emploi (LLM)
#
# Pattern : Prompt + LLM binding + JsonOutputParser
# Reçoit l'AgentState, analyse le texte brut, retourne la mise à jour d'état.
# ============================================================
import logging
from langchain_core.prompts import ChatPromptTemplate
from langchain_core.output_parsers import JsonOutputParser
from langchain_core.messages import AIMessage
from app.core.config import get_llm
from app.schemas.state import AgentState

logger = logging.getLogger(__name__)

# ─── Prompt de l'agent ───
SYSTEM_PROMPT = """Tu es un expert RH spécialisé dans l'analyse d'offres d'emploi tech.

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
- Sois exhaustif sur les compétences
"""

HUMAN_PROMPT = "Offre à analyser :\n\n{raw_offer_text}"


async def offer_analyzer_node(state: AgentState) -> dict:
    """
    Nœud LangGraph — Agent 1 : Analyse du texte brut de l'offre via LLM.

    Entrées depuis state :
        - raw_offer_text : texte brut de l'offre

    Mise à jour de state :
        - analyzed_offer : dict JSON structuré
        - messages : message AI ajouté à l'historique
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
        # ─── Construction de la chaîne LangChain ───
        llm = get_llm()
        prompt = ChatPromptTemplate.from_messages([
            ("system", SYSTEM_PROMPT),
            ("human", HUMAN_PROMPT),
        ])
        parser = JsonOutputParser()
        chain = prompt | llm | parser

        # ─── Invocation ───
        result: dict = await chain.ainvoke({"raw_offer_text": raw_text})

        logger.info(
            "Agent 1 ✅ — Offre analysée : '%s' | %d compétences | %d keywords ATS",
            result.get("titre", "?"),
            len(result.get("competences_requises", [])),
            len(result.get("keywords_ats", [])),
        )

        # ─── Mise à jour de l'état ───
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
