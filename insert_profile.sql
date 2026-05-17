DELETE FROM competence WHERE id_utilisateur = 'd0eebc34-e2b1-4504-9918-b1d4eeefe2b9';
DELETE FROM experience WHERE id_utilisateur = 'd0eebc34-e2b1-4504-9918-b1d4eeefe2b9';

INSERT INTO competence (id_utilisateur, nom, niveau, type_competence, is_valid) VALUES
('d0eebc34-e2b1-4504-9918-b1d4eeefe2b9', 'Angular', 5, 'TECHNIQUE', true),
('d0eebc34-e2b1-4504-9918-b1d4eeefe2b9', 'TypeScript', 4, 'TECHNIQUE', true),
('d0eebc34-e2b1-4504-9918-b1d4eeefe2b9', 'C#', 3, 'TECHNIQUE', true),
('d0eebc34-e2b1-4504-9918-b1d4eeefe2b9', 'HTML/CSS', 5, 'TECHNIQUE', true);

INSERT INTO experience (id_utilisateur, entreprise, poste, date_debut, date_fin, missions, is_valid, ville, type_contrat) VALUES
('d0eebc34-e2b1-4504-9918-b1d4eeefe2b9', 'TechCorp', 'Senior Frontend Developer', '2022-01-01', NULL, 'Lead developer for modern enterprise SPAs. Architecting high performance applications using Angular and Tailwind CSS.', true, 'Paris', 'CDI'),
('d0eebc34-e2b1-4504-9918-b1d4eeefe2b9', 'InnovInc', 'Web Developer', '2019-05-01', '2021-12-31', 'Fullstack developer creating backend web services with .NET Core and C#, and frontend reactive components.', true, 'Lyon', 'CDI');
