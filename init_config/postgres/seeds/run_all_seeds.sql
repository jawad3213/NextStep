-- ============================================================
-- Runs every demo seed in order, in a single transaction, for one
-- Keycloak account (selected by email).
--
-- Prerequisites:
--   1. Start the stack once (backend + agents create/repair the schema).
--   2. Register in Keycloak and log in to NextStep once, so your user exists.
--
-- Usage (from the repository root):
--   psql -U <user> -d nextstep_db -v user_email=you@example.com -f init_config/postgres/seeds/run_all_seeds.sql
-- Docker (\ir includes don't work over stdin, so pipe the numbered files):
--   cat init_config/postgres/seeds/0*.sql | docker compose -f docker-compose.yml -f docker-compose.dev.yml \
--     exec -T db sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 -1 -v user_email=you@example.com'
--
-- Safe to run repeatedly: every script is idempotent.
-- ============================================================

\set ON_ERROR_STOP on

BEGIN;
\ir 00_select_user.sql
\ir 01_keywords.sql
\ir 02_demo_user_profile.sql
\ir 03_offers_and_candidatures.sql
\ir 04_coaching_history.sql
COMMIT;
