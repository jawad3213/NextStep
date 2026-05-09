# ============================================================
# app/domain/email_composer/schemas/state.py
# LangGraph TypedDict state for the email_composer domain.
# Same style/reducers as offer_analyzer, skill_gap, company states.
# ============================================================
import operator
from typing import Annotated, Optional, TypedDict
from langchain_core.messages import BaseMessage
from langgraph.graph.message import add_messages


class EmailComposerState(TypedDict, total=False):
    """État interne de l'agent email_composer.

    Cycle de vie dans le pipeline principal :
    1. START — profile_data, analyzed_offer/raw_offer_text, options injected from pipeline state
    2. Optional enrichment — skill_gap, company_intelligence from preceding nodes
    3. email_composer_node — calls service.generate_email_from_pipeline_state()
    4. END — email_draft written to state
    """

    # ── Core inputs ───────────────────────────────────────────
    user_id: str
    candidature_id: Optional[str]

    profile_data: Optional[dict]
    """Candidate profile from profile_retriever agent."""

    analyzed_offer: Optional[dict]
    """Structured offer from offer_analyzer agent."""

    raw_offer_text: Optional[str]
    """Raw offer text fallback when analyzed_offer is unavailable."""

    # ── Optional enrichment from other pipeline agents ────────
    skill_gap: Optional[dict]
    """Skill gap analysis from skill_gap agent. If absent, email is still generated."""

    company_intelligence: Optional[dict]
    """Company intelligence from company agent. If absent, generic motivation used."""

    # ── Generation options ────────────────────────────────────
    generation_options: Optional[dict]
    """Dict with keys: language, tone, include_motivation_letter."""

    # ── Output ────────────────────────────────────────────────
    email_draft: Optional[dict]
    """Generated email draft: {subject, body, language, tone}."""

    # ── Reducers / debug ──────────────────────────────────────
    messages: Annotated[list[BaseMessage], add_messages]
    errors: Annotated[list[str], operator.add]
    warnings: Annotated[list[str], operator.add]
    iteration_count: int
