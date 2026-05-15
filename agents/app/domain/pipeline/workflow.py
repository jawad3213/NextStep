# ============================================================
# app/domain/pipeline/workflow.py
# Orchestrateur (Pipeline) — Communication par SERVICES
# ============================================================
import logging
from typing import Literal
from langchain_core.messages import AIMessage
from langgraph.graph import StateGraph, END

from app.domain.pipeline.state import PipelineState

# Import des SERVICES (Communication inter-domaines)
from app.domain.offer_analyzer.service import offer_analyzer_service
from app.domain.profile_retriever.service import profile_retriever_service
from app.domain.skill_gap.service import skill_gap_service
from app.domain.cv_optimizer.service import cv_optimizer_service
from app.domain.cv_engine.service import cv_engine_service

logger = logging.getLogger(__name__)

# ─── NŒUDS DU PIPELINE (Appellent les Services) ───────────────

async def offer_analyzer_node(state: PipelineState) -> dict:
    """Nœud appelant le service d'analyse d'offre."""
    logger.info("Pipeline -- Calling OfferAnalyzerService")
    result = await offer_analyzer_service.analyze(state["raw_offer_text"])
    
    return {
        "analyzed_offer":          result.get("analyzed_offer"),
        "normalized_offer_skills": result.get("normalized_offer_skills") or [],
        "normalized_keywords":     result.get("normalized_keywords") or [],
        "errors":                  result.get("errors") or [],
        "messages": [AIMessage(content="[Pipeline] Offre analysée via Service", name="orchestrator")],
    }

async def profile_retriever_node(state: PipelineState) -> dict:
    """Nœud appelant le service de récupération de profil."""
    logger.info("Pipeline -- Calling ProfileRetrieverService")
    result = await profile_retriever_service.get_profile(str(state["user_id"]))
    
    return {
        "profile_data":              result.get("profile_data"),
        "errors":                    result.get("errors") or [],
        "messages": [AIMessage(content="[Pipeline] Profil récupéré via Service", name="orchestrator")],
    }

async def skill_gap_node(state: PipelineState) -> dict:
    """Nœud appelant le service de skill gap (ancien scorer)."""
    logger.info("Pipeline -- Calling SkillGapService")
    
    # On s'assure d'avoir les données nécessaires
    if not state.get("profile_data") or not state.get("analyzed_offer"):
        return {"errors": ["Données manquantes pour l'analyse d'écart"]}

    result = await skill_gap_service.analyze_skill_gap(
        candidate_cv=state["profile_data"],
        job_offer=state["analyzed_offer"]
    )
    
    return {
        "match_result": result.skill_gap.model_dump() if result.skill_gap else None,
        "errors":       result.errors or [],
        "messages": [AIMessage(content="[Pipeline] Skill Gap analysé via Service", name="orchestrator")],
    }

async def cv_optimizer_node(state: PipelineState) -> dict:
    """Nœud appelant le service d'optimisation de CV."""
    logger.info("Pipeline -- Calling CvOptimizerService")
    
    if not state.get("profile_data") or not state.get("analyzed_offer"):
        return {"errors": ["Données manquantes pour l'optimisation de CV"]}
        
    result = await cv_optimizer_service.optimize_cv(
        candidate_cv=state["profile_data"],
        job_offer=state["analyzed_offer"]
    )
    
    return {
        "cv_optimized_content": result.model_dump() if result else None,
        "messages": [AIMessage(content="[Pipeline] CV optimisé via Service", name="orchestrator")],
    }

async def cv_engine_node(state: PipelineState) -> dict:
    """Nœud appelant le service de formatage CV Engine."""
    logger.info("Pipeline -- Calling CvEngineService")
    
    if not state.get("profile_data") or not state.get("cv_optimized_content"):
         return {"errors": ["Données manquantes pour le formatage du CV"]}
         
    result = await cv_engine_service.format_for_questpdf(
        original_profile=state["profile_data"],
        optimized_cv=state["cv_optimized_content"]
    )
    
    return {
        "cv_engine_result": result,
        "messages": [AIMessage(content="[Pipeline] CV finalisé via Service", name="orchestrator")],
    }

async def db_persist_node(state: PipelineState) -> dict:
    """Nœud pour sauvegarder les résultats de l'agent 2 et 4 dans PostgreSQL."""
    logger.info("Pipeline -- Calling DbPersistNode")
    
    from app.core.database import AsyncSessionFactory
    from app.domain.chatbot.models import OffreAnalysee, ResultatMatching
    import uuid
    
    offer_id = state.get("offer_id")
    user_id = state.get("user_id")
    analyzed_offer = state.get("analyzed_offer")
    match_result = state.get("match_result")
    
    if not offer_id or not analyzed_offer:
        return {"messages": [AIMessage(content="[Pipeline] DB save skipped (missing offer_id)", name="orchestrator")]}
        
    try:
        async with AsyncSessionFactory() as db:
            o_uuid = uuid.UUID(offer_id)
            
            # --- Enregistrement OffreAnalysee ---
            db.add(OffreAnalysee(
                id_offre=o_uuid,
                titre_poste=analyzed_offer.get("titre", ""),
                entreprise=analyzed_offer.get("entreprise", ""),
                competences_requises=analyzed_offer.get("competences_requises"),
                competences_souhaitees=analyzed_offer.get("competences_souhaitees"),
                keywords_ats=analyzed_offer.get("keywords_ats"),
                texte_brut=state.get("raw_offer_text")
            ))
            
            # --- Enregistrement ResultatMatching ---
            if match_result:
                u_uuid = None
                if user_id:
                    try:
                        u_uuid = uuid.UUID(str(user_id))
                    except ValueError:
                        pass
                
                if u_uuid:
                    db.add(ResultatMatching(
                        id_offre=o_uuid,
                        id_utilisateur=u_uuid,
                        score_global=match_result.get("score_matching", 0),
                        competences_manquantes=match_result.get("competences_manquantes"),
                        points_forts=match_result.get("competences_matching")
                    ))
                    
            await db.commit()
            return {"messages": [AIMessage(content="[Pipeline] Résultats sauvegardés en DB", name="orchestrator")]}
    except Exception as e:
        logger.error(f"DbPersistNode error: {e}")
        return {"errors": [f"Erreur sauvegarde DB: {str(e)}"]}

# ─── CONSTRUCTION DU GRAPHE ───────────────────────────────────

def build_offer_pipeline() -> StateGraph:
    workflow = StateGraph(PipelineState)

    workflow.add_node("offer_analyzer_node",    offer_analyzer_node)
    workflow.add_node("profile_retriever_node", profile_retriever_node)
    workflow.add_node("skill_gap_node",         skill_gap_node)
    workflow.add_node("cv_optimizer_node",      cv_optimizer_node)
    workflow.add_node("cv_engine_node",         cv_engine_node)
    workflow.add_node("db_persist_node",        db_persist_node)

    workflow.set_entry_point("offer_analyzer_node")

    workflow.add_edge("offer_analyzer_node",    "profile_retriever_node")
    workflow.add_edge("profile_retriever_node", "skill_gap_node")
    workflow.add_edge("skill_gap_node",         "cv_optimizer_node")
    workflow.add_edge("cv_optimizer_node",      "cv_engine_node")
    workflow.add_edge("cv_engine_node",         "db_persist_node")
    workflow.add_edge("db_persist_node",        END)

    return workflow.compile()

_pipeline = None

def get_offer_pipeline():
    global _pipeline
    if _pipeline is None:
        _pipeline = build_offer_pipeline()
    return _pipeline
