-- ============================================================
-- REVISED SEED SCRIPT FOR OFFER MODE TESTING
-- Targeted User: 0198223b-21de-47c5-8915-afe255f9512f
-- ============================================================

-- 0. Create a Dummy User if not exists
INSERT INTO public.utilisateur (id_utilisateur, keycloak_id, nom, prenom, email, date_inscription)
VALUES (
    '0198223b-21de-47c5-8915-afe255f9512f',
    'dummy-keycloak-id-for-testing',
    'Test',
    'User',
    'test.user@example.com',
    NOW()
) ON CONFLICT (id_utilisateur) DO NOTHING;

-- 1. Create a Job Offer
INSERT INTO public.offre (id_offre, titre_poste, entreprise, description_brute, localisation, date_scraping)
VALUES (
    'b1e1e1e1-e1e1-4e1e-b1e1-e1e1e1e1e1e1', 
    'Senior Full-Stack Developer',
    'Google',
    'Senior Full-Stack Developer at Google. We are looking for someone with 5+ years of experience in React, Node.js, and PostgreSQL. Experience with System Design and Distributed Systems is a plus.',
    'Mountain View, CA (Remote)',
    NOW()
) ON CONFLICT (id_offre) DO NOTHING;

-- 2. Create a Candidature (Link User to Offer)
INSERT INTO public.candidature (id_candidature, id_utilisateur, id_offre, statut, date_creation)
VALUES (
    'c1c1c1c1-c1c1-4c1c-b1c1-c1c1c1c1c1c1',
    '0198223b-21de-47c5-8915-afe255f9512f',
    'b1e1e1e1-e1e1-4e1e-b1e1-e1e1e1e1e1e1',
    'ANALYSEE',
    NOW()
) ON CONFLICT (id_candidature) DO NOTHING;

-- 3. Seed Analyzed Offer (Agent 2 Output)
INSERT INTO public.offre_analysee (id, id_offre, titre_poste, entreprise, competences_requises, competences_souhaitees, keywords_ats, stack_technique, annees_experience, type_contrat, localisation, date_analyse)
VALUES (
    gen_random_uuid(),
    'b1e1e1e1-e1e1-4e1e-b1e1-e1e1e1e1e1e1',
    'Senior Full-Stack Developer',
    'Google',
    '["React", "Node.js", "PostgreSQL", "JavaScript", "TypeScript"]'::jsonb,
    '["Docker", "Kubernetes", "Google Cloud Platform"]'::jsonb,
    '["Full-Stack", "Distributed Systems", "Scalability", "Microservices"]'::jsonb,
    '["MERN Stack", "AWS", "Redis"]'::jsonb,
    5,
    'CDI / Full-time',
    'Mountain View, CA (Remote)',
    NOW()
);

-- 4. Seed Company Intel (Agent 3 Output)
INSERT INTO public.intel_entreprise (id, nom_entreprise, id_offre, note_glassdoor, salaire_min, salaire_max, devise_salaire, resume_entreprise, difficulte_entretien, questions_connues, date_collecte)
VALUES (
    gen_random_uuid(),
    'Google',
    'b1e1e1e1-e1e1-4e1e-b1e1-e1e1e1e1e1e1',
    4.5,
    120000,
    185000,
    'USD',
    'Google is a global leader in technology, focusing on search, advertising, cloud computing, and hardware. Known for its innovative culture and high bar for engineering talent.',
    'Hard',
    '["Explain the difference between a process and a thread.", "How would you design a URL shortener like bit.ly?", "Describe a time you had to deal with a difficult technical trade-off.", "What happens when you type a URL into your browser?"]'::jsonb,
    NOW()
);

-- 5. Seed Matching Results (Agent 4 Output)
INSERT INTO public.resultat_matching (id, id_offre, id_utilisateur, score_global, competences_manquantes, points_forts, date_matching)
VALUES (
    gen_random_uuid(),
    'b1e1e1e1-e1e1-4e1e-b1e1-e1e1e1e1e1e1',
    '0198223b-21de-47c5-8915-afe255f9512f',
    88,
    '["Google Cloud Platform", "Kubernetes"]'::jsonb,
    '["Expert in React and Frontend optimization", "Solid Node.js backend experience", "Strong SQL knowledge", "Excellent problem-solving skills"]'::jsonb,
    NOW()
);
