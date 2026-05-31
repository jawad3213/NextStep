"""
MODÈLES SQLALCHEMY — Mappés aux tables PostgreSQL existantes.

Tables utilisées par ce module :
  session_coaching      → notre table principale
  question_entrainement → questions + corrections
  chat_message          → historique de tous les chats

On lit aussi (sans écrire) :
  offre_analysee        → output Agent 2
  intel_entreprise      → output Agent 3
  resultat_matching     → output Agent 4
"""

import uuid
from datetime import datetime
from sqlalchemy import Column, String, Integer, Boolean, DateTime, Text, Float, ForeignKey
from sqlalchemy.dialects.postgresql import UUID, JSONB
from sqlalchemy.orm import relationship
from app.core.database import Base


# ═══════════════════════════════════════════════════════════════
# NOS TABLES (lecture + écriture)
# ═══════════════════════════════════════════════════════════════

class SessionCoaching(Base):
    __tablename__ = "session_coaching"

    id_session = Column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)

    # Liens
    id_candidature = Column(UUID(as_uuid=True), nullable=True)   # NULL en Arena
    id_utilisateur = Column(UUID(as_uuid=True), nullable=True)

    # Config
    mode               = Column(String(10),  nullable=False, default="offer")
    language           = Column(String(10),  nullable=False, default="en")
    duration_minutes   = Column(Integer,     nullable=False, default=20)
    status             = Column(String(20),  nullable=False, default="pending")

    # Arena seulement
    domain       = Column(String(100), nullable=True)
    level        = Column(String(20),  nullable=True)
    focus_areas  = Column(JSONB,       nullable=True)

    # Résultats
    score_entretien = Column(Integer, nullable=True)
    feedback_json   = Column(JSONB,   nullable=True)

    # Timestamps
    date_session = Column(DateTime, default=datetime.utcnow)
    completed_at = Column(DateTime, nullable=True)

    # Relations
    questions    = relationship("QuestionEntrainement", back_populates="session", cascade="all, delete-orphan")


class QuestionEntrainement(Base):
    __tablename__ = "question_entrainement"

    id_question = Column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    id_session  = Column(UUID(as_uuid=True), ForeignKey("session_coaching.id_session", ondelete="CASCADE"), nullable=False)

    texte_question      = Column(Text,       nullable=False)
    type_question       = Column(String(20), default="behavioral")
    source              = Column(String(20), default="generated")
    company_specific    = Column(Boolean,    default=False)
    conseil_reponse     = Column(Text,       nullable=True)
    reponse_utilisateur = Column(Text,       nullable=True)
    correction_ia       = Column(Text,       nullable=True)
    score_reponse       = Column(Integer,    nullable=True)
    ordre               = Column(Integer,    default=0)

    # Relation
    session = relationship("SessionCoaching", back_populates="questions")



