export interface AnalysisStep {
  number: number;
  label: string;
}

export const ANALYSIS_STEPS: AnalysisStep[] = [
  { number: 1, label: 'Soumission' },
  { number: 2, label: 'Analyse' },
  { number: 3, label: 'Template' },
  { number: 4, label: 'Génération' },
  { number: 5, label: 'Résultats' },
];

export interface OfferCard {
  id: string;
  initials: string;
  title: string;
  company: string;
  location: string;
  matchingScore?: number;
  tags: string[];
  status: 'cv_genere' | 'non_traitee' | 'analysee';
  daysLeft?: number;
  urgent?: boolean;
  expired?: boolean;
  createdAt: Date;
  currentStep: number;
}