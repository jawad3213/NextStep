# ============================================================
# app/domain/email_composer/llm.py
#
# LLM for the Email Composer domain: the shared multi-provider wrapper (app.core.config),
# so emails get the same provider fallback, timeouts and token limits as the other agents.
# ============================================================
from app.core.config import get_llm

EMAIL_TEMPERATURE = 0.3


def get_email_llm():
    """Chat model for emails (Groq gpt-oss-120b by default, then the other providers)."""
    return get_llm(temperature=EMAIL_TEMPERATURE, agent_name="email")
