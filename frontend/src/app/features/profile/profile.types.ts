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
}
