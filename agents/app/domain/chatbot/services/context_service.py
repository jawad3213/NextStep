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
    SessionCoaching, QuestionEntrainement,
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
    """Trouve l'id_candidature à partir de l'id_offre et id_utilisateur."""
    if not offer_id or not user_id:
        return None
    try:
        internal_uid = await get_internal_user_id(user_id, db)
        if not internal_uid:
            return None
        from sqlalchemy import text
        r = await db.execute(
            text("SELECT id_candidature FROM candidature WHERE id_offre = :o AND id_utilisateur = :u LIMIT 1"),
            {"o": uuid.UUID(offer_id), "u": internal_uid}
        )
        return r.scalar()
    except Exception as e:
        logger.error(f"Error getting candidature ID: {e}")
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
        # 1. Résolution de l'utilisateur Keycloak ID -> UUID interne DB
        internal_user_uuid = await get_internal_user_id(user_id, db)
        user_uuid = internal_user_uuid if internal_user_uuid else (uuid.UUID(user_id) if user_id else None)

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

        # ── Fallback si les tables séparées sont vides mais l'offre a été traitée (offres_emploi) ── (DÉSACTIVÉ POUR TESTER LA DB EN DIRECT)
        # if not offre_row and not intel_row:
        #     from sqlalchemy import text
        #     r_emploi = await db.execute(
        #         text("SELECT analyse_json FROM offres_emploi WHERE id = :o"),
        #         {"o": offer_uuid}
        #     )
        #     row_emploi = r_emploi.scalar_one_or_none()
        #     if row_emploi:
        #         # SQLAlchemy parses jsonb columns to python dict/list automatically
        #         json_data = row_emploi if isinstance(row_emploi, dict) else {}
        #         if not json_data and isinstance(row_emploi, str):
        #             import json
        #             try:
        #                 json_data = json.loads(row_emploi)
        #             except Exception:
        #                 json_data = {}
        # 
        #         analyzed_offer = json_data.get("analyzed_offer") or {}
        #         company_intel = json_data.get("company_intelligence") or {}
        #         intel_sub = company_intel.get("intelligence") or {}
        #         skill_gap = json_data.get("skill_gap") or {}
        # 
        #         # Extraction des compétences et mots-clés
        #         req_skills = analyzed_offer.get("competences_requises") or []
        #         ats_kws = analyzed_offer.get("keywords_ats") or []
        #         t_stack = analyzed_offer.get("competences_souhaitees") or []
        #         exp_yrs = analyzed_offer.get("annees_experience") or 0
        # 
        #         comp_name = analyzed_offer.get("entreprise") or intel_sub.get("nom") or ""
        #         rating = intel_sub.get("rating") or 4.0
        #         diff = intel_sub.get("interview_difficulty") or "medium"
        #         questions = intel_sub.get("interview_questions") or []
        #         summary = intel_sub.get("summary") or company_intel.get("summary") or ""
        # 
        #         # Extraction des salaires
        #         salaries_list = intel_sub.get("salaries") or []
        #         s_min = 0
        #         s_max = 0
        #         curr = "MAD"
        #         if salaries_list and isinstance(salaries_list, list):
        #             first_sal = salaries_list[0]
        #             s_min = first_sal.get("min_salary") or 0
        #             s_max = first_sal.get("max_salary") or 0
        #             curr = first_sal.get("currency") or "MAD"
        # 
        #         # Matching
        #         score = skill_gap.get("compatibilityScore") or skill_gap.get("score") or company_intel.get("score") or 0
        #         missing = skill_gap.get("competences_manquantes") or []
        #         strengths = skill_gap.get("points_forts") or []
        # 
        #         return OfferContext(
        #             offer=OfferData(
        #                 offer_id=offer_id,
        #                 job_title=analyzed_offer.get("titre") or "",
        #                 company_name=comp_name,
        #                 required_skills=req_skills,
        #                 ats_keywords=ats_kws,
        #                 tech_stack=t_stack,
        #                 experience_years=exp_yrs,
        #                 location=analyzed_offer.get("localisation") or "",
        #                 contract_type=analyzed_offer.get("type_contrat") or "",
        #             ),
        #             company=CompanyData(
        #                 company_name=comp_name,
        #                 glassdoor_rating=float(rating) if rating else 0.0,
        #                 salary_min=int(s_min) if s_min else 0,
        #                 salary_max=int(s_max) if s_max else 0,
        #                 currency=curr,
        #                 company_summary=summary,
        #                 interview_difficulty=diff,
        #                 known_questions=questions,
        #             ),
        #             match=MatchData(
        #                 score_global=int(score) if score else 0,
        #                 missing_skills=missing,
        #                 strengths=strengths,
        #             ),
        #         )

        # Si aucune donnée ni dans les tables ni dans offres_emploi → retourner None
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