# ============================================================
# app/core/config.py — Configuration centralisée (LLM + DB + URLs)
#
# Stratégie Anti-Rate-Limiting :
#   Chaque agent utilise un modèle DIFFÉRENT, réparti sur
#   plusieurs providers (Groq, Gemini, OpenAI) avec fallback
#   automatique en cas d'erreur (413 Payload Too Large, 429
#   Rate Limit, quota épuisé, etc.)
# ============================================================
import asyncio
import os
import logging
from functools import lru_cache
from pathlib import Path
from typing import Optional
from pydantic_settings import BaseSettings
from langchain_core.runnables import Runnable
import dotenv

logger = logging.getLogger(__name__)

dotenv.load_dotenv()

def find_env_file() -> str:
    """Recherche dynamiquement le fichier .env dans les dossiers parents."""
    current = Path(__file__).resolve()
    for _ in range(6):
        env_path = current / ".env"
        if env_path.exists():
            return str(env_path)
        agents_env = current / "agents" / ".env"
        if agents_env.exists():
            return str(agents_env)
        current = current.parent
    return "../.env"


# ─── Mapping Agent → Modèle Groq ─────────────────────────────
AGENT_MODEL_DEFAULTS = {
    "offer_analyzer":  "llama-3.1-8b-instant",
    "skill_gap":       "llama-3.1-8b-instant",
    "cv_optimizer":    "llama-3.3-70b-versatile",
    "company":         "llama-3.1-8b-instant",
    "resume":          "llama-3.3-70b-versatile",
    "default":         "llama-3.1-8b-instant",
}


class Settings(BaseSettings):
    # ─── Base de données ───
    DATABASE_URL: str = os.getenv("DATABASE_URL", "postgresql+asyncpg://admin:admin@localhost:5433/nextstep_db")

    # ─── LLM Provider (fallback priority) ───
    LLM_PROVIDER_PRIORITY: str = "groq,gemini,openai"
    LLM_PROVIDER: str = "groq"  # kept for backward compat, overridden by PRIORITY

    # ─── Groq ───
    GROQ_API_KEY: str = os.getenv("GROQ_API_KEY", "")
    GROQ_MODEL: str = "llama-3.1-8b-instant"
    GROQ_MODEL_PRECISE: str = "llama-3.3-70b-versatile"

    # ─── Per-Agent Model Override (via .env) ───
    GROQ_MODEL_OFFER_ANALYZER: str = ""
    GROQ_MODEL_SKILL_GAP: str = ""
    GROQ_MODEL_CV_OPTIMIZER: str = ""
    GROQ_MODEL_COMPANY: str = ""

    # ─── Google Gemini ───
    GEMINI_API_KEY: str = ""
    GEMINI_MODEL: str = "gemini-2.0-flash"

    # ─── OpenAI ───
    OPENAI_API_KEY: str = ""
    OPENAI_MODEL: str = "gpt-4o-mini"

    # ─── Search API ───
    TAVILY_API_KEY: str = os.getenv("TAVILY_API_KEY", "")

    # ─── Backend .NET ───
    DOTNET_BACKEND_URL: str = "http://localhost:5000"

    # ─── LLM settings ───
    LLM_TEMPERATURE: float = 0.1
    LLM_MAX_TOKENS: int = 2000

    # ─── JWT ───
    JWT_SECRET: str = os.getenv("Keycloak__ClientSecret", "secret-keycloak-local")
    jwt_algorithm: str = os.getenv("JWT_ALGORITHM", "HS256")

    class Config:
        env_file = find_env_file()
        env_file_encoding = "utf-8"
        extra = "ignore"


@lru_cache()
def get_settings() -> Settings:
    return Settings()


settings = get_settings()


def _resolve_groq_model(agent_name: Optional[str] = None) -> str:
    """Résout le modèle Groq à utiliser pour un agent donné."""
    if agent_name:
        env_overrides = {
            "offer_analyzer": settings.GROQ_MODEL_OFFER_ANALYZER,
            "skill_gap":      settings.GROQ_MODEL_SKILL_GAP,
            "cv_optimizer":   settings.GROQ_MODEL_CV_OPTIMIZER,
            "company":        settings.GROQ_MODEL_COMPANY,
        }
        env_val = env_overrides.get(agent_name, "")
        if env_val:
            return env_val
        if agent_name in AGENT_MODEL_DEFAULTS:
            return AGENT_MODEL_DEFAULTS[agent_name]
    return settings.GROQ_MODEL


# ═══════════════════════════════════════════════════════════════
# LLM PROVIDER FALLBACK WRAPPER
# ═══════════════════════════════════════════════════════════════

