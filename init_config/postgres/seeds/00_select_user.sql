-- ============================================================
-- 00 — Select the Keycloak account that receives the demo data
-- Pass its email:  psql ... -v user_email=you@example.com
-- The account must exist: register in Keycloak and log in to NextStep
-- once (the backend creates the utilisateur row on first login).
-- ============================================================

\if :{?user_email}
\else
  \echo 'Missing variable. Usage: psql ... -v user_email=you@example.com -f run_all_seeds.sql'
  \quit
\endif

SELECT set_config('nextstep.seed_email', :'user_email', false);

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM profile.utilisateur u
        WHERE lower(u.email) = lower(current_setting('nextstep.seed_email'))
    ) THEN
        RAISE EXCEPTION 'No NextStep user with email "%". Register in Keycloak and log in once, then re-run.',
            current_setting('nextstep.seed_email');
    END IF;
END $$;
