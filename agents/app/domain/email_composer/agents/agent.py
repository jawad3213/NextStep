# ============================================================
# app/domain/email_composer/agents/agent.py
# LangGraph node function for the email_composer domain.
# ============================================================
import logging
from langchain_core.messages import AIMessage
from app.domain.email_composer.schemas.state import EmailComposerState
from app.domain.email_composer.service import generate_email_from_pipeline_state

logger = logging.getLogger(__name__)


async def email_composer_node(state: EmailComposerState) -> dict:
    """
    LangGraph node — Agent M4: Email Composer.

    Reads profile, offer, skill_gap, company_intelligence from pipeline state.
    Calls the service layer in pipeline mode.
    Returns email_draft on success, or surfaces errors/warnings without crashing.

    ┌─────────────────────────────────────────────────────────────────┐
    │  Reads from state  │  profile_data                             │
    │                    │  analyzed_offer / raw_offer_text          │
    │                    │  skill_gap (optional)                     │
    │                    │  company_intelligence (optional)          │
    │                    │  generation_options (optional)            │
    │  Writes to state   │  email_draft                              │
    │                    │  messages                                 │
    │  On degraded mode  │  warnings (appended)                      │
    │  On failure        │  errors (appended)                        │
    │  NEVER             │  sends email                              │
    └─────────────────────────────────────────────────────────────────┘
    """
    logger.info("Agent M4 [EmailComposer] — START")

    try:
        result = await generate_email_from_pipeline_state(state)
    except Exception as e:
        logger.error("Agent M4 [EmailComposer] — unexpected error: %s", e)
        return {
            "errors": [f"EmailComposer: unexpected error — {str(e)}"],
            "messages": [AIMessage(content="[EmailComposer] Erreur inattendue.", name="email_composer")],
        }

    # Service returned an error dict (graceful failure)
    if isinstance(result, dict) and "error" in result:
        logger.warning("Agent M4 [EmailComposer] — graceful failure: %s", result["error"])
        return {
            "errors": [result["error"]],
            "email_draft": None,
            "messages": [AIMessage(content=f"[EmailComposer] Génération impossible: {result['error']}", name="email_composer")],
        }

    # Success — write draft to state
    email_draft = result.model_dump()
    warnings: list[str] = []

    # Emit warnings if enrichment was absent
    if not state.get("skill_gap"):
        warnings.append("EmailComposer: skill_gap absent — email généré sans positionnement optimisé.")
    if not state.get("company_intelligence"):
        warnings.append("EmailComposer: company_intelligence absent — motivation générique utilisée.")

    logger.info(
        "Agent M4 [EmailComposer] — succeeded | subject=%s",
        email_draft.get("subject", "")[:60],
    )

    return {
        "email_draft": email_draft,
        "warnings": warnings,
        "messages": [AIMessage(
            content=f"[EmailComposer] Email généré : {email_draft.get('subject', '')}",
            name="email_composer",
        )],
    }
