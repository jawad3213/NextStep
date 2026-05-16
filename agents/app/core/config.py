import logging
from functools import lru_cache
from pathlib import Path
from typing import Optional, List, Dict

import dotenv
from langchain_core.runnables import Runnable
from pydantic_settings import BaseSettings, SettingsConfigDict

logger = logging.getLogger(__name__)

# Charge le fichier .env
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
    return str(Path.cwd() / ".env")

# Modèles par défaut pour Groq
GROQ_DEFAULTS = {
    "offer_analyzer": "llama-3.1-8b-instant",
    "skill_gap": "llama-3.1-8b-instant",
    "cv_optimizer": "llama-3.3-70b-versatile",
    "company": "llama-3.1-8b-instant",
    "resume": "llama-3.3-70b-versatile",
    "default": "llama-3.1-8b-instant",
}

class Settings(BaseSettings):
    """Configuration de l'application via variables d'environnement."""
    
    # --- Infrastructure & Database ---
    DATABASE_URL: str = "postgresql+asyncpg://admin:admin@localhost:5433/nextstep_db"
    DOTNET_BACKEND_URL: str = "http://localhost:5000"
    
    # --- LLM Core Settings ---
    LLM_PROVIDER_PRIORITY: str = "groq,gemini,openai"
    LLM_PRIORITY_SKILL_GAP: str = "gemini,groq"
    LLM_PRIORITY_CV_OPTIMIZER: str = "gemini,groq"
    LLM_TEMPERATURE: float = 0.1
    LLM_MAX_TOKENS: int = 2000
    
    # --- Groq Configuration ---
    GROQ_API_KEY: str = ""
    GROQ_MODEL: str = "llama-3.1-8b-instant"
    GROQ_MODEL_PRECISE: str = "llama-3.3-70b-versatile"
    # Agent Overrides
    GROQ_MODEL_OFFER_ANALYZER: str = ""
    GROQ_MODEL_SKILL_GAP: str = ""
    GROQ_MODEL_CV_OPTIMIZER: str = ""
    GROQ_MODEL_COMPANY: str = ""

    # --- Gemini / Google Configuration ---
    GEMINI_API_KEY: str = ""
    GOOGLE_API_KEY: str = ""  # Fallback
    GEMINI_MODEL: str = "gemini-2.0-flash"
    # Agent Overrides
    GEMINI_MODEL_SKILL_GAP: str = ""
    GEMINI_MODEL_CV_OPTIMIZER: str = ""

    # --- OpenAI Configuration ---
    OPENAI_API_KEY: str = ""
    OPENAI_MODEL: str = "gpt-4o-mini"
    # Agent Overrides
    OPENAI_MODEL_SKILL_GAP: str = ""
    OPENAI_MODEL_CV_OPTIMIZER: str = ""

    # --- External APIs ---
    TAVILY_API_KEY: str = ""

    # --- Security & Auth ---
    JWT_SECRET: str = "secret-keycloak-local"
    JWT_ALGORITHM: str = "HS256"

    model_config = SettingsConfigDict(
        env_file=find_env_file(),
        env_file_encoding="utf-8",
        extra="ignore"
    )

    @property
    def effective_gemini_api_key(self) -> str:
        return self.GEMINI_API_KEY or self.GOOGLE_API_KEY

@lru_cache()
def get_settings() -> Settings:
    return Settings()

settings = get_settings()

# --- Resolution Helpers ---

def _resolve_model(provider: str, agent_name: Optional[str] = None) -> str:
    """Résout le modèle à utiliser selon le provider et l'agent."""
    if provider == "groq":
        if agent_name:
            overrides = {
                "offer_analyzer": settings.GROQ_MODEL_OFFER_ANALYZER,
                "skill_gap": settings.GROQ_MODEL_SKILL_GAP,
                "cv_optimizer": settings.GROQ_MODEL_CV_OPTIMIZER,
                "company": settings.GROQ_MODEL_COMPANY,
            }
            if val := overrides.get(agent_name): return val
            if val := GROQ_DEFAULTS.get(agent_name): return val
        return settings.GROQ_MODEL

    elif provider == "gemini":
        if agent_name == "skill_gap" and settings.GEMINI_MODEL_SKILL_GAP:
            return settings.GEMINI_MODEL_SKILL_GAP
        if agent_name == "cv_optimizer" and settings.GEMINI_MODEL_CV_OPTIMIZER:
            return settings.GEMINI_MODEL_CV_OPTIMIZER
        return settings.GEMINI_MODEL

    elif provider == "openai":
        if agent_name == "skill_gap" and settings.OPENAI_MODEL_SKILL_GAP:
            return settings.OPENAI_MODEL_SKILL_GAP
        if agent_name == "cv_optimizer" and settings.OPENAI_MODEL_CV_OPTIMIZER:
            return settings.OPENAI_MODEL_CV_OPTIMIZER
        return settings.OPENAI_MODEL
    
    return ""

