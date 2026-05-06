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
from app.domain.company.graph.company_graph import create_company_graph

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
    ) -> dict:
        """
        Lance le pipeline d'intelligence entreprise (Recherche + Analyse).
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
            "summary": final_state.get("company_summary")
        }

    async def save_company_intelligence(
        self,
        db: AsyncSession,
        intelligence_data: dict,
        id_offre: Optional[str] = None
    ) -> dict:
        """
        Stocke les données d'intelligence générées par l'agent dans la table PostgreSQL 'intel_entreprise'.
        Calcule dynamiquement le salaire min/max de l'offre et convertit les listes au format JSONB.
        """
        intel = intelligence_data.get("intelligence", {})
        if not intel:
            logger.warning("⚠️ Aucune donnée d'intelligence trouvée pour la sauvegarde.")
            return {}

        company_name = intel.get("nom", "Inconnu")
        logger.info(f"💾 Sauvegarde de l'intelligence pour '{company_name}' dans PostgreSQL...")

        # 1. Extraction dynamique des salaires globaux (min de junior, max de senior)
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

        # 2. Préparation des JSONB
        actualites_json = json.dumps(intel.get("actualites", []))
        questions_json = json.dumps(intel.get("interview_questions", []))

        # 3. Requête SQL brute robuste de sauvegarde
        query = text("""
            INSERT INTO intel_entreprise (
                nom_entreprise,
                id_offre,
                note_glassdoor,
                score_culture,
                salaire_min,
                salaire_max,
                devise_salaire,
                actualites,
                resume_entreprise,
                difficulte_entretien,
                questions_connues
            ) VALUES (
                :nom_entreprise,
                :id_offre,
                :note_glassdoor,
                :score_culture,
                :salaire_min,
                :salaire_max,
                :devise_salaire,
                :actualites,
                :resume_entreprise,
                :difficulte_entretien,
                :questions_connues
            )
            RETURNING id, date_collecte;
        """)

        params = {
            "nom_entreprise": company_name,
            "id_offre": id_offre if id_offre else None,
            "note_glassdoor": intel.get("culture", {}).get("glassdoor_rating"),
            "score_culture": intel.get("culture", {}).get("culture_score"),
            "salaire_min": salaire_min,
            "salaire_max": salaire_max,
            "devise_salaire": devise_salaire,
            "actualites": actualites_json,
            "resume_entreprise": intel.get("summary"),
            "difficulte_entretien": intel.get("interview_difficulty", "medium"),
            "questions_connues": questions_json
        }

        try:
            result = await db.execute(query, params)
            await db.commit()
            row = result.fetchone()
            if row:
                logger.info(f"✅ Intelligence stockée avec succès ! ID: {row[0]}")
                return {"id": str(row[0]), "date_collecte": str(row[1])}
        except Exception as e:
            await db.rollback()
            logger.error(f"❌ Erreur critique lors de l'insertion PostgreSQL : {e}")
            raise e

        return {}


# ── Singleton ─────────────────────────────────────────────────
company_service = CompanyService()
