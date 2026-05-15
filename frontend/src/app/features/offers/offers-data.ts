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
  id: number;
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

export const MOCK_OFFERS: OfferCard[] = [
  {
    id: 0,
    initials: 'CP',
    title: 'Full Stack Stage',
    company: 'Capgemini Maroc',
    location: 'Casablanca',
    matchingScore: 84,
    tags: ['React', 'Node.js', 'PostgreSQL'],
    status: 'cv_genere',
    daysLeft: 10,
    createdAt: new Date('2026-05-10'),
    currentStep: 5,
  },
  {
    id: 1,
    initials: 'CG',
    title: 'Développeur Java Spring',
    company: 'CGI',
    location: 'Rabat',
    tags: ['Java', 'Spring Boot'],
    status: 'non_traitee',
    createdAt: new Date('2026-05-14'),
    currentStep: 0,
  },
  {
    id: 2,
    initials: 'OC',
    title: 'DevOps Stage',
    company: 'OCP Group',
    location: 'Jorf Lasfar',
    matchingScore: 71,
    tags: ['Docker', 'Kubernetes'],
    status: 'analysee',
    createdAt: new Date('2026-05-08'),
    currentStep: 2,
  },
  {
    id: 3,
    initials: 'MT',
    title: 'Data Engineer CDI',
    company: 'Maroc Telecom',
    location: 'Rabat',
    matchingScore: 58,
    tags: ['Python', 'Spark', 'Azure'],
    status: 'analysee',
    daysLeft: 2,
    urgent: true,
    createdAt: new Date('2026-05-01'),
    currentStep: 3,
  },
  {
    id: 4,
    initials: 'IN',
    title: 'Backend Python Stage',
    company: 'Intelcia',
    location: '',
    tags: [],
    status: 'non_traitee',
    expired: true,
    createdAt: new Date('2026-04-20'),
    currentStep: 1,
  },
];
