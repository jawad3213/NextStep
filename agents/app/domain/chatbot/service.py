"""
SERVICE — Logique métier du module chatbot.

RESPONSABILITÉS :
  1. Récupérer le contexte offre depuis la DB (outputs agents 2/3/4)
  2. Appeler le graphe LangGraph
  3. Persister les résultats en DB
  4. Retourner les schemas de réponse API

Le router appelle le service.
Le service appelle graph + DB.
Le service ne connaît pas HTTP.
"""

from __future__ import annotations
import uuid
import logging
from datetime import datetime
from sqlalchemy import update

from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy import select

from app.domain.chatbot.graph import interview_graph
from app.domain.chatbot.models import (
    SessionCoaching, QuestionEntrainement, ChatMessage,
)
from app.core.models import (
    OffreAnalysee, IntelEntreprise, ResultatMatching,
)
from app.domain.chatbot.state import (
    InterviewPrepState, ArenaConfig, MessageTurn,
    OfferContext, OfferData, CompanyData, MatchData,
    FeedbackResult, DimensionScore,
)
from app.domain.chatbot.schemas import (
    ArenaConfigSchema, MessageSchema,
    QuestionsResponse, QuestionOut,
    FreeChatResponse,
    StartInterviewResponse,
    SendMessageResponse,
    EndInterviewResponse, FeedbackOut, DimensionOut,
    SalaryResponse, NegotiationStepOut,
)

logger = logging.getLogger(__name__)


# ═══════════════════════════════════════════════════════════════
# HELPERS — Conversions de types
# ═══════════════════════════════════════════════════════════════

def _to_arena_config(schema: ArenaConfigSchema | None) -> ArenaConfig | None:
    if schema is None:
        return None
    return ArenaConfig(
        domain=schema.domain,
        level=schema.level,
        duration_minutes=schema.duration_minutes,
        language=schema.language,
        focus_areas=schema.focus_areas,
    )


def _to_message_turns(history: list[MessageSchema]) -> list[MessageTurn]:
    return [MessageTurn(role=m.role, content=m.content) for m in history]


async def get_internal_user_id(keycloak_id: str | None, db: AsyncSession) -> uuid.UUID | None:
    """Résout le Keycloak ID en internal id_utilisateur UUID."""
    if not keycloak_id:
        return None
    
    try:
        from sqlalchemy import text
        # 1. Priorité absolue : chercher par keycloak_id
        r = await db.execute(
            text("SELECT id_utilisateur FROM utilisateur WHERE keycloak_id = :k"),
            {"k": keycloak_id}
        )
        found_id = r.scalar()
        if found_id:
            return found_id

        # 2. Chercher par id_utilisateur (UUID)
        r = await db.execute(
            text("SELECT id_utilisateur FROM utilisateur WHERE id_utilisateur::text = :k"),
            {"k": keycloak_id}
        )
        found_id = r.scalar()
        if found_id:
            return found_id
            
        # 3. Si non trouvé, on tente de voir si c'est un UUID valide pour l'utiliser
        try:
            val_uuid = uuid.UUID(keycloak_id)
            return val_uuid
        except (ValueError, TypeError):
            return None
            
    except Exception as e:
        logger.error(f"Error resolving user {keycloak_id}: {e}")
        return None


async def get_candidature_id(offer_id: str | None, user_id: str | None, db: AsyncSession) -> uuid.UUID | None:
    """Trouve l'id_candidature à partir de l'id_offre et id_utilisateur (résolu)."""
    if not offer_id or not user_id:
        return None
    try:
        internal_uid = await get_internal_user_id(user_id, db)
        if not internal_uid:
            return None
            
        from sqlalchemy import text
        r = await db.execute(
            text("SELECT id_candidature FROM candidature WHERE id_offre = :o AND id_utilisateur = :u"),
            {"o": uuid.UUID(offer_id), "u": internal_uid}
        )
        return r.scalar_one_or_none()
    except Exception:
        return None


# ═══════════════════════════════════════════════════════════════
# RÉCUPÉRATION DU CONTEXTE OFFRE DEPUIS LA DB
# ═══════════════════════════════════════════════════════════════

