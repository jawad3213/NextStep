-- ============================================================
-- Runs every demo seed in order, in a single transaction.
--
-- Prerequisite: start the backend (and the agents service) once so
-- their startup code creates/repairs the schema.
--
-- Usage (from the repository root):
--   psql -U postgres -d nextstep_db -f config/postgres/seeds/run_all_seeds.sql
-- Docker (\ir includes don't work over stdin, so pipe the numbered files):
--   cat config/postgres/seeds/0*.sql | docker compose exec -T db psql -U postgres -d nextstep_db -v ON_ERROR_STOP=1 -1
--
-- Safe to run repeatedly: every script is idempotent.
-- ============================================================

\set ON_ERROR_STOP on

BEGIN;
\ir 01_keywords.sql
\ir 02_demo_user_profile.sql
\ir 03_offers_and_candidatures.sql
\ir 04_coaching_history.sql
COMMIT;
