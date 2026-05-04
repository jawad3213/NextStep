# ============================================================
# email_engine/llm.py — LLM provider factory
# Reads EMAIL_LLM_PROVIDER + EMAIL_LLM_MODEL from environment.
# Supported: gemini, groq, openai
# ============================================================
import os
import logging
from langchain_core.language_models.chat_models import BaseChatModel

logger = logging.getLogger(__name__)


def get_email_llm() -> BaseChatModel:
    """
    Return a configured LangChain chat model based on env variables.
    """
    provider = os.getenv("EMAIL_LLM_PROVIDER", "gemini").lower().strip()
    logger.info("Email LLM provider: %s", provider)

    if provider == "groq":
        from langchain_groq import ChatGroq  # type: ignore
        
        model = os.getenv("EMAIL_LLM_MODEL", "llama-3.3-70b-versatile")
        api_key = os.getenv("GROQ_API_KEY")
        
        if not api_key:
            raise ValueError("GROQ_API_KEY is missing from environment.")

        logger.info("Email LLM model (Groq): %s", model)
        return ChatGroq(
            model=model,
            temperature=0.3,
            api_key=api_key,
        )

    if provider == "gemini":
        from langchain_google_genai import ChatGoogleGenerativeAI  # type: ignore
        model = os.getenv("EMAIL_LLM_MODEL", "gemini-1.5-flash")
        api_key = os.getenv("GOOGLE_API_KEY")
        if not api_key:
            raise ValueError("GOOGLE_API_KEY is missing.")
        return ChatGoogleGenerativeAI(
            model=model,
            temperature=0.3,
            google_api_key=api_key,
        )

    if provider == "openai":
        from langchain_openai import ChatOpenAI  # type: ignore
        model = os.getenv("EMAIL_LLM_MODEL", "gpt-4o-mini")
        api_key = os.getenv("OPENAI_API_KEY")
        return ChatOpenAI(model=model, temperature=0.3, api_key=api_key)

    raise ValueError(f"Unsupported provider: {provider}")