async def get_offer_context_from_db(
    offer_id: str,
    user_id: str,
    db: AsyncSession,
) -> OfferContext | None:
    """
    Lit les outputs des agents 2, 3, 4 directement depuis PostgreSQL.
    Beaucoup plus rapide et fiable que passer par le .NET backend.

    Retourne None si l'offre n'est pas encore analysée.
    """
    if not offer_id:
        return None

    try:
        offer_uuid = uuid.UUID(offer_id)
        internal_uid = await get_internal_user_id(user_id, db)
        user_uuid = internal_uid if internal_uid else (uuid.UUID(user_id) if user_id else None)

        # ── Agent 2 : offre_analysee ───────────────────────
        r2 = await db.execute(
            select(OffreAnalysee).where(OffreAnalysee.id_offre == offer_uuid)
        )
        offre_row = r2.scalar_one_or_none()

        # ── Agent 3 : intel_entreprise ─────────────────────
        r3 = await db.execute(
            select(IntelEntreprise).where(IntelEntreprise.id_offre == offer_uuid)
        )
        intel_row = r3.scalar_one_or_none()

        # ── Agent 4 : resultat_matching ────────────────────
        match_row = None
        if user_uuid:
            r4 = await db.execute(
                select(ResultatMatching).where(
                    ResultatMatching.id_offre == offer_uuid,
                    ResultatMatching.id_utilisateur == user_uuid,
                )
            )
            match_row = r4.scalar_one_or_none()

        # Si aucune donnée → retourner None
        if not offre_row and not intel_row:
            logger.warning(f"No analyzed data found for offer {offer_id}")
            return None

        return OfferContext(
            offer=OfferData(
                offer_id=offer_id,
                job_title=offre_row.titre_poste       if offre_row else "",
                company_name=offre_row.entreprise     if offre_row else "",
                required_skills=offre_row.competences_requises or [] if offre_row else [],
                ats_keywords=offre_row.keywords_ats   or [] if offre_row else [],
                tech_stack=offre_row.stack_technique  or [] if offre_row else [],
                experience_years=offre_row.annees_experience or 0 if offre_row else 0,
                location=offre_row.localisation       if offre_row and offre_row.localisation else "",
                contract_type=offre_row.type_contrat   if offre_row and offre_row.type_contrat else "",
            ),
            company=CompanyData(
                company_name=intel_row.nom_entreprise         if intel_row else "",
                glassdoor_rating=intel_row.note_glassdoor     or 0.0 if intel_row else 0.0,
                salary_min=intel_row.salaire_min              or 0 if intel_row else 0,
                salary_max=intel_row.salaire_max              or 0 if intel_row else 0,
                currency=intel_row.devise_salaire             or "MAD" if intel_row else "MAD",
                company_summary=intel_row.resume_entreprise   or "" if intel_row else "",
                interview_difficulty=intel_row.difficulte_entretien or "medium" if intel_row else "medium",
                known_questions=intel_row.questions_connues   or [] if intel_row else [],
            ),
            match=MatchData(
                score_global=match_row.score_global                   if match_row else 0,
                missing_skills=match_row.competences_manquantes or [] if match_row else [],
                strengths=match_row.points_forts              or [] if match_row else [],
            ),
        )

    except Exception as e:
        logger.error(f"get_offer_context_from_db error: {e}")
        return None


# ═══════════════════════════════════════════════════════════════
# SERVICE 1 — QUESTIONS (Tab 1)
# ═══════════════════════════════════════════════════════════════

async def generate_questions_service(
    mode: str,
    user_id: str,
    offer_id: str | None,
    arena_config: ArenaConfigSchema | None,
    db: AsyncSession,
) -> QuestionsResponse:
    """Génère les questions via LangGraph sans aucune persistence en DB."""
    
    offer_ctx = None
    if mode == "offer" and offer_id:
        offer_ctx = await get_offer_context_from_db(offer_id, user_id, db)

    state = InterviewPrepState(
        user_id=user_id,
        mode=mode,
        request_type="generate_questions",
        offer_context=offer_ctx,
        arena_config=_to_arena_config(arena_config),
    )

    result = await interview_graph.ainvoke(state)
    questions_out = result.get("questions", [])

    return QuestionsResponse(
        session_id=None,
        mode=mode,
        total=len(questions_out),
        questions=[
            QuestionOut(
                id=q.id,
                question=q.question,
                type=q.type,
                source=q.source,
                company_specific=q.company_specific,
                tip=q.tip,
            )
            for q in questions_out
        ],
    )


