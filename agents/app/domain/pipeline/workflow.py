import logging
from langchain_core.messages import AIMessage
from langgraph.graph import StateGraph, END

from app.domain.pipeline.state import PipelineState
from app.domain.offer_analyzer.service import offer_analyzer_service
from app.domain.profile_retriever.service import profile_retriever_service
from app.domain.company.service import company_service
from app.domain.email_composer.agents.agent import email_composer_node as _email_composer_node
from app.domain.cv_optimizer.service import cv_optimizer_service
from app.domain.cv_engine.service import cv_engine_service
from app.domain.matching.skill_matcher import build_match_result

logger = logging.getLogger(__name__)



async def offer_analyzer_node(state: PipelineState) -> dict:
    """Nœud appelant le service d'analyse d'offre."""
    logger.info("Pipeline — Calling OfferAnalyzerService")
    if state.get("analyzed_offer"):
        return {}
    result = await offer_analyzer_service.analyze(state["raw_offer_text"])
    return {
        "analyzed_offer": result.get("analyzed_offer"),
        "normalized_offer_skills": result.get("normalized_offer_skills") or [],
        "normalized_keywords": result.get("normalized_keywords") or [],
        "errors": result.get("errors") or [],
        "messages": [AIMessage(content="[Pipeline] Offre analysee via Service", name="orchestrator")],
    }


async def profile_retriever_node(state: PipelineState) -> dict:
    """Nœud appelant le service de récupération de profil."""
    logger.info("Pipeline — Calling ProfileRetrieverService")
    if state.get("profile_data"):
        return {}
    result = await profile_retriever_service.get_profile(str(state["user_id"]))
    return {
        "profile_data": result.get("profile_data"),
        "errors": result.get("errors") or [],
        "messages": [AIMessage(content="[Pipeline] Profil recupere via Service", name="orchestrator")],
    }


async def skill_gap_node(state: PipelineState) -> dict:
    """Nœud appelant le service de skill gap / deterministic matching."""
    logger.info("Pipeline — Calling SkillGapService")

    if state.get("skill_gap_analysis") or state.get("match_result"):
        skill_gap_analysis = state.get("skill_gap_analysis") or state.get("match_result")
        return {
            "skill_gap_analysis": skill_gap_analysis,
            "match_result": state.get("match_result") or skill_gap_analysis,
        }

    if not state.get("profile_data") or not state.get("analyzed_offer"):
        return {"errors": ["Donnees manquantes pour l'analyse d'ecart"]}

    match_result = build_match_result(
        state.get("profile_data") or {},
        state.get("analyzed_offer") or {},
    )

    return {
        "skill_gap_analysis": match_result,
        "match_result": match_result,
        "errors": [],
        "messages": [AIMessage(content="[Pipeline] Skill Gap deterministe applique", name="orchestrator")],
    }


async def critical_skill_gap_node(state: PipelineState) -> dict:
    match_result = state.get("skill_gap_analysis") or state.get("match_result") or {}
    profile_data = state.get("profile_data") or {}
    analyzed_offer = state.get("analyzed_offer") or {}

    if not match_result:
        return {}

    match_result = build_match_result(profile_data, analyzed_offer)

    return {
        "skill_gap_analysis": match_result,
        "match_result": match_result,
        "messages": [AIMessage(content="[Pipeline] Critical skill-gap validation applied", name="orchestrator")],
    }


async def email_composer_node(state: PipelineState) -> dict:
    """
    Adaptor node — bridges PipelineState → EmailComposerState.
    Maps match_result → skill_gap for the email_composer agent.
    """
    logger.info("Pipeline — Calling EmailComposerNode")

    # Si on n'a pas été appelé avec l'intention de générer un email (par ex just CV), on peut ignorer ?
    # Ici, nous le laissons courir car generation_options n'est pas strict.

    email_state = {
        "user_id":            str(state.get("user_id", "")),
        "profile_data":       state.get("profile_data"),
        "analyzed_offer":     state.get("analyzed_offer"),
        "raw_offer_text":     state.get("raw_offer_text"),
        "skill_gap":          state.get("match_result"),
        "company_intelligence": state.get("company_intelligence"),
        "generation_options": state.get("generation_options"),
        "messages":           [],
        "errors":             [],
        "warnings":           [],
        "iteration_count":    0,
    }

    result = await _email_composer_node(email_state)

    output: dict = {}
    if result.get("email_draft"):
        output["email_draft"] = result["email_draft"]
    if result.get("errors"):
        output["errors"] = result["errors"]
    if result.get("warnings"):
        output["warnings"] = result["warnings"]
    if result.get("messages"):
        output["messages"] = result["messages"]

    return output


