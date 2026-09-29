import logging

from sqlalchemy import text

from app.core.database import AGENTS_SCHEMA, engine

logger = logging.getLogger(__name__)


_AGENT_TABLES = ("offre_analysee", "intel_entreprise", "resultat_matching")

# One-time upgrade of databases created before the modular split: the agents' tables
# lived in "public" with foreign keys to backend tables. They move to the "agents"
# schema and reference offers/users by id only (no cross-module foreign keys).
_LEGACY_UPGRADE_STATEMENTS = [
    f"""
    DO $$
    BEGIN
        IF to_regclass('public.{table}') IS NOT NULL AND to_regclass('{AGENTS_SCHEMA}.{table}') IS NULL THEN
            ALTER TABLE public.{table} SET SCHEMA {AGENTS_SCHEMA};
        END IF;
    END $$
    """
    for table in _AGENT_TABLES
] + [
    f"""
    DO $$
    DECLARE r record;
    BEGIN
        FOR r IN
            SELECT c.conname, t.relname AS table_name
            FROM pg_constraint c
            JOIN pg_class t      ON t.oid = c.conrelid
            JOIN pg_namespace n  ON n.oid = t.relnamespace
            JOIN pg_class rt     ON rt.oid = c.confrelid
            JOIN pg_namespace rn ON rn.oid = rt.relnamespace
            WHERE c.contype = 'f' AND n.nspname = '{AGENTS_SCHEMA}' AND rn.nspname <> '{AGENTS_SCHEMA}'
        LOOP
            EXECUTE format('ALTER TABLE {AGENTS_SCHEMA}.%I DROP CONSTRAINT %I', r.table_name, r.conname);
        END LOOP;
    END $$
    """
]

