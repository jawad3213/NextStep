# ============================================================
# app/domain/cv_engine/service.py
# Couche service du domaine CV_ENGINE
#
# Orchestre les agents via le graphe LangGraph.
# Utilisé par les routes API — isole la logique du transport HTTP.
# ============================================================
import logging
from app.domain.cv_engine.schemas.state import CvEngineState
from app.domain.cv_engine.graph.workflow import get_cv_engine_workflow

logger = logging.getLogger(__name__)


class CvEngineService:
    """
    Service principal du domaine CV_ENGINE.

    Responsabilités :
      - Construire l'état initial du pipeline
      - Invoquer le graphe LangGraph
      - Convertir le state final en résultat API
      - Logger les métriques de performance
    """

    async def prepare_cv_data(
        self,
        user_id: str,
        template_slug: str = "modern",
        offer_data: dict | None = None,
        match_result: dict | None = None,
    ) -> dict:
        """
        Lance le pipeline complet CV Engine (Nodes 1-3).

        Args:
            user_id:       Identifiant Keycloak de l'utilisateur
            template_slug: Slug du template CV (modern, classic, etc.)
            offer_data:    Offre analysée (Agent 1) — optionnel
            match_result:  Scores de matching (Agent 4) — optionnel

        Returns:
            cv_json (dict) structuré pour QuestPDF
        """
        logger.info(
            "CvEngineService.prepare_cv_data — user_id=%s | template=%s | has_offer=%s",
            user_id, template_slug, offer_data is not None,
        )

        workflow = get_cv_engine_workflow()

        initial_state: CvEngineState = {
            # ── Entrées obligatoires ──
            "user_id":       user_id,
            "template_slug": template_slug,
            # ── Entrées optionnelles (offer domain) ──
            "offer_data":    offer_data,
            "match_result":  match_result,
            # ── Accumulatifs (initialisés vides) ──
            "messages": [],
            "errors":   [],
            # ── Scalaires (None = pas encore calculé) ──
            "raw_profile":      None,
            "optimized_skills": None,
            "ats_coverage":     None,
            "cv_json":          None,
            "next_node":        None,
            "pipeline_version": "1.0",
        }

        final_state: CvEngineState = await workflow.ainvoke(initial_state)

        nb_errors = len(final_state.get("errors") or [])
        nb_msgs   = len(final_state.get("messages") or [])
        logger.info(
            "CvEngineService.prepare_cv_data ✅ — user_id=%s | erreurs=%d | messages=%d",
            user_id, nb_errors, nb_msgs,
        )

        cv_json = final_state.get("cv_json") or {}

        # Inject ats_coverage into the result alongside cv_json
        ats_coverage = final_state.get("ats_coverage") or {
            "covered": 0, "total": 0, "percentage": 0.0,
            "keywords_present": [], "keywords_missing": [],
        }

        return {
            "cv_json":      cv_json,
            "ats_coverage": ats_coverage,
            "errors":       final_state.get("errors") or [],
            "messages":     _serialize_messages(final_state),
        }

    async def optimize_skills_only(
        self,
        user_id: str,
        match_result: dict,
    ) -> dict:
        """
        Lance uniquement les Nodes 1-2 (Profile Loader + Skill Optimizer).
        Utile pour un re-ranking rapide quand l'offre change.

        Returns:
            {"skills": [...], "ats_coverage": {...}}
        """
        logger.info(
            "CvEngineService.optimize_skills_only — user_id=%s",
            user_id,
        )

        from app.domain.cv_engine.agents.profile_loader import profile_loader_node
        from app.domain.cv_engine.agents.skill_optimizer import skill_optimizer_node

        state: CvEngineState = {
            "user_id":      user_id,
            "match_result": match_result,
            "messages": [],
            "errors":   [],
        }

        # Run Node 1
        result1 = await profile_loader_node(state)
        state.update(result1)  # type: ignore[arg-type]

        # Run Node 2
        result2 = await skill_optimizer_node(state)
        state.update(result2)  # type: ignore[arg-type]

        return {
            "skills":       state.get("optimized_skills") or [],
            "ats_coverage": state.get("ats_coverage") or {},
        }


def _serialize_messages(state: CvEngineState) -> list[dict]:
    """Convertit les messages LangChain en dicts sérialisables."""
    raw_msgs = state.get("messages") or []
    return [
        {
            "agent":   getattr(m, "name", "unknown"),
            "content": str(m.content),
        }
        for m in raw_msgs
    ]


# ── Singleton ─────────────────────────────────────────────────
cv_engine_service = CvEngineService()
