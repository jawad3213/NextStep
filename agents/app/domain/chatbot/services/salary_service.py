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
from .context_service import get_offer_context_from_db, get_candidature_id, _to_arena_config, _to_message_turns

# SERVICE 6 — SALARY COACH (Tab 3)
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
        cand_id = await get_candidature_id(offer_id, user_id, db)
        db.add(ChatMessage(
            thread_id=thread_id,
            id_utilisateur=uuid.UUID(user_id) if user_id else None,
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