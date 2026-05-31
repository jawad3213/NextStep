import logging
import re
from difflib import SequenceMatcher
from langchain_core.messages import AIMessage
from langgraph.graph import StateGraph, END

from app.domain.pipeline.state import PipelineState
from app.domain.offer_analyzer.service import offer_analyzer_service
from app.domain.profile_retriever.service import profile_retriever_service
from app.domain.skill_gap.service import skill_gap_service
from app.domain.company.service import company_service
from app.domain.email_composer.agents.agent import email_composer_node as _email_composer_node
from app.domain.cv_optimizer.service import cv_optimizer_service
from app.domain.cv_engine.service import cv_engine_service
from app.core.utils.normalizer.text_utils import normalize_skills, build_profile_full_text, normalize_text

logger = logging.getLogger(__name__)



def _similarity(a: str, b: str) -> float:
    if not a or not b:
        return 0.0
    if a == b:
        return 1.0
    if min(len(a), len(b)) >= 3 and (a in b or b in a):
        return 0.88
    return SequenceMatcher(None, a, b).ratio()


def _canon_skill(s: str) -> str:
    x = (s or "").strip().lower()
    x = re.sub(r"[^\w\s./+#-]", " ", x)
    x = re.sub(r"\s+", " ", x).strip()
    aliases = {
        "chatbots": "chatbot",
        "nodejs": "node.js",
        "expressjs": "express.js",
        "nextjs": "next.js",
        "postgres": "postgresql",
        "ci cd": "ci/cd",
        "cicd": "ci/cd",
        "github actions": "ci/cd",
        "gitlab ci": "ci/cd",
        "retrieval augmented generation": "rag",
        "ai": "ia",
        "artificial intelligence": "ia",
        "intelligence artificielle": "ia",
        "integration ia": "ia",
        "integrtaion ai": "ia",
        "integration ai": "ia",
    }
    return aliases.get(x, x)


def _collapse_ci_cd(items: list[str]) -> list[str]:
    vals = [_canon_skill(i) for i in items if i]
    has_ci = "ci" in vals
    has_cd = "cd" in vals
    vals = [v for v in vals if v not in {"ci", "cd"}]
    if has_ci and has_cd:
        vals.append("ci/cd")
    return list(dict.fromkeys(vals))


def _word_count(value: str) -> int:
    return len(re.findall(r"\w+", value or ""))


def _is_noisy_skill_phrase(value: str) -> bool:
    c = _canon_skill(value)
    if not c:
        return True
    if _word_count(c) >= 5:
        return True
    return False


