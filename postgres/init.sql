-- MODULE IDENTITY & PROFILE
CREATE TABLE utilisateur (
    id_utilisateur UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    keycloak_id VARCHAR(255) NOT NULL UNIQUE,
    nom VARCHAR(100),
    prenom VARCHAR(100),
    email VARCHAR(255) NOT NULL UNIQUE,
    lien_linkedin VARCHAR(500),
    lien_github VARCHAR(500),
    lien_portfolio VARCHAR(500),
    titre_poste VARCHAR(200),
    photo_url VARCHAR(500),
    ville VARCHAR(100),
    pays VARCHAR(100),
    telephone VARCHAR(20),
    resume_professionnel TEXT,
    coordonnees TEXT,
    titres_sections JSONB,
    objectif VARCHAR(50),
    niveau VARCHAR(50),
    secteur VARCHAR(50),
    onboarding_completed BOOLEAN DEFAULT false,
    onboarding_step INTEGER DEFAULT 0,
    onboarding_data JSONB,
    profile_score INTEGER DEFAULT 0,
    date_inscription TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Index pour les recherches fréquentes
CREATE INDEX idx_utilisateur_keycloak_id ON utilisateur(keycloak_id);
CREATE INDEX idx_utilisateur_email ON utilisateur(email);

CREATE TABLE IF NOT EXISTS experience (
    id_experience UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    id_utilisateur UUID REFERENCES utilisateur(id_utilisateur) ON DELETE CASCADE,
    entreprise VARCHAR(150),
    poste VARCHAR(150),
    date_debut DATE,
    date_fin DATE,
    missions TEXT,
    is_valid BOOLEAN DEFAULT FALSE
);

CREATE TABLE IF NOT EXISTS formation (
    id_formation UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    id_utilisateur UUID REFERENCES utilisateur(id_utilisateur) ON DELETE CASCADE,
    etablissement VARCHAR(150),
    diplome VARCHAR(150),
    annee INTEGER
);

CREATE TABLE IF NOT EXISTS projet (
    id_projet UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    id_utilisateur UUID REFERENCES utilisateur(id_utilisateur) ON DELETE CASCADE,
    titre_projet VARCHAR(150),
    description TEXT,
    technologies_utilisees TEXT,
    lien_projet VARCHAR(255),
    date_realisation DATE,
    is_valid BOOLEAN DEFAULT FALSE
);

CREATE TABLE IF NOT EXISTS certification (
    id_certification UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    id_utilisateur UUID REFERENCES utilisateur(id_utilisateur) ON DELETE CASCADE,
    nom VARCHAR(150),
    organisme VARCHAR(150),
    date_obtention DATE,
    url_verification VARCHAR(255)
);

CREATE TABLE IF NOT EXISTS competence (
    id_competence UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    id_utilisateur UUID REFERENCES utilisateur(id_utilisateur) ON DELETE CASCADE,
    nom VARCHAR(100),
    niveau INTEGER CHECK (niveau BETWEEN 1 AND 5),
    type_competence VARCHAR(20),
    is_valid BOOLEAN DEFAULT FALSE
);

-- MODULE OFFRES
CREATE TABLE IF NOT EXISTS offres_emploi (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    utilisateur_id UUID REFERENCES utilisateur(id_utilisateur) ON DELETE CASCADE,
    texte_brut TEXT NOT NULL,
    analyse_json JSONB,
    date_creation TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS keyword (
    id_keyword SERIAL PRIMARY KEY,
    id_offre UUID REFERENCES offres_emploi(id) ON DELETE CASCADE,
    label VARCHAR(100),
    poids_pertinence FLOAT
);

-- MODULE CANDIDATURES
CREATE TABLE IF NOT EXISTS candidature (
    id_candidature UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    id_utilisateur UUID REFERENCES utilisateur(id_utilisateur),
    id_offre UUID REFERENCES offres_emploi(id),
    date_creation TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    inclure_lettre_motivation BOOLEAN DEFAULT FALSE,
    statut VARCHAR(50) DEFAULT 'EN_ATTENTE'
);

CREATE TABLE IF NOT EXISTS document_genere (
    id_document UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    id_candidature UUID UNIQUE REFERENCES candidature(id_candidature) ON DELETE CASCADE,
    cv_contenu_ia_json JSONB,
    lettre_motiv_contenu_ia TEXT,
    chemin_pdf_cv VARCHAR(255),
    chemin_pdf_lettre VARCHAR(255),
    version INTEGER DEFAULT 1,
    date_generation TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- MODULE COACHING
CREATE TABLE IF NOT EXISTS session_coaching (
    id_session UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    id_candidature UUID REFERENCES candidature(id_candidature),
    transcript_chat JSONB,
    score_performance INTEGER,
    feedback_ia TEXT,
    date_session TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS question_entrainement (
    id_question UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    id_session UUID REFERENCES session_coaching(id_session) ON DELETE CASCADE,
    texte_question TEXT,
    conseil_reponse TEXT
);
CREATE TABLE IF NOT EXISTS email_draft (
    id_email_draft UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    id_candidature UUID NOT NULL REFERENCES candidature(id_candidature) ON DELETE CASCADE,
    type_email VARCHAR(50) NOT NULL DEFAULT 'application',
    recipient_email VARCHAR(255),
    objet VARCHAR(255) NOT NULL,
    corps TEXT NOT NULL,
    langue VARCHAR(10) DEFAULT 'fr',
    est_approuve BOOLEAN DEFAULT FALSE,
    est_envoye BOOLEAN DEFAULT FALSE,
    error_message TEXT,
    date_creation TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    date_modification TIMESTAMP NULL,
    date_envoi TIMESTAMP NULL
);