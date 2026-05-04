# ============================================================
# app/domain/offer/service.py
# Couche service du domaine OFFER
#
# Orchestre les agents via le graphe LangGraph.
# Utilisé par les routes API — isole la logique du transport HTTP.
# ============================================================
import logging
from app.domain.offer.schemas.state import OfferState
from app.domain.offer.schemas.offer_schemas import PipelineResult
from app.domain.offer.graph.workflow import get_offer_workflow

logger = logging.getLogger(__name__)


class OfferService:
    """
    Service principal du domaine OFFER.

    Responsabilités :
      - Construire l'état initial du pipeline
      - Invoquer le graphe LangGraph
      - Convertir le state final en PipelineResult
      - Logger les métriques de performance
    """

    async def run_pipeline(
        self,
        raw_text: str,
        user_id: str,
        template_id: int = 1,
    ) -> PipelineResult:
        """
        Lance le pipeline complet (Agents 1-4 + stubs 5-6).

        Args:
            raw_text:    Texte brut de l'offre d'emploi
            user_id:     Identifiant Keycloak de l'utilisateur
            template_id: Template CV sélectionné (1-3)

        Returns:
            PipelineResult contenant tous les résultats + erreurs accumulées
        """
        logger.info(
            "OfferService.run_pipeline — user_id=%s | template=%d | len(text)=%d",
            user_id, template_id, len(raw_text),
        )

        workflow = get_offer_workflow()

        initial_state: OfferState = {
            # ── Entrées obligatoires ──
            "raw_offer_text": raw_text,
            "user_id":        user_id,
            "template_id":    template_id,
            # ── Accumulatifs (initialisés vides) ──
            "messages":                  [],
            "errors":                    [],
            "normalized_offer_skills":   [],
            "normalized_profile_skills": [],
            "normalized_keywords":       [],
            # ── Scalaires (None = pas encore calculé) ──
            "analyzed_offer":    None,
            "profile_data":      None,
            "profile_full_text": "",
            "match_result":      None,
            "cv_template_json":  None,
            "email_draft":       None,
            "next_agent":        None,
            "pipeline_version":  "2.1",
        }

        final_state: OfferState = await workflow.ainvoke(initial_state)

        nb_errors = len(final_state.get("errors") or [])
        nb_msgs   = len(final_state.get("messages") or [])
        logger.info(
            "OfferService.run_pipeline ✅ — user_id=%s | erreurs=%d | messages=%d",
            user_id, nb_errors, nb_msgs,
        )

        return PipelineResult(
            user_id=user_id,
            analyzed_offer=final_state.get("analyzed_offer"),
            profile_data=final_state.get("profile_data"),
            match_result=final_state.get("match_result"),
            cv_template_json=final_state.get("cv_template_json"),
            email_draft=final_state.get("email_draft"),
            errors=final_state.get("errors") or [],
        )

    async def analyze_offer_only(self, raw_text: str, user_id: str) -> dict:
        """
        Lance uniquement Agent 1 (Offer Analyzer LLM).
        Utilisé par GET /analyze-offer pour les tests rapides.

        Returns:
            Dict JSON de l'offre analysée ou {} si erreur
        """
        from app.domain.offer.agents.offer_analyzer import offer_analyzer_node
        state: OfferState = {
            "raw_offer_text": raw_text,
            "user_id": user_id,
            "template_id": 1,
            "messages": [], "errors": [],
            "normalized_offer_skills": [],
            "normalized_profile_skills": [],
            "normalized_keywords": [],
        }
        result = await offer_analyzer_node(state)
        return result.get("analyzed_offer") or {}

    async def match_profile(self, user_id: str, analyzed_offer: dict) -> dict:
        """
        Lance uniquement Agents 2-3-4 (Profile → Normalizer → Scorer).
        Utilisé par POST /match quand l'analyse est déjà faite.

        Returns:
            Dict match_result ou {} si erreur
        """
        from app.domain.offer.agents.profile_retriever import profile_retriever_node
        from app.domain.offer.agents.normalizer import normalizer_node
        from app.domain.offer.agents.scorer import scorer_node

        state: OfferState = {
            "user_id":        user_id,
            "analyzed_offer": analyzed_offer,
            "messages": [], "errors": [],
            "normalized_offer_skills":   [],
            "normalized_profile_skills": [],
            "normalized_keywords":       [],
            "profile_full_text": "",
        }
        state.update(await profile_retriever_node(state))  # type: ignore[arg-type]
        state.update(await normalizer_node(state))          # type: ignore[arg-type]
        state.update(await scorer_node(state))              # type: ignore[arg-type]
        return state.get("match_result") or {}


# ── Singleton du service ───────────────────────────────────────
offer_service = OfferService()