_SCHEMA_STATEMENTS = [
    "CREATE EXTENSION IF NOT EXISTS pgcrypto",
    f"CREATE SCHEMA IF NOT EXISTS {AGENTS_SCHEMA}",
    *_LEGACY_UPGRADE_STATEMENTS,
    """
    CREATE TABLE IF NOT EXISTS agents.offre_analysee (
        id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
        id_offre UUID NULL,
        titre_poste VARCHAR(200),
        entreprise VARCHAR(150),
        competences_requises JSONB,
        competences_souhaitees JSONB,
        keywords_ats JSONB,
        stack_technique JSONB,
        annees_experience INTEGER,
        type_contrat VARCHAR(50),
        localisation VARCHAR(150),
        texte_brut TEXT,
        date_analyse TIMESTAMP DEFAULT CURRENT_TIMESTAMP
    )
    """,
    """
    CREATE TABLE IF NOT EXISTS agents.intel_entreprise (
        id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
        nom_entreprise VARCHAR(200) NOT NULL,
        id_offre UUID NULL,
        note_glassdoor FLOAT,
        score_culture FLOAT,
        salaire_min INTEGER,
        salaire_max INTEGER,
        devise_salaire VARCHAR(10) DEFAULT 'MAD',
        resume_entreprise TEXT,
        actualites JSONB,
        difficulte_entretien VARCHAR(20) DEFAULT 'medium',
        questions_connues JSONB,
        date_collecte TIMESTAMP DEFAULT CURRENT_TIMESTAMP
    )
    """,
    """
    CREATE TABLE IF NOT EXISTS agents.resultat_matching (
        id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
        id_offre UUID NULL,
        id_utilisateur UUID NULL,
        score_global INTEGER,
        competences_manquantes JSONB,
        points_forts JSONB,
        ecart_experience INTEGER DEFAULT 0,
        date_matching TIMESTAMP DEFAULT CURRENT_TIMESTAMP
    )
    """,
    "ALTER TABLE IF EXISTS agents.offre_analysee ADD COLUMN IF NOT EXISTS competences_souhaitees JSONB",
    "ALTER TABLE IF EXISTS agents.offre_analysee ADD COLUMN IF NOT EXISTS keywords_ats JSONB",
    "ALTER TABLE IF EXISTS agents.offre_analysee ADD COLUMN IF NOT EXISTS stack_technique JSONB",
    "ALTER TABLE IF EXISTS agents.offre_analysee ADD COLUMN IF NOT EXISTS annees_experience INTEGER",
    "ALTER TABLE IF EXISTS agents.offre_analysee ADD COLUMN IF NOT EXISTS type_contrat VARCHAR(50)",
    "ALTER TABLE IF EXISTS agents.offre_analysee ADD COLUMN IF NOT EXISTS localisation VARCHAR(150)",
    "ALTER TABLE IF EXISTS agents.offre_analysee ADD COLUMN IF NOT EXISTS texte_brut TEXT",
    "ALTER TABLE IF EXISTS agents.offre_analysee ADD COLUMN IF NOT EXISTS date_analyse TIMESTAMP DEFAULT CURRENT_TIMESTAMP",
    "ALTER TABLE IF EXISTS agents.intel_entreprise ADD COLUMN IF NOT EXISTS id_offre UUID",
    "ALTER TABLE IF EXISTS agents.intel_entreprise ADD COLUMN IF NOT EXISTS note_glassdoor FLOAT",
    "ALTER TABLE IF EXISTS agents.intel_entreprise ADD COLUMN IF NOT EXISTS score_culture FLOAT",
    "ALTER TABLE IF EXISTS agents.intel_entreprise ADD COLUMN IF NOT EXISTS salaire_min INTEGER",
    "ALTER TABLE IF EXISTS agents.intel_entreprise ADD COLUMN IF NOT EXISTS salaire_max INTEGER",
    "ALTER TABLE IF EXISTS agents.intel_entreprise ADD COLUMN IF NOT EXISTS devise_salaire VARCHAR(10) DEFAULT 'MAD'",
    "ALTER TABLE IF EXISTS agents.intel_entreprise ADD COLUMN IF NOT EXISTS resume_entreprise TEXT",
    "ALTER TABLE IF EXISTS agents.intel_entreprise ADD COLUMN IF NOT EXISTS actualites JSONB",
    "ALTER TABLE IF EXISTS agents.intel_entreprise ADD COLUMN IF NOT EXISTS difficulte_entretien VARCHAR(20) DEFAULT 'medium'",
    "ALTER TABLE IF EXISTS agents.intel_entreprise ADD COLUMN IF NOT EXISTS questions_connues JSONB",
    "ALTER TABLE IF EXISTS agents.intel_entreprise ADD COLUMN IF NOT EXISTS date_collecte TIMESTAMP DEFAULT CURRENT_TIMESTAMP",
    "ALTER TABLE IF EXISTS agents.intel_entreprise ADD COLUMN IF NOT EXISTS rapport_complet JSONB",
    "ALTER TABLE IF EXISTS agents.resultat_matching ADD COLUMN IF NOT EXISTS id_offre UUID",
    "ALTER TABLE IF EXISTS agents.resultat_matching ADD COLUMN IF NOT EXISTS id_utilisateur UUID",
    "ALTER TABLE IF EXISTS agents.resultat_matching ADD COLUMN IF NOT EXISTS score_global INTEGER",
    "ALTER TABLE IF EXISTS agents.resultat_matching ADD COLUMN IF NOT EXISTS competences_manquantes JSONB",
    "ALTER TABLE IF EXISTS agents.resultat_matching ADD COLUMN IF NOT EXISTS points_forts JSONB",
    "ALTER TABLE IF EXISTS agents.resultat_matching ADD COLUMN IF NOT EXISTS ecart_experience INTEGER DEFAULT 0",
    "ALTER TABLE IF EXISTS agents.resultat_matching ADD COLUMN IF NOT EXISTS date_matching TIMESTAMP DEFAULT CURRENT_TIMESTAMP",
    "CREATE INDEX IF NOT EXISTS idx_offre_analysee_id_offre ON agents.offre_analysee(id_offre)",
    "CREATE INDEX IF NOT EXISTS idx_intel_entreprise_nom ON agents.intel_entreprise(nom_entreprise)",
    "CREATE INDEX IF NOT EXISTS idx_intel_entreprise_offre ON agents.intel_entreprise(id_offre)",
    "CREATE INDEX IF NOT EXISTS idx_matching_offre_user ON agents.resultat_matching(id_offre, id_utilisateur)",
]


async def ensure_agent_runtime_schema() -> None:
    logger.info("Ensuring agent persistence schema...")
    async with engine.begin() as conn:
        for statement in _SCHEMA_STATEMENTS:
            await conn.execute(text(statement))
    logger.info("Agent persistence schema is ready.")