# ═══════════════════════════════════════════════════════════════
# SERVICE 2 — FREE CHAT (Tab 1 chat)
# ═══════════════════════════════════════════════════════════════

async def free_chat_service(
    user_input: str,
    thread_id: str,
    history: list[MessageSchema],
    offer_id: str | None,
    user_id: str,
    db: AsyncSession,
) -> FreeChatResponse:
    """Répond à une question libre. Sauvegarde dans chat_message."""

    offer_ctx = None
    if offer_id:
        offer_ctx = await get_offer_context_from_db(offer_id, user_id, db)

    state = InterviewPrepState(
        session_id=thread_id,
        user_id=user_id,
        request_type="ask_free",
        offer_context=offer_ctx,
        messages=_to_message_turns(history),
        user_input=user_input,
    )

    result = await interview_graph.ainvoke(state)
    messages_out = result.get("messages", [])
    last_ai = next((m for m in reversed(messages_out) if m.role == "ai"), None)
    ai_content = last_ai.content if last_ai else ""

    # Sauvegarder les 2 messages (user + ai) dans chat_message
    try:
        thread_uuid = uuid.UUID(thread_id) if thread_id else uuid.uuid4()
        internal_uid = await get_internal_user_id(user_id, db)

        db.add(ChatMessage(
            thread_id=thread_uuid,
            id_utilisateur=internal_uid,
            chat_type="questions",
            sender="user",
            content=user_input,
        ))
        db.add(ChatMessage(
            thread_id=thread_uuid,
            id_utilisateur=internal_uid,
            chat_type="questions",
            sender="ai",
            content=ai_content,
        ))
        await db.commit()

    except Exception as e:
        logger.error(f"DB save error in free_chat: {e}")
        await db.rollback()

    return FreeChatResponse(
        thread_id=thread_id,
        response=ai_content,
    )


# ═══════════════════════════════════════════════════════════════
# SERVICE 3 — START INTERVIEW (Tab 2)
# ═══════════════════════════════════════════════════════════════

async def start_interview_service(
    session_id: str | None,
    mode: str,
    offer_id: str | None,
    arena_config: ArenaConfigSchema | None,
    user_id: str,
    db: AsyncSession,
    questions: list[QuestionOut] | None = None
) -> StartInterviewResponse:
    """Crée la session en DB seulement maintenant."""

    offer_ctx = None
    if mode == "offer" and offer_id:
        offer_ctx = await get_offer_context_from_db(offer_id, user_id, db)

    # 1. Résoudre les identifiants
    internal_uid = await get_internal_user_id(user_id, db)
    cand_id = await get_candidature_id(offer_id, user_id, db)
    
    # 2. Créer la session
    final_session_id = uuid.UUID(session_id) if session_id else uuid.uuid4()
    
    try:
        session_db = SessionCoaching(
            id_session=final_session_id,
            id_utilisateur=internal_uid,
            id_candidature=cand_id,
            mode=mode,
            language=arena_config.language if arena_config else "en",
            duration_minutes=arena_config.duration_minutes if arena_config else 20,
            domain=arena_config.domain if arena_config else None,
            level=arena_config.level if arena_config else None,
            status="started",
            date_session=datetime.utcnow()
        )
        db.add(session_db)
        
        # 3. Sauvegarder les questions si fournies
        if questions:
            for i, q in enumerate(questions):
                db.add(QuestionEntrainement(
                    id_session=final_session_id,
                    texte_question=q.question,
                    type_question=q.type,
                    source=q.source,
                    conseil_reponse=q.tip,
                    ordre=i
                ))
        
        await db.commit()
        logger.info(f"Session {final_session_id} created in DB for user {user_id}")
    except Exception as e:
        logger.error(f"Error creating session record: {e}")
        await db.rollback()

    # 4. Invoquer le graphe
    state = InterviewPrepState(
        session_id=str(final_session_id),
        user_id=user_id,
        mode=mode,
        request_type="start_interview",
        offer_context=offer_ctx,
        arena_config=_to_arena_config(arena_config),
        messages=[],
    )

    result = await interview_graph.ainvoke(state)
    messages_out = result.get("messages", [])
    opening = messages_out[-1].content if messages_out else "Hello! Let's begin."

    # 5. Sauvegarder le message d'ouverture
    try:
        db.add(ChatMessage(
            thread_id=final_session_id,
            id_utilisateur=internal_uid,
            id_session=final_session_id,
            chat_type="interview",
            sender="ai",
            content=opening,
        ))
        await db.commit()
    except Exception as e:
        logger.error(f"DB save opening message error: {e}")
        await db.rollback()

    return StartInterviewResponse(
        session_id=str(final_session_id),
        opening_message=opening,
    )


