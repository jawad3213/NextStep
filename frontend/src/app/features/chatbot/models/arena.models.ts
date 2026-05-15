// src/app/features/chatbot/models/arena.models.ts
export type InterviewLevel = 'junior' | 'mid' | 'senior';
export type ActiveTab = 'questions' | 'interview' | 'salary';

export interface ArenaConfig {
  domain: string;
  level: InterviewLevel;
  duration_minutes: number;
  language: string;
  focus_areas: string[];
}

/** Unified config passed to the shared interview-session page.
 *  Works for both Arena mode and Offer-Based mode.
 */
export interface SessionConfig {
  mode: 'arena' | 'offer';
  domain: string;
  level: InterviewLevel;
  duration_minutes: number;
  language: string;
  focus_areas: string[];
  // Offer-specific (optional — undefined in Arena mode)
  offer_id?: string;
  job_title?: string;
  company?: string;
  job_description?: string;
  // Derived display helpers
  display_title?: string;   // e.g. "Software Dev Arena" or "Full-Stack Dev @ Google"
  display_emoji?: string;   // domain emoji
  session_id?: string;      // The unified session UUID
}

export interface ChatMessage {
  role: 'user' | 'ai';
  content: string;
  timestamp?: Date;
}

export interface QuestionItem {
  id: string;
  question: string;
  type: 'behavioral' | 'technical' | 'situational';
  source: 'glassdoor' | 'generated' | 'web_search';
  company_specific: boolean;
  tip: string;
}

export interface DimensionScore {
  name: string;
  score: number;
  comment: string;
}

export interface QuestionEvaluation {
  question: string;
  user_answer: string;
  score: number;
  correction: string;
}

export interface FeedbackResult {
  global_score: number;
  dimensions: DimensionScore[];
  question_evaluations: QuestionEvaluation[];
  strengths: string[];
  improvements: string[];
  best_answer: string;
  worst_answer: string;
  coaching_tips: string[];
}

export interface NegotiationStep {
  step: number;
  action: string;
  phrase: string;
  why: string;
}

export interface SalaryResult {
  range_min: number;
  range_max: number;
  currency: string;
  your_target: number;
  confidence_level: string;
  market_sources: string[];
  negotiation_script: NegotiationStep[];
}

export interface SessionSummary {
  idSession: string;
  mode: string;
  status: string;
  language: string;
  durationMinutes: number;
  domain: string;
  level: string;
  scoreEntretien: number | null;
  dateSession: Date;
  completedAt?: Date;
}

export interface PastSessionDto {
  id: string;
  id_session: string;
  mode: string;
  status: string;
  language: string;
  durationMinutes: number;
  domain: string;
  level: string;
  scoreEntretien: number | null;
  dateSession: string;
}

export interface SessionCoachingDetailsDto {
  id: string;
  global_score: number;
  mode: string;
  domain: string;
  level: string;
  date: string;
  dimensions: DimensionScore[];
  strengths: string[];
  improvements: string[];
  coaching_tips: string[];
}

export const DOMAINS = [
  { key: 'software', name: 'Software Dev', emoji: '💻', count: '2.4k' },
  { key: 'data', name: 'Data & AI', emoji: '📊', count: '1.8k' },
  { key: 'design', name: 'Design / UX', emoji: '🎨', count: '920' },
  { key: 'product', name: 'Product Mgmt', emoji: '📈', count: '1.1k' },
  { key: 'finance', name: 'Finance', emoji: '💼', count: '860' },
  { key: 'engineering', name: 'Engineering', emoji: '🏗️', count: '1.3k' },
  { key: 'sales', name: 'Sales & Biz', emoji: '🤝', count: '740' },
  { key: 'consulting', name: 'Consulting', emoji: '🎓', count: '580' },
];

export const LEVELS = [
  { key: 'junior', label: 'Junior', desc: 'Building fundamentals', years: '0–2 yrs' },
  { key: 'mid', label: 'Mid-level', desc: 'Growing ownership', years: '2–5 yrs' },
  { key: 'senior', label: 'Senior', desc: 'Leading & mentoring', years: '5–10 yrs' },
];

export const DURATIONS = [
  { value: 10, label: '10', unit: 'min', rec: '' },
  { value: 15, label: '15', unit: 'min', rec: 'Quick' },
  { value: 20, label: '20', unit: 'min', rec: 'Recommended' },
  { value: 30, label: '30', unit: 'min', rec: 'Full sim' },
];

export const LANGUAGES = [
  { key: 'en', flag: '🇬🇧', name: 'English' },
  { key: 'fr', flag: '🇫🇷', name: 'Français' },
  { key: 'es', flag: '🇪🇸', name: 'Español' },
  { key: 'ar', flag: '🇸🇦', name: 'العربية' },
  { key: 'de', flag: '🇩🇪', name: 'Deutsch' },
  { key: 'zh', flag: '🇨🇳', name: '中文' },
];

export const FOCUS_BY_DOMAIN: Record<string, string[]> = {
  software: ['Algorithms', 'System Design', 'OOP', 'APIs & REST', 'DevOps', 'Frontend', 'Behavioral'],
  data: ['Machine Learning', 'SQL', 'Python', 'ETL Pipelines', 'Spark', 'Data Modeling', 'Behavioral'],
  design: ['UX Research', 'Figma', 'Design Systems', 'Prototyping', 'Accessibility', 'Behavioral'],
  product: ['Roadmapping', 'Metrics', 'A/B Testing', 'Stakeholder Mgmt', 'Go-to-Market', 'Behavioral'],
  finance: ['Financial Modeling', 'Valuation', 'Risk Management', 'Excel', 'Behavioral', 'Case Studies'],
  engineering: ['Structural Design', 'Project Mgmt', 'CAD', 'Safety', 'Behavioral'],
  sales: ['Discovery', 'Objection Handling', 'CRM', 'Negotiation', 'Behavioral'],
  consulting: ['Case Studies', 'Frameworks', 'Stakeholders', 'Slide Decks', 'Behavioral'],
};