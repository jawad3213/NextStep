export interface UserProfile {
  id: string;
  keycloakId: string;
  nom: string | null;
  prenom: string | null;
  email: string;
  lienLinkedin?: string | null;
  lienGithub?: string | null;
  lienPortfolio?: string | null;
  resumeProfessionnel?: string | null;
  coordonnees?: string | null;
  onboardingCompleted: boolean;
  profileScore: number;
  dateInscription: string;
}

export interface UserProfileResponse {
  message: string;
  data: UserProfile;
}

export interface ProfileStatus {
  isComplete: boolean;
  onboardingCompleted: boolean;
  profileScore: number;
  missingSections: string[];
}

// Enum types matching backend
export type ObjectifEnum = 'STAGE' | 'ALTERNANCE' | 'PREMIER_EMPLOI' | 'CDI' | 'FREELANCE';
export type NiveauEnum = 'BAC' | 'BAC_PLUS_2' | 'BAC_PLUS_3' | 'BAC_PLUS_5' | 'DOCTORAT';
export type SecteurEnum =
  | 'INFORMATIQUE' | 'FINANCE' | 'MARKETING' | 'SANTE'
  | 'INGENIERIE' | 'DROIT' | 'EDUCATION' | 'COMMERCE'
  | 'DESIGN' | 'COMMUNICATION' | 'RESSOURCES_HUMAINES' | 'AUTRE';

export interface SoftOnboardingPayload {
  objectif: ObjectifEnum;
  niveau: NiveauEnum;
  secteur: SecteurEnum;
}

// Display labels for enums (FR)
export const OBJECTIF_LABELS: Record<ObjectifEnum, string> = {
  STAGE: 'Find an Internship',
  ALTERNANCE: 'Find an Apprenticeship',
  PREMIER_EMPLOI: 'Land a first job',
  CDI: 'Find a Full-time Job',
  FREELANCE: 'Become a Freelancer',
};

export const NIVEAU_LABELS: Record<NiveauEnum, string> = {
  BAC: 'High School Diploma',
  BAC_PLUS_2: "Associate's Degree (2 years)",
  BAC_PLUS_3: "Bachelor's Degree (3 years)",
  BAC_PLUS_5: "Master's Degree (5 years)",
  DOCTORAT: 'PhD / Doctorate',
};

export const SECTEUR_LABELS: Record<SecteurEnum, string> = {
  INFORMATIQUE: 'IT & Tech',
  FINANCE: 'Finance & Accounting',
  MARKETING: 'Marketing & Digital',
  SANTE: 'Health & Medical',
  INGENIERIE: 'Engineering',
  DROIT: 'Law & Legal',
  EDUCATION: 'Education & Training',
  COMMERCE: 'Sales & Commerce',
  DESIGN: 'Design & Creative',
  COMMUNICATION: 'Communication & Media',
  RESSOURCES_HUMAINES: 'Human Resources',
  AUTRE: 'Other Sector',
};
