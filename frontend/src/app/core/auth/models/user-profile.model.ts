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
  STAGE: 'Trouver un stage',
  ALTERNANCE: 'Trouver une alternance',
  PREMIER_EMPLOI: 'Décrocher un premier emploi',
  CDI: 'Trouver un CDI',
  FREELANCE: 'Devenir freelance',
};

export const NIVEAU_LABELS: Record<NiveauEnum, string> = {
  BAC: 'Baccalauréat',
  BAC_PLUS_2: 'Bac +2 (BTS / DUT)',
  BAC_PLUS_3: 'Bac +3 (Licence)',
  BAC_PLUS_5: 'Bac +5 (Master / Ingénieur)',
  DOCTORAT: 'Doctorat',
};

export const SECTEUR_LABELS: Record<SecteurEnum, string> = {
  INFORMATIQUE: 'Informatique & Tech',
  FINANCE: 'Finance & Comptabilité',
  MARKETING: 'Marketing & Digital',
  SANTE: 'Santé & Médical',
  INGENIERIE: 'Ingénierie',
  DROIT: 'Droit & Juridique',
  EDUCATION: 'Éducation & Formation',
  COMMERCE: 'Commerce & Vente',
  DESIGN: 'Design & Création',
  COMMUNICATION: 'Communication & Média',
  RESSOURCES_HUMAINES: 'Ressources Humaines',
  AUTRE: 'Autre secteur',
};
