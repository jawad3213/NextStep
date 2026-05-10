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
from langchain_core.messages import AIMessage
from app.core.config import get_llm
from app.core.utils.normalizer import normalize_skills
from app.domain.offer_analyzer.schemas.state import OfferAnalyzerState
from app.domain.offer_analyzer.schemas.models import AnalyzedOffer
from app.domain.offer_analyzer.agents.prompt import SYSTEM_PROMPT, HUMAN_PROMPT

logger = logging.getLogger(__name__)


async def offer_analyzer_node(state: OfferAnalyzerState) -> dict:
    """
    Nœud LangGraph — Agent 1 : Analyse du texte brut via LLM et Pydantic.

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
    logger.info("Agent 1 [Offer Analyzer] -- START (Iteration: %d)", state.get("iteration_count", 0) + 1)
    raw_text = state.get("raw_offer_text", "")
    current_count = state.get("iteration_count", 0)

    if not raw_text:
        logger.warning("Agent 1 — raw_offer_text vide")
        return {
            "analyzed_offer": None,
            "errors": ["Agent1: raw_offer_text est vide"],
            "iteration_count": current_count + 1
        }

    try:
        # Configuration du LLM avec sortie structurée Pydantic (temperature 0.0 pour un score déterministe)
        llm = get_llm(agent_name="offer_analyzer", temperature=0.0).with_structured_output(AnalyzedOffer)
        
        # On peut injecter les erreurs précédentes dans le prompt si c'est un retry
        prev_errors = state.get("errors", [])
        system_msg = SYSTEM_PROMPT
        if prev_errors and current_count > 0:
            # Escape braces in error messages to avoid LangChain template parsing issues
            safe_errors = str(prev_errors).replace("{", "{{").replace("}", "}}")
            system_msg += f"\n\nIMPORTANT: Tes précédentes tentatives ont échoué avec ces erreurs : {safe_errors}. Corrige-les impérativement."

        prompt = ChatPromptTemplate.from_messages([
            ("system", system_msg),
            ("human", HUMAN_PROMPT),
        ])
        
        chain = prompt | llm

        # Invocation
        analyzed_obj: AnalyzedOffer = await chain.ainvoke({"raw_offer_text": raw_text})
        result = analyzed_obj.model_dump()

        # ── Normalisation intégrée ──────────────────────────────
        offer_skills = normalize_skills(result.get("competences_requises", []))
        keywords     = normalize_skills(result.get("keywords_ats", []))

        return {
            "analyzed_offer":          result,
            "normalized_offer_skills": offer_skills,
            "normalized_keywords":     keywords,
            "messages": [AIMessage(content=f"Analyse terminée (Tentative {current_count + 1})", name="offer_analyzer")],
            "iteration_count": current_count + 1
        }

    except Exception as e:
        logger.error("Agent 1 ❌ — Erreur LLM : %s", str(e))
        return {
            "analyzed_offer": None,
            "errors": [f"Agent1: {str(e)}"],
            "iteration_count": current_count + 1
        }


def offer_validator_node(state: OfferAnalyzerState) -> dict:
    """
    Nœud Algorithmique — Valide et nettoie les données extraites par l'Agent 1.
    """
    logger.info("🔧 Validation Algorithmique — START")
    data = state.get("analyzed_offer")
    
    # On vide les erreurs précédentes pour ne pas boucler à l'infini sur de vieux logs
    # Note: Dans LangGraph avec operator.add, on ne peut pas "vider". 
    # On va donc utiliser une logique dans le router basée uniquement sur les erreurs fraîches.
    current_errors = []

    if not data:
        return {"errors": ["Validator: Aucune donnée à valider."]}

    # 1. Vérification des champs critiques
    if not data.get("titre") or data["titre"].lower() in ["string", "n/a", "unknown"]:
        current_errors.append("Validator: Le titre du poste est manquant ou invalide.")

    # 2. Nettoyage des listes
    for field in ["competences_requises", "competences_souhaitees", "keywords_ats"]:
        original_list = data.get(field, [])
        cleaned_list = list(set([
            item.strip() for item in original_list 
            if item and item.lower() not in ["string", "skill"]
        ]))
        data[field] = cleaned_list

    if not data["competences_requises"]:
        current_errors.append("Validator: Aucune compétence obligatoire détectée.")

    # 3. Validation logique — annees_experience (string parsé)
    exp = data.get("annees_experience")
    if exp is not None:
        import re
        match = re.search(r'\d+', str(exp))
        if match:
            val = int(match.group())
            if val < 0 or val > 40:
                data["annees_experience"] = None
            else:
                data["annees_experience"] = str(val)
        else:
            data["annees_experience"] = None

    # 4. Vérification du contrat
    from app.domain.offer_analyzer.schemas.models import TypeContrat
    valid_contracts = [item.value for item in TypeContrat]
    if data.get("type_contrat") and data["type_contrat"] not in valid_contracts:
        current_errors.append(f"Validator: Type de contrat '{data['type_contrat']}' non supporté.")

    return {
        "analyzed_offer": data,
        "errors": current_errors,
        "messages": [AIMessage(content=f"Validation terminée. {len(current_errors)} erreurs.", name="validator")]
    }


def offer_analyzer_router(state: OfferAnalyzerState) -> str:
    """
    Routeur — Décide s'il faut recommencer l'analyse ou terminer.
    Si des erreurs persistent après 3 tentatives, on continue quand même 
    avec le dernier résultat obtenu.
    """
    count = state.get("iteration_count", 0)
    errors = state.get("errors", [])
    
    # On retente uniquement s'il y a des erreurs ET qu'on n'a pas dépassé 3 essais
    if errors and count < 3:
        logger.warning("🔄 Retry demandé (Tentative %d/3). Erreurs: %s", count, errors[-1:])
        return "retry"
    
    # Dans tous les autres cas (succès OU max atteint), on termine
    if errors:
        logger.warning("⚠️ Max iterations atteintes (3/3). On continue avec le dernier résultat malgré les erreurs.")
    else:
        logger.info("✅ Analyse validée avec succès.")
        
    return "end"