def _create_provider_llm(
    provider: str,
    agent_name: Optional[str] = None,
    temperature: Optional[float] = None,
    bound_kwargs: Optional[dict] = None,
    structured_output=None,
):
    """Crée une instance LLM pour un provider donné. Retourne None si non configuré."""
    temp = temperature if temperature is not None else settings.LLM_TEMPERATURE

    try:
        if provider == "groq":
            if not settings.GROQ_API_KEY:
                return None
            from langchain_groq import ChatGroq
            model = _resolve_groq_model(agent_name)
            llm = ChatGroq(
                model=model,
                api_key=settings.GROQ_API_KEY,
                temperature=temp,
                max_tokens=settings.LLM_MAX_TOKENS,
            )

        elif provider == "gemini":
            api_key = settings.GEMINI_API_KEY or os.getenv("GOOGLE_API_KEY")
            if not api_key:
                return None
            from langchain_google_genai import ChatGoogleGenerativeAI
            llm = ChatGoogleGenerativeAI(
                model=settings.GEMINI_MODEL,
                google_api_key=api_key,
                temperature=temp,
            )

        elif provider == "openai":
            if not settings.OPENAI_API_KEY:
                return None
            from langchain_openai import ChatOpenAI
            llm = ChatOpenAI(
                model=settings.OPENAI_MODEL,
                api_key=settings.OPENAI_API_KEY,
                temperature=temp,
            )
        else:
            return None

        # Structured output (e.g. with_structured_output)
        if structured_output:
            try:
                llm = llm.with_structured_output(structured_output)
            except (TypeError, AttributeError):
                logger.warning(f"{provider} ne supporte pas with_structured_output")
                return None

        # Bound kwargs (e.g. response_format={"type": "json_object"})
        if bound_kwargs:
            try:
                llm = llm.bind(**bound_kwargs)
            except (TypeError, ValueError):
                logger.info(f"{provider} ne supporte pas {bound_kwargs}")

        return llm

    except Exception as e:
        logger.warning(f"Échec init {provider}: {e}")
        return None


class _LLMProvider(Runnable):
    """Wrapper Runnable qui essaie plusieurs providers LLM avec fallback automatique.

    Hérite de Runnable LangChain pour être compatible avec `prompt | llm`.
    Ordre de fallback défini par LLM_PROVIDER_PRIORITY (env).
    """

    def __init__(
        self,
        agent_name: Optional[str] = None,
        temperature: Optional[float] = None,
        bound_kwargs: Optional[dict] = None,
    ):
        super().__init__()
        self._agent_name = agent_name
        self._temperature = temperature
        self._bound_kwargs = bound_kwargs or {}
        self._structured_output = None

    def bind(self, **kwargs):
        return _LLMProvider(
            agent_name=self._agent_name,
            temperature=self._temperature,
            bound_kwargs={**self._bound_kwargs, **kwargs},
        )

    def with_structured_output(self, schema, **kwargs):
        new = self.bind()
        new._structured_output = schema
        return new

    def _provider_list(self) -> list[str]:
        priority = os.getenv("LLM_PROVIDER_PRIORITY") or settings.LLM_PROVIDER_PRIORITY
        return [p.strip() for p in priority.split(",") if p.strip()]

    def invoke(self, input, config=None, **kwargs):
        last_error = None
        for provider in self._provider_list():
            llm = _create_provider_llm(
                provider=provider,
                agent_name=self._agent_name,
                temperature=self._temperature,
                bound_kwargs=self._bound_kwargs,
                structured_output=self._structured_output,
            )
            if llm is None:
                continue
            try:
                logger.info(f"[LLM] {provider} — agent={self._agent_name}")
                return llm.invoke(input, config=config, **kwargs)
            except Exception as e:
                logger.warning(f"⚠️ {provider} failed for agent={self._agent_name}: {e}")
                last_error = e
                continue
        logger.error(f"❌ Tous les providers ont échoué pour agent={self._agent_name}")
        raise last_error or RuntimeError("Aucun provider LLM disponible")

    async def ainvoke(self, input, config=None, **kwargs):
        last_error = None
        for provider in self._provider_list():
            llm = _create_provider_llm(
                provider=provider,
                agent_name=self._agent_name,
                temperature=self._temperature,
                bound_kwargs=self._bound_kwargs,
                structured_output=self._structured_output,
            )
            if llm is None:
                continue
            try:
                logger.info(f"[LLM] {provider} — agent={self._agent_name}")
                return await llm.ainvoke(input, config=config, **kwargs)
            except Exception as e:
                logger.warning(f"⚠️ {provider} failed for agent={self._agent_name}: {e}")
                last_error = e
                continue
        logger.error(f"❌ Tous les providers ont échoué pour agent={self._agent_name}")
        raise last_error or RuntimeError("Aucun provider LLM disponible")


def get_llm(temperature: float | None = None, agent_name: str | None = None):
    """
    Factory — retourne un wrapper avec fallback multi-provider.

    Ordre de tentative : Groq → Gemini → OpenAI (configurable via .env).

    Args:
        temperature: Température du LLM.
        agent_name:  Nom de l'agent ("offer_analyzer", "skill_gap", etc.)

    Exemples:
        get_llm()
        get_llm(agent_name="skill_gap")
        get_llm(agent_name="cv_optimizer", temperature=0.0)

    Configuration .env :
        LLM_PROVIDER_PRIORITY=groq,gemini,openai
        GEMINI_API_KEY=...
        OPENAI_API_KEY=...
    """
    return _LLMProvider(agent_name=agent_name, temperature=temperature)


def get_llm_precise():
    """Température basse pour extraction structurée (Groq uniquement)."""
    from langchain_groq import ChatGroq
    return ChatGroq(
        model=settings.GROQ_MODEL_PRECISE,
        api_key=settings.GROQ_API_KEY,
        temperature=0.0,
        max_tokens=settings.LLM_MAX_TOKENS,
    )