def _build_matching_targets(required: list[str], preferred: list[str], keywords: list[str]) -> list[str]:
    required = [str(s) for s in (required or []) if str(s).strip()]
    preferred = [str(s) for s in (preferred or []) if str(s).strip()]
    keywords = [str(s) for s in (keywords or []) if str(s).strip()]

    clean_required = [s for s in required if not _is_noisy_skill_phrase(s)]
    clean_preferred = [s for s in preferred if not _is_noisy_skill_phrase(s)]

    normalized_keywords = _collapse_ci_cd([_canon_skill(s) for s in normalize_skills(keywords)])
    normalized_clean = _collapse_ci_cd([_canon_skill(s) for s in normalize_skills([*clean_required, *clean_preferred])])

    raw_count = len(required) + len(preferred)
    clean_count = len(clean_required) + len(clean_preferred)
    mostly_noisy = raw_count > 0 and clean_count <= max(2, raw_count // 2)
    if mostly_noisy and normalized_keywords:
        return normalized_keywords
    return normalized_clean if normalized_clean else normalized_keywords


def _profile_evidence_tokens(profile_data: dict) -> set[str]:
    chunks: list[str] = []
    if isinstance(profile_data, dict):
        personal_info = profile_data.get("personalInfo") or {}
        if isinstance(personal_info, dict):
            chunks.append(str(personal_info.get("resumeProfessionnel") or ""))
            chunks.append(str(personal_info.get("titrePoste") or ""))

        for c in profile_data.get("competences", []) or []:
            if isinstance(c, dict) and c.get("nom"):
                chunks.append(str(c.get("nom")))
        for e in profile_data.get("experiences", []) or []:
            if isinstance(e, dict):
                chunks.append(str(e.get("titre") or ""))
                chunks.append(str(e.get("poste") or ""))
                chunks.append(str(e.get("description") or ""))
                chunks.append(str(e.get("missions") or ""))
                for t in e.get("taches", []) or []:
                    chunks.append(str(t))
        for p in profile_data.get("projets", []) or profile_data.get("projects", []) or []:
            if isinstance(p, dict):
                chunks.append(str(p.get("titre") or p.get("titreProjet") or ""))
                chunks.append(str(p.get("description") or ""))
                techs = p.get("technologies") or p.get("technologiesUtilisees") or []
                if isinstance(techs, str):
                    chunks.extend([t.strip() for t in techs.split(",") if t.strip()])
                elif isinstance(techs, list):
                    chunks.extend([str(t) for t in techs if t])
                for t in p.get("taches", []) or []:
                    chunks.append(str(t))
        chunks.append(str(profile_data.get("resume") or profile_data.get("resume_professionnel") or ""))

    text = " ".join(chunks).lower()
    text = re.sub(r"[^\w\s./+#-]", " ", text)
    text = re.sub(r"\s+", " ", text).strip()
    rough_tokens = re.split(r"[\s,;:(){}\[\]<>|]+", text)
    tokens = {
        _canon_skill(t.strip(" .-_"))
        for t in rough_tokens
        if t.strip(" .-_") and len(t.strip(" .-_")) > 1
    }

    if " ai " in f" {text} " or " artificial intelligence " in f" {text} " or " intelligence artificielle " in f" {text} " or " integration ia " in f" {text} " or " integration ai " in f" {text} ":
        tokens.add("ia")
    if "n8n" in text:
        tokens.add("n8n")
    if "chatbot" in text or "chatbots" in text:
        tokens.add("chatbot")
    if "retrieval augmented generation" in text or " rag " in f" {text} ":
        tokens.add("rag")
    if "github actions" in text or "gitlab ci" in text or "ci cd" in text or "cicd" in text:
        tokens.add("ci/cd")
    return tokens


def _compute_deterministic_match(profile_data: dict, analyzed_offer: dict) -> dict:
    offer_keywords = analyzed_offer.get("keywords_ats", []) or []
    required = analyzed_offer.get("competences_requises", []) or []
    preferred = analyzed_offer.get("competences_souhaitees", []) or []
    normalized_targets = _build_matching_targets(required, preferred, offer_keywords)
    normalized_keywords = _collapse_ci_cd([_canon_skill(s) for s in normalize_skills([str(t) for t in offer_keywords if t])])

    profile_skills = []
    for c in profile_data.get("competences", []) or []:
        if isinstance(c, dict) and c.get("nom"):
            profile_skills.append(c.get("nom"))
    for s in profile_data.get("skills", []) or []:
        if isinstance(s, str):
            profile_skills.append(s)
        elif isinstance(s, dict):
            profile_skills.append(s.get("nom") or s.get("name") or "")

    normalized_profile_skills = _collapse_ci_cd([_canon_skill(s) for s in normalize_skills([str(s) for s in profile_skills if s])])
    profile_text = build_profile_full_text(profile_data)
    profile_tokens = set(normalized_profile_skills)
    profile_tokens.update(_profile_evidence_tokens(profile_data))
    profile_tokens.update(_canon_skill(t) for t in normalize_text(profile_text).split() if t)

    matched, partial, missing = [], [], []
    for t in normalized_targets:
        best = 0.0
        for p in profile_tokens:
            sim = _similarity(t, p)
            if sim > best:
                best = sim
        if best >= 0.80:
            matched.append(t)
        elif best >= 0.52:
            partial.append(t)
        else:
            missing.append(t)

    score_matching = int(round(((len(matched) + 0.5 * len(partial)) / max(1, len(normalized_targets))) * 100))
    score_matching = max(0, min(100, score_matching))

    kw_present, kw_missing = [], []
    for kw in normalized_keywords:
        best = 0.0
        for p in profile_tokens:
            sim = _similarity(kw, p)
            if sim > best:
                best = sim
        if best >= 0.80:
            kw_present.append(kw)
        else:
            kw_missing.append(kw)

    score_ats = int(round((len(kw_present) / max(1, len(normalized_keywords))) * 100))
    score_ats = max(0, min(100, score_ats))

    return {
        "matched_skills": matched,
        "partial_skills": partial,
        "missing_skills": missing,
        "keywords_presents": kw_present,
        "keywords_manquants": kw_missing,
        "score_matching": score_matching,
        "score_ats": score_ats,
    }


def _display_label(canonical: str, analyzed_offer: dict) -> str:
    labels = [
        *(analyzed_offer.get("competences_requises", []) or []),
        *(analyzed_offer.get("competences_souhaitees", []) or []),
        *(analyzed_offer.get("keywords_ats", []) or []),
    ]
    for label in labels:
        if _canon_skill(str(label)) == canonical:
            return str(label)
    special = {
        "ci/cd": "CI/CD",
        "ia": "IA",
        "rag": "RAG",
        "node.js": "Node.js",
        "express.js": "Express.js",
        "next.js": "Next.js",
        "postgresql": "PostgreSQL",
    }
    return special.get(canonical, canonical)


def _build_deterministic_match_result(profile_data: dict, analyzed_offer: dict) -> dict:
    deterministic = _compute_deterministic_match(profile_data or {}, analyzed_offer or {})
    matched = _collapse_ci_cd(deterministic["matched_skills"])
    partial = [s for s in _collapse_ci_cd(deterministic["partial_skills"]) if s not in set(matched)]
    missing = [
        s for s in _collapse_ci_cd(deterministic["missing_skills"])
        if s not in set(matched) and s not in set(partial)
    ]

    matched_labels = [_display_label(s, analyzed_offer) for s in matched]
    partial_labels = [_display_label(s, analyzed_offer) for s in partial]
    missing_labels = [_display_label(s, analyzed_offer) for s in missing]
    keyword_present = [_display_label(s, analyzed_offer) for s in _collapse_ci_cd(deterministic["keywords_presents"])]
    keyword_missing = [_display_label(s, analyzed_offer) for s in _collapse_ci_cd(deterministic["keywords_manquants"])]

    return {
        "candidate_name": "Candidat",
        "job_title": analyzed_offer.get("titre") or analyzed_offer.get("job_title") or "Poste",
        "relevance_score": round(deterministic["score_matching"] / 100, 2),
        "score_matching": deterministic["score_matching"],
        "score_ats": deterministic["score_ats"],
        "matched_skills": matched_labels,
        "missing_skills": missing_labels,
        "partial_skills": partial_labels,
        "competences_matching": matched_labels,
        "competences_manquantes": missing_labels,
        "keywords_presents": keyword_present,
        "keywords_manquants": keyword_missing,
        "required_certs": [],
        "cert_match": False,
        "experience_years": 0.0,
        "required_years": 0.0,
        "experience_gap_years": 0.0,
        "flag": "minor_gap" if deterministic["score_matching"] >= 60 else "critical_gap",
        "recommandations": [
            f"Ajouter une preuve concrete de '{skill}' dans une experience ou un projet."
            for skill in missing_labels[:5]
        ],
        "revision_hints": [],
        "analysis_source": "deterministic_skill_gap_v2",
        "errors": [],
    }


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

    match_result = _build_deterministic_match_result(
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

    match_result = _build_deterministic_match_result(profile_data, analyzed_offer)

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
    analyzed_offer = state.get("analyzed_offer")
    if not analyzed_offer or not analyzed_offer.get("entreprise"):
        return {"messages": [AIMessage(content="[Pipeline] Pas d'entreprise detectee, intelligence ignoree", name="orchestrator")]}

    result = await company_service.get_company_intelligence(
        company_name=analyzed_offer.get("entreprise"),
        job_title=analyzed_offer.get("titre", "Poste inconnu"),
        user_id=state.get("user_id", ""),
    )
    return {
        "company_intelligence": result,
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
        offer_skills=state.get("normalized_offer_skills", []),
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

            company_intel = state.get("company_intelligence")
            if company_intel:
                try:
                    await company_service.save_company_intelligence(db, company_intel, offer_id)
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
