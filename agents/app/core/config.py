# ============================================================
# app/core/config.py — Configuration centralisée (LLM + DB + URLs)
# ============================================================
import os
from functools import lru_cache
from pathlib import Path
from pydantic_settings import BaseSettings
import dotenv

dotenv.load_dotenv()

def find_env_file() -> str:
    """Recherche dynamiquement le fichier .env dans les dossiers parents."""
    current = Path(__file__).resolve()
    # On remonte jusqu'à 5 niveaux de dossiers pour trouver le .env
    for _ in range(6):
        env_path = current / ".env"
        if env_path.exists():
            return str(env_path)
        # On vérifie aussi dans agents/.env si on est au niveau racine
        agents_env = current / "agents" / ".env"
        if agents_env.exists():
            return str(agents_env)
        current = current.parent
    return "../.env"  # Fallback par défaut


class Settings(BaseSettings):
    # ─── Base de données ───
    DATABASE_URL: str = os.getenv("DATABASE_URL", "postgresql+asyncpg://admin:admin@localhost:5433/nextstep_db")
    database_url: str = os.getenv("DATABASE_URL", "postgresql+asyncpg://admin:admin@localhost:5433/nextstep_db")

    # ─── LLM Provider ───
    LLM_PROVIDER: str = "groq"          # "groq" | "openai"
    GROQ_API_KEY: str = os.getenv("GROQ_API_KEY", "")
    groq_api_key: str = os.getenv("GROQ_API_KEY", "")
    GROQ_MODEL: str = "llama-3.1-8b-instant"
    groq_model: str = "llama-3.3-70b-versatile"
    OPENAI_API_KEY: str = ""
    OPENAI_MODEL: str = "gpt-4o-mini"

    # ─── Search API ───
    TAVILY_API_KEY: str = os.getenv("TAVILY_API_KEY", "")
    tavily_api_key: str = os.getenv("TAVILY_API_KEY", "")

    # ─── Backend .NET ───
    DOTNET_BACKEND_URL: str = "http://localhost:5000"
    backend_url: str = "http://localhost:5000"

    # ─── LLM settings ───
    LLM_TEMPERATURE: float = 0.1
    LLM_MAX_TOKENS: int = 4096
    
    # ─── JWT ───
    jwt_secret: str = os.getenv("Keycloak__ClientSecret", "secret-keycloak-local")
    jwt_algorithm: str = os.getenv("JWT_ALGORITHM", "HS256")

    class Config:
        env_file = find_env_file()
        env_file_encoding = "utf-8"
        extra = "ignore"


@lru_cache()
def get_settings() -> Settings:
    return Settings()


settings = get_settings()


def get_llm(temperature: float | None = None):
    """
    Factory — retourne le LLM configuré (Groq ou OpenAI).
    Utilisé par tous les agents pour instancier leur modèle.
    """
    temp = temperature if temperature is not None else settings.LLM_TEMPERATURE

    if settings.LLM_PROVIDER == "groq":
        from langchain_groq import ChatGroq
        return ChatGroq(
            model=settings.GROQ_MODEL,
            api_key=settings.GROQ_API_KEY,
            temperature=temp,
        )
    else:
        from langchain_openai import ChatOpenAI
        return ChatOpenAI(
            model=settings.OPENAI_MODEL,
            api_key=settings.OPENAI_API_KEY,
            temperature=temp,
        )

def get_llm_precise():
    """Température basse pour extraction structurée."""
    from langchain_groq import ChatGroq
    return ChatGroq(
        model=settings.groq_model,
        api_key=settings.groq_api_key,
        temperature=0.1,
    )

