"""
SERVICE — Logique métier du module chatbot.

RESPONSABILITÉS :
  1. Récupérer le contexte de l'offre (tables agents + backend)
  2. Appeler le graphe LangGraph
  3. Retourner les schemas de réponse API

Les sessions et questions sont enregistrées par le backend (module Coaching).

Le router appelle le service.
Le service appelle le graphe.
Le service ne connaît pas HTTP.
"""

from __future__ import annotations
import uuid
import logging

from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy import select

from app.core import backend_client
from app.core.models import OffreAnalysee, IntelEntreprise, ResultatMatching
from app.domain.chatbot.state import ArenaConfig, MessageTurn, OfferContext, OfferData, CompanyData, MatchData
from app.domain.chatbot.schemas import ArenaConfigSchema, MessageSchema

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
    """Local user id for a Keycloak id or local id (backend Profile module)."""
    if not keycloak_id:
        return None
    try:
        user = await backend_client.get_user(keycloak_id)
        if user and user.get("userId"):
            return uuid.UUID(str(user["userId"]))
    except (backend_client.BackendError, ValueError) as e:
        logger.error(f"Error resolving user {keycloak_id}: {e}")
        return None
    # Unknown to the backend: keep a well-formed id as-is (sessions stay tied to it).
    try:
        return uuid.UUID(keycloak_id)
    except (ValueError, TypeError):
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

        # The agents' tables are only filled by CV generation. An offer that was only
        # analysed (step 2) has its analysis in the backend's offres_emploi.analyse_json.
        if not offre_row and not intel_row:
            from_analysis = await _context_from_offer_analysis(offer_id, user_id)
            if from_analysis:
                return from_analysis

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


async def _context_from_offer_analysis(offer_id: str, user_id: str | None) -> OfferContext | None:
    """Offer context from the analysis the backend stored for this user's offer."""
    if not user_id:
        return None
    try:
        data = await backend_client.get_offer_analysis(user_id, offer_id)
    except backend_client.BackendError as e:
        logger.error(f"Offer analysis unavailable for {offer_id}: {e.message}")
        return None
    if not isinstance(data, dict):
        return None

    offer = data.get("analyzed_offer") or {}
    if not offer:
        return None
    gap = data.get("skill_gap_analysis") or data.get("match_result") or data.get("skill_gap") or {}
    company_intel = data.get("company_intelligence") or {}
    intel = company_intel.get("intelligence") or {}
    culture = intel.get("culture") or {}
    salaries = intel.get("salaries") or []
    salary = salaries[0] if salaries and isinstance(salaries[0], dict) else {}

    def as_int(value) -> int:
        try:
            return int(float(value))
        except (TypeError, ValueError):
            return 0

    company_name = offer.get("entreprise") or intel.get("nom") or ""
    return OfferContext(
        offer=OfferData(
            offer_id=offer_id,
            job_title=offer.get("titre") or "",
            company_name=company_name,
            required_skills=offer.get("competences_requises") or [],
            ats_keywords=offer.get("keywords_ats") or [],
            tech_stack=offer.get("competences_souhaitees") or [],
            experience_years=as_int(offer.get("annees_experience")),
            location=offer.get("localisation") or "",
            contract_type=offer.get("type_contrat") or "",
        ),
        company=CompanyData(
            company_name=company_name,
            glassdoor_rating=float(culture.get("glassdoor_rating") or 0.0),
            salary_min=as_int(salary.get("min_salary")),
            salary_max=as_int(salary.get("max_salary")),
            currency=salary.get("currency") or "MAD",
            company_summary=intel.get("summary") or company_intel.get("summary") or "",
            interview_difficulty=intel.get("interview_difficulty") or "medium",
            known_questions=intel.get("interview_questions") or [],
        ),
        match=MatchData(
            score_global=as_int(gap.get("score_matching")),
            missing_skills=gap.get("missing_skills") or gap.get("competences_manquantes") or [],
            strengths=gap.get("matched_skills") or gap.get("competences_matching") or [],
        ),
    )
