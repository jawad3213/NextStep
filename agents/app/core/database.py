# ============================================================
# app/core/database.py — SQLAlchemy async + pgvector
# ============================================================
from sqlalchemy.ext.asyncio import AsyncSession, create_async_engine, async_sessionmaker
from sqlalchemy.orm import DeclarativeBase
from pgvector.sqlalchemy import Vector  # noqa: F401 — enregistre le type Vector
from app.core.config import settings

engine = create_async_engine(
    settings.DATABASE_URL,
    echo=False,
    pool_pre_ping=True,
    pool_size=5,
    max_overflow=10,
)

AsyncSessionFactory = async_sessionmaker(
    engine,
    class_=AsyncSession,
    expire_on_commit=False,
)


class Base(DeclarativeBase):
    """Base commune pour tous les modèles SQLAlchemy."""
    pass


async def get_db() -> AsyncSession:  # type: ignore[override]
    """FastAPI Dependency — session DB async."""
    async with AsyncSessionFactory() as session:
        yield session