async def company_intelligence_node(state: PipelineState) -> dict:
    """Company analysis, reused when possible: the one sent by the backend (earlier run for
    this offer), else a recent analysis of the same company, else a new web research."""
    if state.get("company_intelligence"):
        return {
            "company_intelligence_source": "request",
            "messages": [AIMessage(content="[Pipeline] Intelligence entreprise reprise de l'analyse existante", name="orchestrator")],
        }

    analyzed_offer = state.get("analyzed_offer")
    if not analyzed_offer or not analyzed_offer.get("entreprise"):
        return {"messages": [AIMessage(content="[Pipeline] Pas d'entreprise detectee, intelligence ignoree", name="orchestrator")]}
    company = analyzed_offer.get("entreprise")

    from app.core.database import AsyncSessionFactory
    try:
        async with AsyncSessionFactory() as db:
            cached = await company_service.find_cached_intelligence(db, company)
    except Exception as e:
        logger.warning("Company cache unavailable for '%s': %s", company, e)
        cached = None
    if cached:
        report, collected_at = cached
        return {
            "company_intelligence": report,
            "company_intelligence_source": "cache",
            "company_intelligence_collected_at": collected_at,
            "messages": [AIMessage(content="[Pipeline] Intelligence entreprise reprise du cache", name="orchestrator")],
        }

    result = await company_service.get_company_intelligence(
        company_name=company,
        job_title=analyzed_offer.get("titre", "Poste inconnu"),
        user_id=state.get("user_id", ""),
    )
    return {
        "company_intelligence": result,
        "company_intelligence_source": "fresh",
        "messages": [AIMessage(content="[Pipeline] Intelligence entreprise recuperee", name="orchestrator")],
    }


async def cv_optimizer_node(state: PipelineState) -> dict:
    if not state.get("profile_data") or not state.get("analyzed_offer"):
        return {"errors": ["Donnees manquantes pour l'optimisation de CV"]}

    result = await cv_optimizer_service.optimize_cv(
        candidate_cv=state["profile_data"],
        job_offer=state["analyzed_offer"],
        match_result=state.get("match_result"),
        skill_gap_analysis=state.get("skill_gap_analysis"),
        language=state["profile_data"].get("preferred_language") or "en",
    )
    return {
        "cv_optimized_content": result.model_dump() if result else None,
        "messages": [AIMessage(content="[Pipeline] CV optimise via Service", name="orchestrator")],
    }


async def cv_engine_node(state: PipelineState) -> dict:
    if not state.get("profile_data") or not state.get("cv_optimized_content"):
        return {"errors": ["Donnees manquantes pour le formatage du CV"]}

    match_result = state.get("skill_gap_analysis") or state.get("match_result", {})
    matched_skills = match_result.get("matched_skills", []) if match_result else []

    result = await cv_engine_service.format_for_questpdf(
        original_profile=state["profile_data"],
        optimized_cv=state["cv_optimized_content"],
        matched_skills=matched_skills,
        offer_skills=(state.get("analyzed_offer") or {}).get("competences_requises") or [],
    )
    return {
        "cv_engine_result": result,
        "messages": [AIMessage(content="[Pipeline] CV finalise via Service", name="orchestrator")],
    }


