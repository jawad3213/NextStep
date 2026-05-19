# ============================================================
# app/domain/company/service.py
# Couche service du domaine COMPANY — Intelligence Agent
# ============================================================
import logging
import json
from typing import Optional
from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy import text
from app.domain.company.schemas.state import CompanyState
from app.domain.company.graph.workflow import create_company_graph

logger = logging.getLogger(__name__)


class CompanyService:
    """Service du domaine COMPANY — Intelligence Agent."""

    def __init__(self):
        self.graph = create_company_graph()

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

        # Exécution du graphe
        final_state = await self.graph.ainvoke(initial_state)

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
        id_offre: Optional[str] = None
    ) -> dict:
        """
        Stocke les données d'intelligence générées par l'agent dans la table PostgreSQL 'intel_entreprise'.
        Utilise l'ORM SQLAlchemy pour rester synchro avec le modèle.
        """
        from app.core.models import IntelEntreprise

        intel = intelligence_data.get("intelligence", {})
        if not intel:
            logger.warning("⚠️ Aucune donnée d'intelligence trouvée pour la sauvegarde.")
            return {}

        company_name = intel.get("nom", "Inconnu")
        logger.info(f"💾 Sauvegarde de l'intelligence pour '{company_name}' dans PostgreSQL (ORM)...")

        offer_uuid = None
        if id_offre:
            try:
                import uuid
                offer_uuid = uuid.UUID(str(id_offre))
            except ValueError:
                pass

        # Supprimer l'ancienne intelligence si elle existe pour cette offre
        if offer_uuid:
            from sqlalchemy import select
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
        )

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
