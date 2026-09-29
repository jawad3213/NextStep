-- ============================================================
-- 04 — Demo interview coaching history (Arena + Offer modes)
-- Requires 00 (seed user) and 03 (candidature for the offer-mode session).
-- Idempotent: fixed IDs + ON CONFLICT DO NOTHING.
-- ============================================================

INSERT INTO coaching.session_coaching
    (id_session, id_utilisateur, id_candidature, mode, language, duration_minutes, status, domain, level, score_entretien, date_session, completed_at)
SELECT v.id::uuid, u.id_utilisateur, v.candidature::uuid, v.mode, v.lang, v.duree, 'completed',
       v.domain, v.level, v.score, NOW() - v.started::interval, NOW() - v.ended::interval
FROM profile.utilisateur u
CROSS JOIN (VALUES
    ('5eed0004-0000-4000-8000-000000000001', NULL,                                   'arena', 'fr', 20, 'Data Science',    'senior', 85, '1 day',   '23 hours'),
    ('5eed0004-0000-4000-8000-000000000002', '5eed0003-0000-4000-8000-000000000002', 'offer', 'en', 30, NULL,              NULL,     72, '2 days',  '47 hours'),
    ('5eed0004-0000-4000-8000-000000000003', NULL,                                   'arena', 'fr', 15, 'Product Manager', 'junior', 91, '3 hours', '2 hours')
) AS v(id, candidature, mode, lang, duree, domain, level, score, started, ended)
WHERE lower(u.email) = lower(current_setting('nextstep.seed_email'))
ON CONFLICT DO NOTHING;
