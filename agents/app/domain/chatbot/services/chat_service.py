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
    FeedbackResult, DimensionScore, SalaryResult,
)
from app.domain.chatbot.schemas import (
    ArenaConfigSchema, MessageSchema,
    QuestionsResponse, QuestionOut,
    FreeChatResponse,
    StartInterviewResponse,
    SendMessageResponse,
    EndInterviewResponse, FeedbackOut, DimensionOut,
    SalaryResponse, NegotiationStepOut, SalaryContextSchema,
)

logger = logging.getLogger(__name__)
from .context_service import get_offer_context_from_db, get_candidature_id, _to_arena_config, _to_message_turns, get_internal_user_id

# SERVICE 2 — FREE CHAT (Tab 1 chat)
async def free_chat_service(
    user_input: str,
    thread_id: str,
    history: list[MessageSchema],
    offer_id: str | None,
    user_id: str,
    db: AsyncSession,
    mode: str | None = None,
    chat_type: str | None = None,
    arena_config: ArenaConfigSchema | None = None,
    salary_context: SalaryContextSchema | None = None,
) -> FreeChatResponse:
    """Répond à une question libre. Sauvegarde dans chat_message."""

    offer_ctx = None
    if offer_id:
        offer_ctx = await get_offer_context_from_db(offer_id, user_id, db)

    # Si le thread_id se termine par '-q', c'est une question de scénario de quiz, on ne la persiste pas forcément de la même manière, mais on garde la compatibilité
    db_chat_type = chat_type if chat_type else "questions"

    salary_res = None
    if salary_context:
        salary_res = SalaryResult(
            range_min=salary_context.range_min,
            range_max=salary_context.range_max,
            currency=salary_context.currency,
            your_target=salary_context.your_target,
            confidence_level="medium",
        )

    state = InterviewPrepState(
        session_id=thread_id,
        user_id=user_id,
        request_type="ask_free",
        mode=mode if mode in ["offer", "arena"] else ("offer" if offer_ctx else "arena"),
        offer_context=offer_ctx,
        arena_config=_to_arena_config(arena_config),
        salary=salary_res,
        messages=_to_message_turns(history),
        user_input=user_input,
        chat_type=db_chat_type,
    )

    result = await interview_graph.ainvoke(state)
    messages_out = result.get("messages", [])
    last_ai = next((m for m in reversed(messages_out) if m.role == "ai"), None)
    ai_content = last_ai.content if last_ai else ""

    # Sauvegarder les 2 messages (user + ai) dans chat_message si ce n'est pas un quiz temporaire
    if not (thread_id and thread_id.endswith('-q')):
        try:
            thread_uuid = uuid.UUID(thread_id) if thread_id else uuid.uuid4()
            internal_uid = await get_internal_user_id(user_id, db)

            db.add(ChatMessage(
                thread_id=thread_uuid,
                id_utilisateur=internal_uid,
                chat_type=db_chat_type,
                sender="user",
                content=user_input,
            ))
            db.add(ChatMessage(
                thread_id=thread_uuid,
                id_utilisateur=internal_uid,
                chat_type=db_chat_type,
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