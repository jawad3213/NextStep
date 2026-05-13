import uuid
from datetime import datetime
from sqlalchemy import Column, String, Integer, Float, DateTime, Text
from sqlalchemy.dialects.postgresql import UUID, JSONB
from app.core.database import Base

class OffreAnalysee(Base):
    """Output Agent 2."""
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
    """Output Agent 3."""
    __tablename__ = "intel_entreprise"

    id                    = Column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    nom_entreprise        = Column(String(200), nullable=False)
    id_offre              = Column(UUID(as_uuid=True), nullable=True)
    note_glassdoor        = Column(Float,   nullable=True)
    salaire_min           = Column(Integer, nullable=True)
    salaire_max           = Column(Integer, nullable=True)
    devise_salaire        = Column(String(10), default="MAD")
    resume_entreprise     = Column(Text,    nullable=True)
    actualites            = Column(JSONB,   nullable=True)
    difficulte_entretien  = Column(String(20), default="medium")
    questions_connues     = Column(JSONB,   nullable=True)
    date_collecte         = Column(DateTime, default=datetime.utcnow)


class ResultatMatching(Base):
    """Output Agent 4."""
    __tablename__ = "resultat_matching"

    id                       = Column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    id_offre                 = Column(UUID(as_uuid=True), nullable=False)
    id_utilisateur           = Column(UUID(as_uuid=True), nullable=False)
    score_global             = Column(Integer, nullable=True)
    competences_manquantes   = Column(JSONB,   nullable=True)
    points_forts             = Column(JSONB,   nullable=True)
    date_matching            = Column(DateTime, default=datetime.utcnow)