# ═══════════════════════════════════════════════════════════════
# SERVICE 4 — SEND MESSAGE (Tab 2)
# ═══════════════════════════════════════════════════════════════

async def send_message_service(
    session_id: str,
    user_input: str,
    history: list[MessageSchema],
    mode: str,
    offer_id: str | None,
    arena_config: ArenaConfigSchema | None,
    user_id: str,
    db: AsyncSession,
) -> SendMessageResponse:
    """Envoie un message et reçoit la réponse du recruteur IA."""

    offer_ctx = None
    if mode == "offer" and offer_id:
        offer_ctx = await get_offer_context_from_db(offer_id, user_id, db)

    state = InterviewPrepState(
        session_id=session_id,
        user_id=user_id,
        mode=mode,
        request_type="continue_interview",
        offer_context=offer_ctx,
        arena_config=_to_arena_config(arena_config),
        messages=_to_message_turns(history),
        user_input=user_input,
    )

    result = await interview_graph.ainvoke(state)
    messages_out = result.get("messages", [])
    last_ai = next((m for m in reversed(messages_out) if m.role == "ai"), None)
    ai_content = last_ai.content if last_ai else ""

    # Sauvegarder user + ai dans chat_message
    try:
        session_uuid = uuid.UUID(session_id)
        internal_uid = await get_internal_user_id(user_id, db)

        db.add(ChatMessage(
            thread_id=session_uuid,
            id_utilisateur=internal_uid,
            id_session=session_uuid,
            chat_type="interview",
            sender="user",
            content=user_input,
        ))
        db.add(ChatMessage(
            thread_id=session_uuid,
            id_utilisateur=internal_uid,
            id_session=session_uuid,
            chat_type="interview",
            sender="ai",
            content=ai_content,
        ))
        await db.commit()

    except Exception as e:
        logger.error(f"DB save message error: {e}")
        await db.rollback()

    return SendMessageResponse(
        session_id=session_id,
        ai_response=ai_content,
    )


# ═══════════════════════════════════════════════════════════════
# SERVICE 5 — END INTERVIEW + EVALUATE (Tab 2)
# ═══════════════════════════════════════════════════════════════

