"""
SCHEMAS API — Pydantic models pour les requêtes et réponses FastAPI.

DISTINCTION IMPORTANTE :
  state.py   → modèles internes LangGraph (circulent dans le graphe)
  schemas.py → modèles API (ce que Angular envoie / reçoit)

Un schema API est toujours plus simple que le state interne.
Le service fait le mapping entre les deux.
"""

from __future__ import annotations
from typing import Optional, Literal
from pydantic import BaseModel, Field


# ═══════════════════════════════════════════════════════════════
# SOUS-MODÈLES COMMUNS
# ═══════════════════════════════════════════════════════════════

class ArenaConfigSchema(BaseModel):
    """Config Arena envoyée par le frontend après le stepper."""
    domain: str
    level: Literal["junior", "mid", "senior"] = "junior"
    duration_minutes: int = Field(default=20, ge=5, le=60)
    language: str = "en"
    focus_areas: list[str] = []


class MessageSchema(BaseModel):
    """Un message de conversation."""
    role: Literal["user", "ai"]
    content: str


# ═══════════════════════════════════════════════════════════════
# REQUÊTES (Angular → FastAPI)
# ═══════════════════════════════════════════════════════════════

class QuestionsRequest(BaseModel):
    """
    Tab 1 — Générer les questions.
    Mode offer  : fournir offer_id
    Mode arena  : fournir arena_config
    """
    mode: Literal["offer", "arena"]
    offer_id: Optional[str] = None
    arena_config: Optional[ArenaConfigSchema] = None
    user_id: str = ""
    user_token: str = ""


class SalaryContextSchema(BaseModel):
    """Informations de salaire actuelles de l'interface."""
    range_min: int = 0
    range_max: int = 0
    currency: str = "MAD"
    your_target: int = 0


class FreeChatRequest(BaseModel):
    """Tab 1 — Question libre dans le chat."""
    user_input: str
    thread_id: str
    history: list[MessageSchema] = []
    offer_id: Optional[str] = None
    user_id: str = ""
    user_token: str = ""
    mode: Optional[str] = None
    chat_type: Optional[str] = None
    arena_config: Optional[ArenaConfigSchema] = None
    salary_context: Optional[SalaryContextSchema] = None


class StartInterviewRequest(BaseModel):
    """Tab 2 — Démarrer la session mock interview."""
    session_id: Optional[str] = None
    mode: Literal["offer", "arena"]
    offer_id: Optional[str] = None
    arena_config: Optional[ArenaConfigSchema] = None
    user_id: str
    user_token: str = ""


class SendMessageRequest(BaseModel):
    """Tab 2 — Envoyer une réponse pendant l'interview."""
    session_id: str
    user_input: str
    history: list[MessageSchema]
    mode: Literal["offer", "arena"]
    offer_id: Optional[str] = None
    arena_config: Optional[ArenaConfigSchema] = None
    user_id: str = ""
    user_token: str = ""


class EndInterviewRequest(BaseModel):
    """Tab 2 — Terminer la session et obtenir le feedback."""
    session_id: str
    history: list[MessageSchema]
    mode: Literal["offer", "arena"]
    offer_id: Optional[str] = None
    arena_config: Optional[ArenaConfigSchema] = None
    user_id: str = ""
    user_token: str = ""


class SalaryRequest(BaseModel):
    """Tab 3 — Analyse salariale et script de négociation."""
    mode: Literal["offer", "arena"]
    offer_id: Optional[str] = None
    arena_config: Optional[ArenaConfigSchema] = None
    user_id: str = ""
    user_token: str = ""


# ═══════════════════════════════════════════════════════════════
# RÉPONSES (FastAPI → Angular)
# ═══════════════════════════════════════════════════════════════

class QuestionOut(BaseModel):
    """Une question dans la réponse API."""
    id: str
    question: str
    type: str
    source: str
    company_specific: bool
    tip: str


class QuestionsResponse(BaseModel):
    """Réponse de POST /questions."""
    status: str = "ok"
    session_id: str | None = None
    mode: str
    total: int
    questions: list[QuestionOut]


class FreeChatResponse(BaseModel):
    """Réponse de POST /free-chat."""
    status: str = "ok"
    thread_id: str
    response: str


class StartInterviewResponse(BaseModel):
    """Réponse de POST /interview/start."""
    status: str = "ok"
    session_id: str
    opening_message: str


class SendMessageResponse(BaseModel):
    """Réponse de POST /interview/message."""
    status: str = "ok"
    session_id: str
    ai_response: str


class DimensionOut(BaseModel):
    name: str
    score: int
    comment: str


class FeedbackOut(BaseModel):
    global_score: int
    dimensions: list[DimensionOut]
    strengths: list[str]
    improvements: list[str]
    best_answer: str
    worst_answer: str
    coaching_tips: list[str]


class EndInterviewResponse(BaseModel):
    """Réponse de POST /interview/end."""
    status: str = "ok"
    session_id: str
    score: int
    feedback: FeedbackOut


class NegotiationStepOut(BaseModel):
    step: int
    action: str
    phrase: str
    why: str


class SalaryResponse(BaseModel):
    """Réponse de POST /salary."""
    status: str = "ok"
    range_min: int
    range_max: int
    currency: str
    your_target: int
    confidence_level: str
    market_sources: list[str]
    negotiation_script: list[NegotiationStepOut]
