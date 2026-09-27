-- ============================================================
-- 02 — Demo user + profile (skills, experiences, education)
-- The demo user is the backend's Dev-mode user (keycloak_id = 'dev-user',
-- see DevAuthenticationHandler / DatabaseInitializer), so this data is
-- visible immediately when running with Auth:Mode = "Dev".
-- Idempotent: fixed IDs + ON CONFLICT DO NOTHING; never overwrites edits.
-- ============================================================

INSERT INTO public.utilisateur (id_utilisateur, keycloak_id, email, nom, prenom, date_inscription, onboarding_completed, profile_score)
VALUES ('00000000-0000-0000-0000-0000000000de', 'dev-user', 'dev@nextstep.local', 'User', 'Dev', NOW(), TRUE, 70)
ON CONFLICT DO NOTHING;

-- Skills
INSERT INTO public.competence (id_competence, id_utilisateur, nom, niveau, type_competence, is_valid)
SELECT v.id::uuid, u.id_utilisateur, v.nom, v.niveau, 'TECHNIQUE', TRUE
FROM public.utilisateur u
CROSS JOIN (VALUES
    ('5eed0002-0000-4000-8000-000000000001', 'Angular',    5),
    ('5eed0002-0000-4000-8000-000000000002', 'TypeScript', 4),
    ('5eed0002-0000-4000-8000-000000000003', 'C#',         3),
    ('5eed0002-0000-4000-8000-000000000004', 'HTML/CSS',   5),
    ('5eed0002-0000-4000-8000-000000000005', 'PostgreSQL', 3)
) AS v(id, nom, niveau)
WHERE u.keycloak_id = 'dev-user'
ON CONFLICT DO NOTHING;

-- Experiences
INSERT INTO public.experience (id_experience, id_utilisateur, entreprise, poste, date_debut, date_fin, missions, ville, type_contrat, is_valid)
SELECT v.id::uuid, u.id_utilisateur, v.entreprise, v.poste, v.debut::date, v.fin::date, v.missions, v.ville, 'CDI', TRUE
FROM public.utilisateur u
CROSS JOIN (VALUES
    ('5eed0002-0000-4000-8000-000000000101', 'TechCorp', 'Senior Frontend Developer', '2022-01-01', NULL,
     'Lead developer for modern enterprise SPAs. Architecting high performance applications using Angular and Tailwind CSS.', 'Paris'),
    ('5eed0002-0000-4000-8000-000000000102', 'InnovInc', 'Web Developer', '2019-05-01', '2021-12-31',
     'Fullstack developer creating backend web services with .NET Core and C#, and frontend reactive components.', 'Lyon')
) AS v(id, entreprise, poste, debut, fin, missions, ville)
WHERE u.keycloak_id = 'dev-user'
ON CONFLICT DO NOTHING;

-- Education
INSERT INTO public.formation (id_formation, id_utilisateur, etablissement, diplome, specialisation, annee, annee_fin, ville)
SELECT '5eed0002-0000-4000-8000-000000000201'::uuid, u.id_utilisateur,
       'Université Claude Bernard Lyon 1', 'Master', 'Génie Logiciel', 2017, 2019, 'Lyon'
FROM public.utilisateur u
WHERE u.keycloak_id = 'dev-user'
ON CONFLICT DO NOTHING;