async def db_persist_node(state: PipelineState) -> dict:
    from app.core.database import AsyncSessionFactory
    from app.core.models import OffreAnalysee, ResultatMatching
    from sqlalchemy import select
    import uuid

    offer_id = state.get("offer_id")
    user_id = state.get("user_id")
    analyzed_offer = state.get("analyzed_offer")
    match_result = state.get("skill_gap_analysis") or state.get("match_result")

    if not offer_id or not analyzed_offer:
        return {"messages": [AIMessage(content="[Pipeline] DB save skipped (missing offer_id)", name="orchestrator")]}

    try:
        async with AsyncSessionFactory() as db:
            o_uuid = uuid.UUID(offer_id)

            # Supprimer l'ancienne analyse si elle existe
            existing_analyses = await db.execute(
                select(OffreAnalysee).where(OffreAnalysee.id_offre == o_uuid)
            )
            for old_ana in existing_analyses.scalars().all():
                await db.delete(old_ana)

            db.add(OffreAnalysee(
                id_offre=o_uuid,
                titre_poste=analyzed_offer.get("titre", ""),
                entreprise=analyzed_offer.get("entreprise", ""),
                competences_requises=analyzed_offer.get("competences_requises"),
                competences_souhaitees=analyzed_offer.get("competences_souhaitees"),
                keywords_ats=analyzed_offer.get("keywords_ats"),
                texte_brut=state.get("raw_offer_text"),
            ))

            if match_result:
                u_uuid = None
                if user_id:
                    try:
                        u_uuid = uuid.UUID(str(user_id))
                    except ValueError:
                        pass
                if u_uuid:
                    # Supprimer l'ancien matching s'il existe
                    existing_matchings = await db.execute(
                        select(ResultatMatching)
                        .where(ResultatMatching.id_offre == o_uuid)
                        .where(ResultatMatching.id_utilisateur == u_uuid)
                    )
                    for old_match in existing_matchings.scalars().all():
                        await db.delete(old_match)

                    db.add(ResultatMatching(
                        id_offre=o_uuid,
                        id_utilisateur=u_uuid,
                        score_global=match_result.get("score_matching", 0),
                        competences_manquantes=match_result.get("competences_manquantes"),
                        points_forts=match_result.get("competences_matching"),
                    ))

            # A new analysis is saved (cache + link to this offer). A reused one is only linked
            # when the offer has none yet, keeping its collection date so the cache still expires.
            company_intel = state.get("company_intelligence")
            fresh = state.get("company_intelligence_source", "fresh") == "fresh"
            if company_intel and (fresh or not await company_service.has_offer_intelligence(db, offer_id)):
                try:
                    await company_service.save_company_intelligence(
                        db, company_intel, offer_id,
                        company_name=analyzed_offer.get("entreprise"),
                        collected_at=None if fresh else state.get("company_intelligence_collected_at"),
                    )
                except Exception as intel_e:
                    logger.error("Failed to save company intel: %s", intel_e)

            await db.commit()
            return {"messages": [AIMessage(content="[Pipeline] Resultats sauvegardes en DB", name="orchestrator")]}
    except Exception as e:
        if "UndefinedTableError" in str(e) or "relation \"offre_analysee\" does not exist" in str(e):
            logger.warning("DbPersistNode skipped: optional agent persistence tables are missing.")
            return {"messages": [AIMessage(content="[Pipeline] Agent DB persistence skipped (tables missing)", name="orchestrator")]}
        logger.error("DbPersistNode error: %s", e)
        return {"errors": [f"Erreur sauvegarde DB: {str(e)}"]}



def build_offer_pipeline() -> StateGraph:
    workflow = StateGraph(PipelineState)

    workflow.add_node("offer_analyzer_node", offer_analyzer_node)
    workflow.add_node("profile_retriever_node", profile_retriever_node)
    workflow.add_node("skill_gap_node", skill_gap_node)
    workflow.add_node("critical_skill_gap_node", critical_skill_gap_node)
    workflow.add_node("company_intelligence_node", company_intelligence_node)
    workflow.add_node("email_composer_node", email_composer_node)
    workflow.add_node("cv_optimizer_node", cv_optimizer_node)
    workflow.add_node("cv_engine_node", cv_engine_node)
    workflow.add_node("db_persist_node", db_persist_node)

    workflow.set_entry_point("offer_analyzer_node")

    workflow.add_edge("offer_analyzer_node", "profile_retriever_node")
    workflow.add_edge("profile_retriever_node", "skill_gap_node")
    workflow.add_edge("skill_gap_node", "critical_skill_gap_node")
    workflow.add_edge("critical_skill_gap_node", "company_intelligence_node")

    def route_after_intel(state: PipelineState):
        if state.get("only_analysis", False):
            return "db_persist_node"
        return "cv_optimizer_node"

    workflow.add_conditional_edges(
        "company_intelligence_node",
        route_after_intel,
        {
            "db_persist_node": "db_persist_node",
            "cv_optimizer_node": "cv_optimizer_node",
        },
    )

    workflow.add_edge("cv_optimizer_node", "cv_engine_node")
    workflow.add_edge("cv_engine_node", "email_composer_node")
    workflow.add_edge("email_composer_node", "db_persist_node")
    workflow.add_edge("db_persist_node", END)

    return workflow.compile()


_pipeline = None


def get_offer_pipeline():
    global _pipeline
    if _pipeline is None:
        _pipeline = build_offer_pipeline()
    return _pipeline
