# ============================================================
# app/domain/email_composer/llm.py
#
# LLM provider factory for the Email Composer domain.
# Moved from legacy email_engine/llm.py to avoid circular imports.
# ============================================================
import os
import logging
from langchain_core.language_models.chat_models import BaseChatModel

logger = logging.getLogger(__name__)


def get_email_llm() -> BaseChatModel:
    """
    Return a configured LangChain chat model based on env variables.
    """
    requested_provider = os.getenv("EMAIL_LLM_PROVIDER", "").lower().strip()

    if requested_provider:
        providers = [requested_provider, "groq", "gemini", "openai"]
    else:
        # Default preference for email generation when no explicit provider is set.
        providers = ["groq", "gemini", "openai"]

    # Deduplicate while keeping order.
    ordered_providers: list[str] = []
    for provider in providers:
        if provider and provider not in ordered_providers:
            ordered_providers.append(provider)

    last_error: Exception | None = None
    for provider in ordered_providers:
        try:
            if provider == "groq":
                from langchain_groq import ChatGroq  # type: ignore

                model = os.getenv("EMAIL_LLM_MODEL", "llama-3.3-70b-versatile")
                api_key = os.getenv("GROQ_API_KEY")
                if not api_key:
                    raise ValueError("GROQ_API_KEY is missing from environment.")

                logger.info("Email LLM provider: groq | model: %s", model)
                return ChatGroq(
                    model=model,
                    temperature=0.3,
                    api_key=api_key,
                )

            if provider == "gemini":
                from langchain_google_genai import ChatGoogleGenerativeAI  # type: ignore

                model = os.getenv("EMAIL_LLM_MODEL", "gemini-1.5-flash")
                api_key = os.getenv("GOOGLE_API_KEY") or os.getenv("GEMINI_API_KEY")
                if not api_key:
                    raise ValueError("GOOGLE_API_KEY/GEMINI_API_KEY is missing.")

                logger.info("Email LLM provider: gemini | model: %s", model)
                return ChatGoogleGenerativeAI(
                    model=model,
                    temperature=0.3,
                    google_api_key=api_key,
                )

            if provider == "openai":
                from langchain_openai import ChatOpenAI  # type: ignore

                model = os.getenv("EMAIL_LLM_MODEL", "gpt-4o-mini")
                api_key = os.getenv("OPENAI_API_KEY")
                if not api_key:
                    raise ValueError("OPENAI_API_KEY is missing from environment.")

                logger.info("Email LLM provider: openai | model: %s", model)
                return ChatOpenAI(model=model, temperature=0.3, api_key=api_key)

            raise ValueError(f"Unsupported provider: {provider}")
        except Exception as exc:
            last_error = exc
            logger.warning("Email LLM provider '%s' unavailable: %s", provider, exc)
            continue

    raise ValueError(f"No usable Email LLM provider found. Last error: {last_error}")
