# ============================================================
# app/domain/company/service.py
# Couche service du domaine COMPANY — Intelligence Agent
# ============================================================
import logging
import uuid
from datetime import datetime, timedelta
from typing import Optional
from sqlalchemy import func, select
from sqlalchemy.exc import ProgrammingError
from sqlalchemy.ext.asyncio import AsyncSession
from app.core.models import IntelEntreprise
from app.core.time_utils import utc_now
from app.domain.company.schemas.state import CompanyState
from app.domain.company.agents.intelligence_agent import analyst_node, report_has_content, researcher_node

logger = logging.getLogger(__name__)

# A company report is reused for this many days, then the company is analysed again.
CACHE_DAYS = 7


def has_usable_data(report: Optional[dict]) -> bool:
    """False for "no data" reports: nothing found, or hollow (e.g. cached before web search worked)."""
    intelligence = (report or {}).get("intelligence") or {}
    return intelligence.get("data_available") is not False and report_has_content(intelligence)


def _offer_uuid(id_offre: Optional[str]) -> Optional[uuid.UUID]:
    try:
        return uuid.UUID(str(id_offre)) if id_offre else None
    except ValueError:
        return None


class CompanyService:
    """Service du domaine COMPANY — Intelligence Agent."""

    async def find_cached_intelligence(self, db: AsyncSession, company_name: str) -> Optional[tuple[dict, datetime]]:
        """
        (report, collected_at) of an analysis of this company made less than CACHE_DAYS ago
        (exact name, case-insensitive; no wildcard matching), or None.
        """
        name = (company_name or "").strip().lower()
        if not name:
            return None
        stmt = (
            select(IntelEntreprise.rapport_complet, IntelEntreprise.date_collecte)
            .where(func.lower(IntelEntreprise.nom_entreprise) == name)
            .where(IntelEntreprise.rapport_complet.is_not(None))
            .where(IntelEntreprise.date_collecte >= utc_now() - timedelta(days=CACHE_DAYS))
            .order_by(IntelEntreprise.date_collecte.desc())
            .limit(1)
        )
        try:
            row = (await db.execute(stmt)).first()
        except ProgrammingError as e:
            await db.rollback()
            logger.warning("Company cache lookup skipped: %s", e)
            return None
        if not row or not has_usable_data(row[0]):
            # An empty report (web search blocked, no source) must not stick for CACHE_DAYS.
            return None
        return row[0], row[1]

    async def has_offer_intelligence(self, db: AsyncSession, id_offre: Optional[str]) -> bool:
        """Whether an analysis is already linked to this offer (read by the interview coach)."""
        offer_uuid = _offer_uuid(id_offre)
        if offer_uuid is None:
            return False
        stmt = select(IntelEntreprise.id).where(IntelEntreprise.id_offre == offer_uuid).limit(1)
        return (await db.execute(stmt)).first() is not None

    async def get_company_intelligence(
        self,
        company_name: str,
        job_title: str,
        user_id: str = "",
        candidate_cv: Optional[dict] = None,
        job_offer: Optional[dict] = None,
    ) -> dict:
        """
        Lance le pipeline d'intelligence entreprise (Recherche + Analyse + Skill Gap).
        """
        logger.info(f"🏢 CompanyService.get_company_intelligence — '{company_name}' for '{job_title}'")

        initial_state: CompanyState = {
            "company_name": company_name,
            "job_title": job_title,
            "user_id": user_id,
            "messages": [],
            "errors": [],
            "raw_search_results": [],
            "pipeline_version": "3.0",
        }

        # Research (web sources), then analysis (LLM synthesis of those sources).
        final_state = dict(initial_state)
        final_state.update(await researcher_node(final_state))
        final_state.update(await analyst_node(final_state))

        return {
            "intelligence": final_state.get("intelligence"),
            "score": final_state.get("score"),
            "recommendations": final_state.get("recommendations", []),
            "summary": final_state.get("company_summary"),
            "skill_gap": final_state.get("skill_gap")
        }

    async def save_company_intelligence(
        self,
        db: AsyncSession,
        intelligence_data: dict,
        id_offre: Optional[str] = None,
        company_name: Optional[str] = None,
        collected_at: Optional[datetime] = None,
    ) -> dict:
        """
        Stocke les données d'intelligence générées par l'agent dans la table PostgreSQL 'intel_entreprise'.
        The full result is kept in rapport_complet (cache of /analyze-company), under the
        company name that was asked for (the report may spell it differently).
        Reports without real data are not stored: a failed search must not be cached.
        `collected_at`: when a reused report was collected (keeps its cache age); now by default.
        """
        intel = intelligence_data.get("intelligence", {})
        if not intel:
            logger.warning("⚠️ Aucune donnée d'intelligence trouvée pour la sauvegarde.")
            return {}

        if intel.get("data_available") is False:
            logger.info("Company intel without data: not cached.")
            return {}

        company_name = (company_name or intel.get("nom") or "Inconnu").strip()
        logger.info(f"💾 Sauvegarde de l'intelligence pour '{company_name}' dans PostgreSQL (ORM)...")

        offer_uuid = _offer_uuid(id_offre)

        # Supprimer l'ancienne intelligence si elle existe pour cette offre
        if offer_uuid:
            existing_intel = await db.execute(
                select(IntelEntreprise).where(IntelEntreprise.id_offre == offer_uuid)
            )
            for old_intel in existing_intel.scalars().all():
                await db.delete(old_intel)

        salaries = intel.get("salaries", [])
        salaire_min = None
        salaire_max = None
        devise_salaire = "MAD"
        if salaries:
            try:
                valid_mins = [s.get("min_salary") for s in salaries if s.get("min_salary") is not None]
                valid_maxs = [s.get("max_salary") for s in salaries if s.get("max_salary") is not None]
                if valid_mins:
                    salaire_min = min(valid_mins)
                if valid_maxs:
                    salaire_max = max(valid_maxs)
                devise_salaire = salaries[0].get("currency", "MAD")
            except Exception as e:
                logger.error(f"⚠️ Erreur lors du calcul des tranches salariales: {e}")

        row = IntelEntreprise(
            nom_entreprise=company_name,
            id_offre=offer_uuid,
            note_glassdoor=intel.get("culture", {}).get("glassdoor_rating"),
            salaire_min=salaire_min,
            salaire_max=salaire_max,
            devise_salaire=devise_salaire,
            resume_entreprise=intel.get("summary"),
            actualites=intel.get("actualites", []),
            difficulte_entretien=intel.get("interview_difficulty", "medium"),
            questions_connues=intel.get("interview_questions", []),
            rapport_complet=intelligence_data,
        )
        if collected_at is not None:
            row.date_collecte = collected_at

        try:
            db.add(row)
            await db.commit()
            await db.refresh(row)
            logger.info(f"✅ Intelligence stockée avec succès ! ID: {row.id}")
            return {"id": str(row.id), "date_collecte": str(row.date_collecte)}
        except Exception as e:
            await db.rollback()
            logger.error(f"❌ Erreur ORM lors de l'insertion PostgreSQL : {e}")
            raise e


# ── Singleton ─────────────────────────────────────────────────
company_service = CompanyService()
