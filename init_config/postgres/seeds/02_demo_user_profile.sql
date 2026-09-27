-- ============================================================
-- 02 — Demo profile (skills, experiences, education) for the seed user
-- The user is a real Keycloak account selected by 00_select_user.sql;
-- the backend creates its utilisateur row on first login.
-- Idempotent: fixed IDs + ON CONFLICT DO NOTHING; never overwrites edits.
-- ============================================================

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
WHERE lower(u.email) = lower(current_setting('nextstep.seed_email'))
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
WHERE lower(u.email) = lower(current_setting('nextstep.seed_email'))
ON CONFLICT DO NOTHING;

-- Education
INSERT INTO public.formation (id_formation, id_utilisateur, etablissement, diplome, specialisation, annee, annee_fin, ville)
SELECT '5eed0002-0000-4000-8000-000000000201'::uuid, u.id_utilisateur,
       'Université Claude Bernard Lyon 1', 'Master', 'Génie Logiciel', 2017, 2019, 'Lyon'
FROM public.utilisateur u
WHERE lower(u.email) = lower(current_setting('nextstep.seed_email'))
ON CONFLICT DO NOTHING;
