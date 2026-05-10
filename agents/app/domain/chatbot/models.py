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
    chat_messages = relationship("ChatMessage", back_populates="session")


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


class ChatMessage(Base):
    __tablename__ = "chat_message"

    id             = Column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    thread_id      = Column(UUID(as_uuid=True), nullable=False)
    id_utilisateur = Column(UUID(as_uuid=True), nullable=True)
    id_session     = Column(UUID(as_uuid=True), ForeignKey("session_coaching.id_session", ondelete="CASCADE"), nullable=True)
    id_candidature = Column(UUID(as_uuid=True), nullable=True)

    chat_type  = Column(String(20), nullable=False)  # questions | salary | interview
    sender     = Column(String(10), nullable=False)  # user | ai
    content    = Column(Text,       nullable=False)
    created_at = Column(DateTime,   default=datetime.utcnow)

    # Relation
    session = relationship("SessionCoaching", back_populates="chat_messages")


# ═══════════════════════════════════════════════════════════════
# TABLES DES AGENTS COLLÈGUES (lecture seulement)
# ═══════════════════════════════════════════════════════════════

class OffreAnalysee(Base):
    """Output Agent 2 — on lit seulement."""
    __tablename__ = "offre_analysee"

    id                   = Column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    id_offre             = Column(UUID(as_uuid=True), nullable=False)
    titre_poste          = Column(String(200), nullable=True)
    entreprise           = Column(String(150), nullable=True)
    competences_requises = Column(JSONB, nullable=True)
    competences_souhaitees = Column(JSONB, nullable=True)
    keywords_ats         = Column(JSONB, nullable=True)
    stack_technique      = Column(JSONB, nullable=True)
    annees_experience    = Column(Integer, nullable=True)
    type_contrat         = Column(String(50), nullable=True)
    localisation         = Column(String(150), nullable=True)
    texte_brut           = Column(Text, nullable=True)
    date_analyse         = Column(DateTime, default=datetime.utcnow)


class IntelEntreprise(Base):
    """Output Agent 3 — on lit seulement."""
    __tablename__ = "intel_entreprise"

    id                    = Column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    nom_entreprise        = Column(String(200), nullable=False)
    id_offre              = Column(UUID(as_uuid=True), nullable=True)
    note_glassdoor        = Column(Float,   nullable=True)
    salaire_min           = Column(Integer, nullable=True)
    salaire_max           = Column(Integer, nullable=True)
    devise_salaire        = Column(String(10), default="MAD")
    resume_entreprise     = Column(Text,    nullable=True)
    difficulte_entretien  = Column(String(20), default="medium")
    questions_connues     = Column(JSONB,   nullable=True)
    date_collecte         = Column(DateTime, default=datetime.utcnow)


class ResultatMatching(Base):
    """Output Agent 4 — on lit seulement."""
    __tablename__ = "resultat_matching"

    id                       = Column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    id_offre                 = Column(UUID(as_uuid=True), nullable=False)
    id_utilisateur           = Column(UUID(as_uuid=True), nullable=False)
    score_global             = Column(Integer, nullable=True)
    competences_manquantes   = Column(JSONB,   nullable=True)
    points_forts             = Column(JSONB,   nullable=True)
    date_matching            = Column(DateTime, default=datetime.utcnow)
