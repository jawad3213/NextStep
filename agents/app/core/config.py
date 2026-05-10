# ============================================================
# app/core/config.py — Configuration centralisée (LLM + DB + URLs)
#
# Stratégie Anti-Rate-Limiting :
#   Chaque agent utilise un modèle Groq DIFFÉRENT.
#   Groq applique ses limites PAR modèle, donc distribuer
#   les appels sur 3-4 modèles multiplie le quota effectif.
# ============================================================
import os
import logging
from functools import lru_cache
from pathlib import Path
from typing import Optional
from pydantic_settings import BaseSettings

logger = logging.getLogger(__name__)


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


# ─── Mapping Agent → Modèle Groq ─────────────────────────────
# Chaque agent tape un modèle différent = quotas séparés
# Modifie ces valeurs via .env pour personnaliser
AGENT_MODEL_DEFAULTS = {
    "offer_analyzer":  "llama-3.1-8b-instant",           # Rapide, structured output
    "skill_gap":       "qwen/qwen3-32b",                 # Puissant, bon pour l'analyse
    "cv_optimizer":    "llama-3.3-70b-versatile",         # Gros modèle, meilleur pour réécriture
    "company":         "meta-llama/llama-4-scout-17b-16e-instruct",  # Bon compromis
    "default":         "llama-3.1-8b-instant",            # Fallback
}


class Settings(BaseSettings):
    # ─── Base de données ───
    DATABASE_URL: str = "postgresql+asyncpg://nextstep:nextstep@localhost:5432/nextstep_db"

    # ─── LLM Provider ───
    LLM_PROVIDER: str = "groq"          # "groq" | "openai" | "gemini"
    GROQ_API_KEY: str = ""
    GROQ_MODEL: str = "llama-3.1-8b-instant"  # Modèle par défaut (fallback)
    OPENAI_API_KEY: str = ""
    OPENAI_MODEL: str = "gpt-4o-mini"

    # ─── Per-Agent Model Override (via .env) ───
    GROQ_MODEL_OFFER_ANALYZER: str = ""
    GROQ_MODEL_SKILL_GAP: str = ""
    GROQ_MODEL_CV_OPTIMIZER: str = ""
    GROQ_MODEL_COMPANY: str = ""

    # ─── Google Gemini (Free alternative) ───
    GEMINI_API_KEY: str = ""

    # ─── Search API ───
    TAVILY_API_KEY: str = ""

    # ─── Backend .NET ───
    DOTNET_BACKEND_URL: str = "http://localhost:5000"

    # ─── LLM settings ───
    LLM_TEMPERATURE: float = 0.1
    LLM_MAX_TOKENS: int = 4096

    class Config:
        env_file = find_env_file()
        env_file_encoding = "utf-8"
        extra = "ignore"


@lru_cache()
def get_settings() -> Settings:
    return Settings()


settings = get_settings()


def _resolve_groq_model(agent_name: Optional[str] = None) -> str:
    """
    Résout le modèle Groq à utiliser pour un agent donné.
    Priorité : .env override > AGENT_MODEL_DEFAULTS > settings.GROQ_MODEL
    """
    if agent_name:
        # 1. Vérifier les overrides .env
        env_overrides = {
            "offer_analyzer": settings.GROQ_MODEL_OFFER_ANALYZER,
            "skill_gap":      settings.GROQ_MODEL_SKILL_GAP,
            "cv_optimizer":   settings.GROQ_MODEL_CV_OPTIMIZER,
            "company":        settings.GROQ_MODEL_COMPANY,
        }
        env_val = env_overrides.get(agent_name, "")
        if env_val:
            return env_val

        # 2. Defaults intégrés
        if agent_name in AGENT_MODEL_DEFAULTS:
            return AGENT_MODEL_DEFAULTS[agent_name]

    # 3. Fallback global
    return settings.GROQ_MODEL


def get_llm(temperature: float | None = None, agent_name: str | None = None):
    """
    Factory — retourne le LLM configuré pour un agent spécifique.

    Args:
        temperature: Température du LLM (override la config globale).
        agent_name:  Nom de l'agent appelant. Si fourni, un modèle dédié
                     est attribué pour distribuer les appels et éviter le
                     rate limiting. Valeurs : "offer_analyzer", "skill_gap",
                     "cv_optimizer", "company", ou None (défaut).

    Exemples:
        get_llm()                                    # Modèle par défaut
        get_llm(agent_name="offer_analyzer")         # llama-3.1-8b-instant
        get_llm(agent_name="cv_optimizer", temperature=0.0)  # llama-3.3-70b
    """
    temp = temperature if temperature is not None else settings.LLM_TEMPERATURE

    if settings.LLM_PROVIDER == "groq":
        from langchain_groq import ChatGroq
        model = _resolve_groq_model(agent_name)
        logger.info(f"[LLM Factory] agent={agent_name or 'default'} → model={model}")
        return ChatGroq(
            model=model,
            api_key=settings.GROQ_API_KEY,
            temperature=temp,
        )
    elif settings.LLM_PROVIDER == "gemini" and settings.GEMINI_API_KEY:
        from langchain_google_genai import ChatGoogleGenerativeAI
        return ChatGoogleGenerativeAI(
            model="gemini-2.0-flash",
            google_api_key=settings.GEMINI_API_KEY,
            temperature=temp,
        )
    else:
        from langchain_openai import ChatOpenAI
        return ChatOpenAI(
            model=settings.OPENAI_MODEL,
            api_key=settings.OPENAI_API_KEY,
            temperature=temp,
        )

