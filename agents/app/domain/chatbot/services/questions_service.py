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

# SERVICE 1 — QUESTIONS (Tab 1)
async def generate_questions_service(
    mode: str,
    offer_id: str | None,
    arena_config: ArenaConfigSchema | None,
    user_id: str,
    db: AsyncSession,
) -> QuestionsResponse:
    """
    Génère les questions via LangGraph.
    Sauvegarde chaque question dans question_entrainement.
    """

    # 1. Récupérer le contexte offre si mode offer
    offer_ctx = None
    if mode == "offer" and offer_id:
        offer_ctx = await get_offer_context_from_db(offer_id, user_id, db)

    # 2. Construire l'état initial
    state = InterviewPrepState(
        session_id=str(uuid.uuid4()),
        user_id=user_id,
        mode=mode,
        request_type="generate_questions",
        offer_context=offer_ctx,
        arena_config=_to_arena_config(arena_config),
    )

    # 3. Invoquer le graphe
    result = await interview_graph.ainvoke(state)
    questions_out = result.get("questions", [])

    if not questions_out:
        logger.warning("Graph returned 0 questions")

    # 4. Sauvegarder en DB (session temporaire pour les questions générées)
    try:
        cand_id = await get_candidature_id(offer_id, user_id, db)
        session_db = SessionCoaching(
            id_utilisateur=uuid.UUID(user_id) if user_id else None,
            id_candidature=cand_id,
            mode=mode,
            language=arena_config.language if arena_config else "en",
            duration_minutes=arena_config.duration_minutes if arena_config else 20,
            domain=arena_config.domain if arena_config else None,
            level=arena_config.level if arena_config else None,
            focus_areas=arena_config.focus_areas if arena_config else None,
            status="pending",
        )
        db.add(session_db)
        await db.flush()  # obtenir l'id_session sans commit

        for i, q in enumerate(questions_out):
            db.add(QuestionEntrainement(
                id_session=session_db.id_session,
                texte_question=q.question,
                type_question=q.type,
                source=q.source,
                company_specific=q.company_specific,
                conseil_reponse=q.tip,
                ordre=i,
            ))

        await db.commit()
        logger.info(f"Saved session {session_db.id_session} with {len(questions_out)} questions")

    except Exception as e:
        logger.error(f"DB save error in generate_questions: {e}")
        await db.rollback()

    # 5. Retourner le schema de réponse
    return QuestionsResponse(
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