-- ============================================================
-- 03 — Demo job offer, candidature and AI agent outputs
-- Requires 02 (demo user). Offers live in offres_emploi (the legacy
-- "offre" table is migrated away by the backend at startup).
-- The agent output tables are created by the agents service; those
-- inserts are skipped if the agents have never been started.
-- Idempotent: fixed IDs + ON CONFLICT DO NOTHING.
-- ============================================================

-- Offer (title/company/etc. are read from analyse_json.analyzed_offer)
INSERT INTO public.offres_emploi (id, utilisateur_id, texte_brut, analyse_json, date_creation)
SELECT
    '5eed0003-0000-4000-8000-000000000001'::uuid,
    u.id_utilisateur,
    'Senior Full-Stack Developer at Google. We are looking for someone with 5+ years of experience in React, Node.js, and PostgreSQL. Experience with System Design and Distributed Systems is a plus.',
    '{
        "analyzed_offer": {
            "titre": "Senior Full-Stack Developer",
            "entreprise": "Google",
            "localisation": "Mountain View, CA (Remote)",
            "type_contrat": "CDI",
            "annees_experience": "5",
            "competences_requises": ["React", "Node.js", "PostgreSQL", "JavaScript", "TypeScript"],
            "competences_souhaitees": ["Docker", "Kubernetes", "Google Cloud"],
            "keywords_ats": ["Full-Stack", "Distributed Systems", "Scalability", "Microservices"]
        }
    }'::jsonb,
    NOW()
FROM public.utilisateur u
WHERE u.keycloak_id = 'dev-user'
ON CONFLICT DO NOTHING;

-- Candidature (statut must be a CandidatureStatut enum name)
INSERT INTO public.candidature (id_candidature, id_utilisateur, id_offre, statut, channel, date_creation)
SELECT '5eed0003-0000-4000-8000-000000000002'::uuid, u.id_utilisateur,
       '5eed0003-0000-4000-8000-000000000001'::uuid, 'EN_COURS_EXAMEN', 'EMAIL', NOW()
FROM public.utilisateur u
WHERE u.keycloak_id = 'dev-user'
ON CONFLICT DO NOTHING;

-- Agent outputs (tables owned by the agents service)
DO $$
DECLARE
    v_user  uuid := (SELECT id_utilisateur FROM public.utilisateur WHERE keycloak_id = 'dev-user');
    v_offer uuid := '5eed0003-0000-4000-8000-000000000001';
BEGIN
    IF to_regclass('public.offre_analysee') IS NULL THEN
        RAISE NOTICE 'Agent tables not found — start the agents service once, then re-run. Skipping agent outputs.';
        RETURN;
    END IF;

    -- Agent 2: offer analysis
    INSERT INTO public.offre_analysee (id, id_offre, titre_poste, entreprise, competences_requises, competences_souhaitees, keywords_ats, stack_technique, annees_experience, type_contrat, localisation, date_analyse)
    VALUES (
        '5eed0003-0000-4000-8000-000000000003', v_offer,
        'Senior Full-Stack Developer', 'Google',
        '["React", "Node.js", "PostgreSQL", "JavaScript", "TypeScript"]'::jsonb,
        '["Docker", "Kubernetes", "Google Cloud"]'::jsonb,
        '["Full-Stack", "Distributed Systems", "Scalability", "Microservices"]'::jsonb,
        '["React", "Node.js", "PostgreSQL", "Redis"]'::jsonb,
        5, 'CDI', 'Mountain View, CA (Remote)', NOW()
    ) ON CONFLICT DO NOTHING;

    -- Agent 3: company intel
    INSERT INTO public.intel_entreprise (id, nom_entreprise, id_offre, note_glassdoor, salaire_min, salaire_max, devise_salaire, resume_entreprise, difficulte_entretien, questions_connues, date_collecte)
    VALUES (
        '5eed0003-0000-4000-8000-000000000004', 'Google', v_offer,
        4.5, 120000, 185000, 'USD',
        'Google is a global leader in technology, focusing on search, advertising, cloud computing, and hardware. Known for its innovative culture and high bar for engineering talent.',
        'hard',
        '["Explain the difference between a process and a thread.", "How would you design a URL shortener like bit.ly?", "Describe a time you had to deal with a difficult technical trade-off.", "What happens when you type a URL into your browser?"]'::jsonb,
        NOW()
    ) ON CONFLICT DO NOTHING;

    -- Agent 4: profile matching
    INSERT INTO public.resultat_matching (id, id_offre, id_utilisateur, score_global, competences_manquantes, points_forts, date_matching)
    VALUES (
        '5eed0003-0000-4000-8000-000000000005', v_offer, v_user, 88,
        '["Google Cloud", "Kubernetes"]'::jsonb,
        '["Expert in React and Frontend optimization", "Solid Node.js backend experience", "Strong SQL knowledge", "Excellent problem-solving skills"]'::jsonb,
        NOW()
    ) ON CONFLICT DO NOTHING;
END $$;
