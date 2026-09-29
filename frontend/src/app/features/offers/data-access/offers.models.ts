// ── Analysis pipeline (UI) ──────────────────────────────────────────────────

export type OfferStepId = 'submit' | 'analysis' | 'template' | 'generation' | 'results';

export interface OfferStep {
  id: OfferStepId;
  label: string;
  icon: string;
}

// ── API: /api/offers ────────────────────────────────────────────────────────

export interface OfferSubmitPayload {
  rawText: string;
  templateId: number;
}

export interface OfferSubmitResponse {
  offerId: string;
  status: string;
}

/** GET /api/offers — one analysed offer of the user. */
export interface OfferHistoryItem {
  offerId: string;
  titre: string;
  entreprise: string;
  localisation: string;
  scoreMatching?: number;
  status: 'cv_genere' | 'non_traitee' | 'analysee';
  currentStep: number;
  dateCreation: string;
}

export interface SkillDetail {
  nom: string;
  categorie: 'technique' | 'soft' | 'langue' | 'certification';
  statut: 'correspond' | 'partiel' | 'manquant';
}

export interface RecommendationPriorisee {
  texte: string;
  priorite: 'haute' | 'moyenne' | 'basse';
}

export interface KeywordPondere {
  mot: string;
  poids: number;
}

/** GET /api/offers/{id}/analysis — the offer analysis (and the generated CV data once available). */
export interface OfferAnalysisResponse {
  offerId: string;
  /** Identifies the run that produced this analysis; null for results stored before it existed. */
  runId?: string | null;
  titre: string;
  entreprise: string | null;
  typeContrat: string | null;
  localisation: string | null;
  competencesRequises: string[];
  competencesSouhaitees: string[];
  keywordsAts: string[];
  anneesExperience: number | null;
  niveauEtudes: string | null;
  modeTravail?: string;
  descriptionPoste: string | null;
  texteBrut: string | null;
  scoreMatching: number;
  scoreAts: number;
  keywordsPresents: string[];
  keywordsManquants: string[];
  recommandations: string[];
  competencesMatching: string[];
  competencesManquantes: string[];
  companyCultureScore: number;
  companySalaryMin: number;
  companySalaryMax: number;
  companySize: string;
  companyNews: { title: string; date: string }[];
  dateAnalyse: string;
  erreurs: string[];
  competencesAvecDetails?: SkillDetail[];
  recommandationsAvecPriorite?: RecommendationPriorisee[];
  keywordsAvecPoids?: KeywordPondere[];
  forcesProfil?: string[];
  cvGeneratedContent?: any;
  cv_data?: any;
  cvData?: any;
  profileData?: any;
  profile_data?: any;
}

/** Result of the CV generation phase (the analysis enriched with the generated CV). */
export interface ResumePipelineResponse {
  cvGeneratedContent?: any;
  cv_data?: any;
  cvData?: any;
  profileData?: any;
  profile_data?: any;
  _cvPending?: boolean;
  cv_optimized_content?: any;
  cvOptimizedContent?: any;
  email_subject?: string;
  email_body?: string;
  recruiter_name?: string;
  [key: string]: any;
}
