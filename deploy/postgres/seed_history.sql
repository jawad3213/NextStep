-- Script d'injection de données mock pour l'historique de l'Arena
-- Cible l'utilisateur : cb09c6c2-9c04-403e-8b7c-e186b1d011f5 (Keycloak: 0198223b-21de-47c5-8915-afe255f9512f)

-- Insertion de 3 sessions de test réalistes
INSERT INTO session_coaching 
(id_session, id_utilisateur, mode, language, duration_minutes, status, domain, level, score_entretien, date_session, completed_at)
VALUES 
(gen_random_uuid(), 'cb09c6c2-9c04-403e-8b7c-e186b1d011f5', 'arena', 'fr', 20, 'completed', 'Data Science', 'Senior', 85, NOW() - INTERVAL '1 day', NOW() - INTERVAL '23 hours'),
(gen_random_uuid(), 'cb09c6c2-9c04-403e-8b7c-e186b1d011f5', 'offer', 'en', 30, 'completed', 'Software Engineer', 'Mid', 72, NOW() - INTERVAL '2 days', NOW() - INTERVAL '47 hours'),
(gen_random_uuid(), 'cb09c6c2-9c04-403e-8b7c-e186b1d011f5', 'arena', 'fr', 15, 'completed', 'Product Manager', 'Junior', 91, NOW() - INTERVAL '3 hours', NOW() - INTERVAL '2 hours')
ON CONFLICT DO NOTHING;
