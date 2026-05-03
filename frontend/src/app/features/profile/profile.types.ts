export type ProfileStepId = 'coordonnees' | 'formation' | 'experience' | 'competences' | 'resume' | 'projets' | 'certifications';

export interface PersonalInfo {
  firstName: string;
  lastName: string;
  email: string;
  phone: string;
  jobTitle: string;
  address: string;
  city: string;
  country: string;
  linkedinUrl: string;
  githubUrl: string;
  photoUrl: string | null;
  useAsHeadline: boolean;
}

export interface Education {
  id: string;
  degree: string;
  institution: string;
  city: string;
  startYear: string;
  endYear: string;
  current: boolean;
  specialization: string;
  mention: 'Passable' | 'Bien' | 'Très bien' | 'Excellent';
}

export interface Experience {
  id: string;
  title: string;
  company: string;
  city: string;
  startDate: string;
  endDate: string;
  current: boolean;
  type: 'Stage' | 'Alternance' | 'CDI' | 'CDD' | 'Freelance' | 'PFA' | 'PFE';
  description: string;
}

export interface Skill {
  id: string;
  name: string;
  category?: string;
}

export interface Language {
  id: string;
  name: string;
  level: 'A1' | 'A2' | 'B1' | 'B2' | 'C1' | 'C2' | 'Natif';
}

export interface Project {
  id: string;
  title: string;
  description: string;
  stack: string[];
  githubUrl: string;
  demoUrl: string;
  imageUrl?: string;
  isUniversity: boolean;
}

export interface Certification {
  id: string;
  name: string;
  issuer: string;
  date: string;
  verificationUrl: string;
}

export interface Profile {
  personal: PersonalInfo;
  education: Education[];
  experience: Experience[];
  skills: Skill[];
  languages: Language[];
  resume: string;
  projets: Project[];
  certifications: Certification[];
  sectionTitles?: {
    formation?: string;
    experience?: string;
    competences?: string;
    projets?: string;
    certifications?: string;
  };
}

// Display Mappings for UI
export const MENTION_LABELS: Record<string, string> = {
  'Passable': 'Pass',
  'Bien': 'Good',
  'Très bien': 'Very Good',
  'Excellent': 'Excellent'
};

export const EXPERIENCE_TYPE_LABELS: Record<string, string> = {
  'Stage': 'Internship',
  'Alternance': 'Apprenticeship',
  'CDI': 'Full-time (CDI)',
  'CDD': 'Fixed-term (CDD)',
  'Freelance': 'Freelance',
  'PFA': 'Academic Project (PFA)',
  'PFE': 'Graduation Project (PFE)'
};

export const DEGREE_LABELS: Record<string, string> = {
  'licence': "Bachelor's Degree",
  'master': "Master's Degree",
  'ingenieur': 'Engineering Degree',
  'doctorat': 'PhD / Doctorate'
};

export const LANGUAGE_LEVEL_LABELS: Record<string, string> = {
  'A1': 'Beginner (A1)',
  'A2': 'Elementary (A2)',
  'B1': 'Intermediate (B1)',
  'B2': 'Upper-Intermediate (B2)',
  'C1': 'Advanced (C1)',
  'C2': 'Proficient (C2)',
  'Natif': 'Native'
};