def _create_provider_llm(
    provider: str,
    agent_name: Optional[str] = None,
    temperature: Optional[float] = None,
    bound_kwargs: Optional[dict] = None,
    structured_output=None,
):
    """Crée une instance LLM pour un provider donné."""
    temp = temperature if temperature is not None else settings.LLM_TEMPERATURE
    model = _resolve_model(provider, agent_name)

    try:
        if provider == "groq":
            if not settings.GROQ_API_KEY: return None
            from langchain_groq import ChatGroq
            llm = ChatGroq(
                model=model,
                api_key=settings.GROQ_API_KEY,
                temperature=temp,
                max_tokens=settings.LLM_MAX_TOKENS,
            )

        elif provider == "gemini":
            api_key = settings.effective_gemini_api_key
            if not api_key: return None
            from langchain_google_genai import ChatGoogleGenerativeAI
            llm = ChatGoogleGenerativeAI(
                model=model,
                google_api_key=api_key,
                temperature=temp,
            )

        elif provider == "openai":
            if not settings.OPENAI_API_KEY: return None
            from langchain_openai import ChatOpenAI
            llm = ChatOpenAI(
                model=model,
                api_key=settings.OPENAI_API_KEY,
                temperature=temp,
            )
        else:
            return None

        # Post-processing (structured output, binding)
        if structured_output:
            try:
                llm = llm.with_structured_output(structured_output)
            except Exception:
                logger.warning(f"{provider} ne supporte pas structured_output")
                return None

        if bound_kwargs:
            llm = llm.bind(**bound_kwargs)

        return llm

    except Exception as e:
        logger.warning(f"Echec init {provider}: {e}")
        return None

class _LLMProvider(Runnable):
    """Wrapper Runnable avec fallback automatique multi-provider."""

    def __init__(
        self,
        agent_name: Optional[str] = None,
        temperature: Optional[float] = None,
        bound_kwargs: Optional[dict] = None,
        structured_output=None
    ):
        super().__init__()
        self._agent_name = agent_name
        self._temperature = temperature
        self._bound_kwargs = bound_kwargs or {}
        self._structured_output = structured_output

    def bind(self, **kwargs):
        return _LLMProvider(
            agent_name=self._agent_name,
            temperature=self._temperature,
            bound_kwargs={**self._bound_kwargs, **kwargs},
            structured_output=self._structured_output
        )

    def with_structured_output(self, schema, **kwargs):
        return _LLMProvider(
            agent_name=self._agent_name,
            temperature=self._temperature,
            bound_kwargs=self._bound_kwargs,
            structured_output=schema
        )

    def _get_providers(self) -> List[str]:
        """Détermine la liste des providers à essayer pour cet agent."""
        if self._agent_name == "skill_gap" and settings.LLM_PRIORITY_SKILL_GAP:
            return [p.strip() for p in settings.LLM_PRIORITY_SKILL_GAP.split(",") if p.strip()]
        if self._agent_name == "cv_optimizer" and settings.LLM_PRIORITY_CV_OPTIMIZER:
            return [p.strip() for p in settings.LLM_PRIORITY_CV_OPTIMIZER.split(",") if p.strip()]
        
        return [p.strip() for p in settings.LLM_PROVIDER_PRIORITY.split(",") if p.strip()]

    def invoke(self, input, config=None, **kwargs):
        last_error = None
        for provider in self._get_providers():
            llm = _create_provider_llm(
                provider, self._agent_name, self._temperature, 
                self._bound_kwargs, self._structured_output
            )
            if not llm: continue
            try:
                logger.info(f"[LLM] {provider} -> {self._agent_name or 'default'}")
                return llm.invoke(input, config=config, **kwargs)
            except Exception as e:
                logger.warning(f"{provider} failed: {e}")
                last_error = e
        raise last_error or RuntimeError("Aucun provider LLM disponible")

    async def ainvoke(self, input, config=None, **kwargs):
        last_error = None
        for provider in self._get_providers():
            llm = _create_provider_llm(
                provider, self._agent_name, self._temperature, 
                self._bound_kwargs, self._structured_output
            )
            if not llm: continue
            try:
                logger.info(f"[LLM] {provider} -> {self._agent_name or 'default'}")
                return await llm.ainvoke(input, config=config, **kwargs)
            except Exception as e:
                logger.warning(f"{provider} failed: {e}")
                last_error = e
        raise last_error or RuntimeError("Aucun provider LLM disponible")

def get_llm(temperature: Optional[float] = None, agent_name: Optional[str] = None):
    """Factory: retourne un wrapper avec fallback multi-provider."""
    return _LLMProvider(agent_name=agent_name, temperature=temperature)

def get_llm_precise():
    """Modèle précis (Groq 70b) avec température 0."""
    from langchain_groq import ChatGroq
    return ChatGroq(
        model=settings.GROQ_MODEL_PRECISE,
        api_key=settings.GROQ_API_KEY,
        temperature=0.0,
        max_tokens=settings.LLM_MAX_TOKENS,
    )
