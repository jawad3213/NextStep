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

# ══ HELPERS ══
# HELPERS — Conversions de types
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


async def get_candidature_id(offer_id: str | None, user_id: str | None, db: AsyncSession) -> uuid.UUID | None:
    """Trouve l'id_candidature à partir de l'id_offre et id_utilisateur."""
    if not offer_id or not user_id:
        return None
    try:
        from sqlalchemy import text
        r = await db.execute(
            text("SELECT id_candidature FROM candidature WHERE id_offre = :o AND id_utilisateur = :u"),
            {"o": uuid.UUID(offer_id), "u": uuid.UUID(user_id)}
        )
        return r.scalar_one_or_none()
    except Exception:
        return None

# ══ CONTEXT ══
# RÉCUPÉRATION DU CONTEXTE OFFRE DEPUIS LA DB
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
        user_uuid  = uuid.UUID(user_id) if user_id else None

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