async def end_interview_service(
    session_id: str,
    history: list[MessageSchema],
    mode: str,
    offer_id: str | None,
    arena_config: ArenaConfigSchema | None,
    user_id: str,
    db: AsyncSession,
) -> EndInterviewResponse:
    """Termine la session, évalue, sauvegarde le score et le feedback en DB."""

    offer_ctx = None
    if mode == "offer" and offer_id:
        offer_ctx = await get_offer_context_from_db(offer_id, user_id, db)

    state = InterviewPrepState(
        session_id=session_id,
        user_id=user_id,
        mode=mode,
        request_type="end_interview",
        offer_context=offer_ctx,
        arena_config=_to_arena_config(arena_config),
        messages=_to_message_turns(history),
        session_complete=True,
    )

    result = await interview_graph.ainvoke(state)
    feedback: FeedbackResult | None = result.get("feedback")
    score = feedback.global_score if feedback else 0
    
    logger.info(f"[EVALUATOR] Evaluated session {session_id}. Score: {score}")

    # Mettre à jour la session en DB
    try:
        session_uuid = uuid.UUID(session_id)
        row = await db.get(SessionCoaching, session_uuid)
        if row:
            row.status          = "completed"
            row.score_entretien = score
            row.completed_at    = datetime.utcnow()
            # Stocker feedback comme dict/JSON
            row.feedback_json   = feedback.model_dump() if feedback else None
            
            # Flush pour s'assurer que la session est mise à jour avant les questions
            await db.flush()

            # Mise à jour des questions si feedback présent
            if feedback and feedback.dimensions:
                user_messages = [m for m in state.messages if m.role == "user"]
                dims = feedback.dimensions
                tips = feedback.coaching_tips if feedback else []

                for i, msg in enumerate(user_messages):
                    try:
                        await db.execute(
                            update(QuestionEntrainement)
                            .where(QuestionEntrainement.id_session == session_uuid)
                            .where(QuestionEntrainement.ordre == i)
                            .values(
                                reponse_utilisateur = msg.content,
                                score_reponse       = (dims[i].score * 10) if i < len(dims) else None,
                                correction_ia       = tips[i] if i < len(tips) else None
                            )
                        )
                    except Exception as qe:
                        logger.warning(f"Failed to update question {i} for session {session_id}: {qe}")

            await db.commit()
            logger.info(f"✅ Session {session_id} successfully saved to DB with score {score}")
        else:
            logger.error(f"❌ Session {session_id} not found in DB during end_interview")

    except Exception as e:
        logger.error(f"❌ End session processing error for {session_id}: {e}")
        await db.rollback()

    # Construire la réponse
    feedback_out = FeedbackOut(
        global_score=feedback.global_score if feedback else 0,
        dimensions=[DimensionOut(**d.model_dump()) for d in (feedback.dimensions if feedback else [])],
        strengths=feedback.strengths    if feedback else [],
        improvements=feedback.improvements if feedback else [],
        best_answer=feedback.best_answer   if feedback else "",
        worst_answer=feedback.worst_answer  if feedback else "",
        coaching_tips=feedback.coaching_tips if feedback else [],
    )

    return EndInterviewResponse(
        session_id=session_id,
        score=score,
        feedback=feedback_out,
    )


# ═══════════════════════════════════════════════════════════════
# SERVICE 6 — SALARY COACH (Tab 3)
# ═══════════════════════════════════════════════════════════════

async def get_salary_service(
    mode: str,
    offer_id: str | None,
    arena_config: ArenaConfigSchema | None,
    user_id: str,
    db: AsyncSession,
) -> SalaryResponse:
    """Génère l'analyse salariale et sauvegarde le thread dans chat_message."""

    offer_ctx = None
    if mode == "offer" and offer_id:
        offer_ctx = await get_offer_context_from_db(offer_id, user_id, db)

    thread_id = uuid.uuid4()

    state = InterviewPrepState(
        session_id=str(thread_id),
        user_id=user_id,
        mode=mode,
        request_type="get_salary",
        offer_context=offer_ctx,
        arena_config=_to_arena_config(arena_config),
    )

    result = await interview_graph.ainvoke(state)
    from app.domain.chatbot.state import SalaryResult
    salary: SalaryResult | None = result.get("salary")

    # Sauvegarder dans chat_message (type = salary)
    try:
        summary = (
            f"Salary analysis: {salary.range_min}–{salary.range_max} {salary.currency}"
            if salary else "Salary analysis requested"
        )
        internal_uid = await get_internal_user_id(user_id, db)
        cand_id = await get_candidature_id(offer_id, user_id, db)
        db.add(ChatMessage(
            thread_id=thread_id,
            id_utilisateur=internal_uid,
            id_candidature=cand_id,
            chat_type="salary",
            sender="ai",
            content=summary,
        ))
        await db.commit()

    except Exception as e:
        logger.error(f"DB save salary error: {e}")
        await db.rollback()

    if not salary:
        return SalaryResponse(
            range_min=0, range_max=0, currency="MAD",
            your_target=0, confidence_level="low",
            market_sources=[], negotiation_script=[],
        )

    return SalaryResponse(
        range_min=salary.range_min,
        range_max=salary.range_max,
        currency=salary.currency,
        your_target=salary.your_target,
        confidence_level=salary.confidence_level,
        market_sources=salary.market_sources,
        negotiation_script=[
            NegotiationStepOut(**step) for step in salary.negotiation_script
        ],
    )
