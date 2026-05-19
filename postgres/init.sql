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
-- ── Session coaching ───────────────────────────────────────────
-- Stocke la CONFIG et les RÉSULTATS d'une session
-- PAS les messages — c'est le rôle de chat_message
CREATE TABLE IF NOT EXISTS session_coaching (
    id_session          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    id_candidature      UUID REFERENCES candidature(id_candidature),
    id_utilisateur      UUID REFERENCES utilisateur(id_utilisateur) ON DELETE CASCADE,

    -- Mode
    mode                VARCHAR(10)  NOT NULL DEFAULT 'offer',  -- 'offer' | 'arena'
    language            VARCHAR(10)  NOT NULL DEFAULT 'en',
    duration_minutes    INTEGER      NOT NULL DEFAULT 20,
    status              VARCHAR(20)  NOT NULL DEFAULT 'pending',
                        -- 'pending' | 'in_progress' | 'completed'

    -- Arena Mode seulement (NULL si mode='offer')
    domain              VARCHAR(100),
    level               VARCHAR(20),                -- 'junior' | 'mid' | 'senior'
    focus_areas         JSONB,                      -- ["Algorithms", "System Design"]

    -- Résultats finaux
    score_entretien     INTEGER CHECK (score_entretien BETWEEN 0 AND 100),
    feedback_json       JSONB,
    -- {
    --   "dimensions": [{"name":"Clarity","score":80,"comment":"..."}],
    --   "strengths":     ["..."],
    --   "improvements":  ["..."],
    --   "coaching_tips": ["..."],
    --   "best_answer":   "...",
    --   "worst_answer":  "mieux de répondre : ..."
    -- }

    date_session        TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    completed_at        TIMESTAMP
);

CREATE INDEX idx_session_user        ON session_coaching(id_utilisateur);
CREATE INDEX idx_session_candidature ON session_coaching(id_candidature);


-- ── Question entraînement ──────────────────────────────────────
-- Chaque question posée pendant une session + réponse + correction
CREATE TABLE IF NOT EXISTS question_entrainement (
    id_question         UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    id_session          UUID REFERENCES session_coaching(id_session) ON DELETE CASCADE,

    texte_question      TEXT        NOT NULL,
    type_question       VARCHAR(20) DEFAULT 'behavioral',
                        -- 'behavioral' | 'technical' | 'situational'
    source              VARCHAR(20) DEFAULT 'generated',
                        -- 'glassdoor' | 'generated' | 'web_search'
    company_specific    BOOLEAN     DEFAULT FALSE,
    conseil_reponse     TEXT,                       -- tip STAR affiché avant

    -- Rempli après la session
    reponse_utilisateur TEXT,                       -- ce que l'user a dit
    correction_ia       TEXT,                       -- "Mieux de répondre : ..."
    score_reponse       INTEGER CHECK (score_reponse BETWEEN 0 AND 100),
    ordre               INTEGER DEFAULT 0
);

CREATE INDEX idx_question_session ON question_entrainement(id_session);


-- ── Chat message ───────────────────────────────────────────────
-- UN SEUL endroit pour TOUS les messages de TOUS les chats du module
-- tab Questions | tab Salaire | transcript mock interview
CREATE TABLE IF NOT EXISTS chat_message (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    thread_id           UUID        NOT NULL,
                        -- une conversation = un thread_id commun
    id_utilisateur      UUID REFERENCES utilisateur(id_utilisateur) ON DELETE CASCADE,
    id_session          UUID REFERENCES session_coaching(id_session) ON DELETE CASCADE,
                        -- NULL pour tabs Questions et Salaire sans session active
    id_candidature      UUID REFERENCES candidature(id_candidature),
                        -- NULL en Arena Mode

    chat_type           VARCHAR(20) NOT NULL,
                        -- 'questions' | 'salary' | 'interview'
    sender              VARCHAR(10) NOT NULL,        -- 'user' | 'ai'
    content             TEXT        NOT NULL,
    created_at          TIMESTAMP   DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_chat_thread   ON chat_message(thread_id);
CREATE INDEX idx_chat_session  ON chat_message(id_session);
CREATE INDEX idx_chat_user     ON chat_message(id_utilisateur, chat_type);




-- ── Agent 2 output (Offer Analyzer) ───────────────────────────
CREATE TABLE IF NOT EXISTS offre_analysee (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    id_offre UUID REFERENCES offres_emploi(id) ON DELETE CASCADE,
    titre_poste VARCHAR(200),
    entreprise VARCHAR(150),
    competences_requises JSONB,        -- ["Python", "Spark", ...]
    competences_souhaitees JSONB,
    keywords_ats JSONB,                -- ["ETL", "pipeline", ...]
    stack_technique JSONB,
    annees_experience INTEGER,
    type_contrat VARCHAR(50),
    localisation VARCHAR(150),
    texte_brut TEXT,
    date_analyse TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_offre_analysee_id_offre ON offre_analysee(id_offre);

-- ── Agent 3 output (Company Intel) ────────────────────────────
CREATE TABLE IF NOT EXISTS intel_entreprise (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    nom_entreprise VARCHAR(200) NOT NULL,
    id_offre UUID REFERENCES offres_emploi(id) ON DELETE CASCADE,
    note_glassdoor FLOAT,
    score_culture FLOAT,
    salaire_min INTEGER,
    salaire_max INTEGER,
    devise_salaire VARCHAR(10) DEFAULT 'MAD',
    actualites JSONB,                  -- ["news 1", "news 2"]
    resume_entreprise TEXT,
    difficulte_entretien VARCHAR(20),   -- "easy" | "medium" | "hard"
    questions_connues JSONB,           -- vraies questions Glassdoor
    date_collecte TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_intel_entreprise_nom ON intel_entreprise(nom_entreprise);
CREATE INDEX idx_intel_entreprise_offre ON intel_entreprise(id_offre);

-- ── Agent 4 output (Profile Matcher) ──────────────────────────
CREATE TABLE IF NOT EXISTS resultat_matching (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    id_offre UUID REFERENCES offres_emploi(id) ON DELETE CASCADE,
    id_utilisateur UUID REFERENCES utilisateur(id_utilisateur) ON DELETE CASCADE,
    score_global INTEGER,              -- 0-100
    competences_manquantes JSONB,      -- ["Kafka", "Docker"]
    points_forts JSONB,               -- ["Python", "Spark"]
    ecart_experience INTEGER DEFAULT 0,-- en années
    date_matching TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(id_offre, id_utilisateur)   -- un seul match par couple
);

CREATE INDEX idx_matching_offre_user ON resultat_matching(id_offre, id_utilisateur);




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