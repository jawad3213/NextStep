-- MODULE IDENTITY & PROFILE
CREATE TABLE IF NOT EXISTS utilisateur (
    id_utilisateur UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    keycloak_id VARCHAR(255) UNIQUE NOT NULL,
    nom VARCHAR(100),
    prenom VARCHAR(100),
    email VARCHAR(255) UNIQUE NOT NULL,
    lien_linkedin VARCHAR(255),
    lien_github VARCHAR(255),
    lien_portfolio VARCHAR(255),
    resume_professionnel TEXT,
    coordonnees VARCHAR(255),
    onboarding_status VARCHAR(20) DEFAULT 'NEW',
    profile_score INTEGER DEFAULT 0,
    date_inscription TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

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
CREATE TABLE IF NOT EXISTS offre (
    id_offre UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    titre_poste VARCHAR(150),
    entreprise VARCHAR(150),
    description_brute TEXT,
    localisation VARCHAR(150),
    url_source VARCHAR(255),
    date_scraping TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS keyword (
    id_keyword SERIAL PRIMARY KEY,
    id_offre UUID REFERENCES offre(id_offre) ON DELETE CASCADE,
    label VARCHAR(100),
    poids_pertinence FLOAT
);

-- MODULE CANDIDATURES
CREATE TABLE IF NOT EXISTS candidature (
    id_candidature UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    id_utilisateur UUID REFERENCES utilisateur(id_utilisateur),
    id_offre UUID REFERENCES offre(id_offre),
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
