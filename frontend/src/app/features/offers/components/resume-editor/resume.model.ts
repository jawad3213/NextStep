export interface PersonalDetails {
  fullName: string;
  title: string;
  email: string;
  phone: string;
  location: string;
  photoUrl: string | null;
  linkedIn: string | null;
  gitHub: string | null;
  portfolio: string | null;
}

export interface Experience {
  role: string;
  company: string;
  location?: string;
  start: string;
  end: string;
  bullets: string[];
}

export interface Education {
  degree: string;
  institution: string;
  location?: string;
  year: string;
  gpa?: string;
}

export interface Skill {
  name: string;
  level: number;
  category?: string;
  isMatched?: boolean;
}

export interface Language {
  name: string;
  proficiency: string;
}

export interface Project {
  title: string;
  bullets: string[];
  technologies?: string[];
}

export interface Certification {
  name: string;
  issuer: string;
  date?: string;
}

export type TemplateId = 'chrono' | 'circular' | 'modern' | 'elegant' | 'luxe';

export interface ResumeData {
  personalDetails: PersonalDetails;
  summary: string;
  experience: Experience[];
  education: Education[];
  skills: Skill[];
  languages: Language[];
  projects: Project[];
  certifications: Certification[];
  activities: string[];
  selectedTemplate: TemplateId;
  primaryColor: string;
  fontSize: 'sm' | 'base' | 'lg';
  atsScore?: number;
  matchingScore?: number;
}

export const INITIAL_RESUME_DATA: ResumeData = {
  personalDetails: {
    fullName: '',
    title: '',
    email: '',
    phone: '',
    location: '',
    photoUrl: null,
    linkedIn: null,
    gitHub: null,
    portfolio: null,
  },
  summary: '',
  experience: [],
  education: [],
  skills: [],
  languages: [],
  projects: [],
  certifications: [],
  activities: [],
  selectedTemplate: 'chrono',
  primaryColor: '#465fff',
  fontSize: 'base',
};
