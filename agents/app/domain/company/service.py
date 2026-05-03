# ============================================================
# app/domain/company/service.py
# Couche service du domaine COMPANY
# ============================================================
import logging
from app.domain.company.schemas.state import CompanyState
from app.domain.company.agents.company_analyzer import company_analyzer_node

logger = logging.getLogger(__name__)


class CompanyService:
    """Service du domaine COMPANY — analyse d'entreprise."""

    async def analyze_company(
        self,
        company_name: str,
        offer_data: dict,
        profile_data: dict | None = None,
        user_id: str = "",
    ) -> dict:
        """
        Analyse une entreprise à partir de l'offre.

        Returns:
            Dict avec company_info, company_culture_score, company_insights
        """
        logger.info("CompanyService.analyze_company — '%s'", company_name)

        state: CompanyState = {
            "company_name": company_name,
            "user_id":      user_id,
            "offer_data":   offer_data,
            "profile_data": profile_data or {},
            "messages": [], "errors": [],
            "pipeline_version": "2.1",
        }
        result = await company_analyzer_node(state)  # type: ignore[arg-type]
        return {
            "company_info":          result.get("company_info"),
            "company_culture_score": result.get("company_culture_score"),
            "company_insights":      result.get("company_insights", []),
        }


# ── Singleton ─────────────────────────────────────────────────
company_service = CompanyService()
