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

from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy import select

from app.domain.chatbot.graph import interview_graph
from app.core.models import OffreAnalysee, IntelEntreprise, ResultatMatching
from app.domain.chatbot.models import (
    SessionCoaching, QuestionEntrainement, ChatMessage,
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
from .context_service import get_offer_context_from_db, get_candidature_id, _to_arena_config, _to_message_turns, get_internal_user_id

# SERVICE 3 — START INTERVIEW (Tab 2)
async def start_interview_service(
    mode: str,
    offer_id: str | None,
    arena_config: ArenaConfigSchema | None,
    user_id: str,
    db: AsyncSession,
    session_id: str | None = None,
) -> StartInterviewResponse:
    """Démarre la session. Crée la ligne en DB. Retourne le message d'ouverture."""

    offer_ctx = None
    if mode == "offer" and offer_id:
        offer_ctx = await get_offer_context_from_db(offer_id, user_id, db)

    # Créer la session en DB d'abord
    cfg = arena_config
    try:
        cand_id = await get_candidature_id(offer_id, user_id, db)
        internal_uid = await get_internal_user_id(user_id, db)
        session_uuid = uuid.UUID(session_id) if session_id else uuid.uuid4()
        session_db = SessionCoaching(
            id_session=session_uuid,
            id_utilisateur=internal_uid,
            id_candidature=cand_id,
            mode=mode,
            language=cfg.language if cfg else "en",
            duration_minutes=cfg.duration_minutes if cfg else 20,
            domain=cfg.domain if cfg else None,
            level=cfg.level if cfg else None,
            focus_areas=cfg.focus_areas if cfg else None,
            status="started",
        )
        db.add(session_db)
        await db.commit()
        session_id = str(session_db.id_session)
        # Vérifier que la session a bien un ID avant de l'utiliser
        if not session_id:
            raise ValueError("Session ID is None")
        logger.info(f"Session created in DB: {session_id}")

    except Exception as e:
        logger.error(f"DB create session error: {e}")
        await db.rollback()
        session_id = str(uuid.uuid4())

    # Appeler le graphe pour le message d'ouverture
    state = InterviewPrepState(
        session_id=session_id,
        user_id=user_id,
        mode=mode,
        request_type="start_interview",
        offer_context=offer_ctx,
        arena_config=_to_arena_config(arena_config),
        messages=[],
    )

    result = await interview_graph.ainvoke(state)
    messages_out = result.get("messages", [])
    opening = messages_out[-1].content if messages_out else "Hello! Let's begin the interview."

    # Sauvegarder le message d'ouverture
    try:
        internal_uid = await get_internal_user_id(user_id, db)
        db.add(ChatMessage(
            thread_id=uuid.UUID(session_id),
            id_utilisateur=internal_uid,
            id_session=uuid.UUID(session_id),
            chat_type="interview",
            sender="ai",
            content=opening,
        ))
        await db.commit()
    except Exception as e:
        logger.error(f"DB save opening message error: {e}")
        await db.rollback()

    return StartInterviewResponse(
        session_id=session_id,
        opening_message=opening,
    )

# SERVICE 4 — SEND MESSAGE (Tab 2)
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

# SERVICE 5 — END INTERVIEW + EVALUATE (Tab 2)
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

    # Mettre à jour la session en DB
    try:
        session_uuid = uuid.UUID(session_id)
        row = await db.get(SessionCoaching, session_uuid)
        if row:
            row.status          = "completed"
            row.score_entretien = score
            row.completed_at    = datetime.utcnow()
            row.feedback_json   = feedback.model_dump() if feedback else {}
            await db.commit()
            logger.info(f"Session {session_id} completed with score {score}")
        else:
            logger.warning(f"Session {session_id} not found in DB")

    except Exception as e:
        logger.error(f"DB update session error: {e}")
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