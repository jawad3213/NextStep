import {
  CertificationDto,
  CompetenceDto,
  ExperienceDto,
  FormationDto,
  FullProfileDto,
  PersonalInfoDto,
  ProjetDto,
} from './profile-api.models';
import { Certification, Education, Experience, Language, PersonalInfo, Profile, Project, Skill } from './profile.models';

/** Fallbacks for the name/email when the backend has none yet (the Keycloak account). */
export interface AccountIdentity {
  firstName?: string;
  lastName?: string;
  email?: string;
}

// ── Backend → UI ────────────────────────────────────────────────────────────

/** Maps GET /api/profile to the UI profile. The photo URL is resolved separately. */
export function toProfile(data: FullProfileDto, account: AccountIdentity | null, defaultTitles: Profile['sectionTitles']): Profile {
  const info = data.personalInfo;

  let sectionTitles = defaultTitles;
  if (info.titresSections) {
    try {
      sectionTitles = JSON.parse(info.titresSections);
    } catch {
      console.warn('Failed to parse section titles, using default titles');
    }
  }

  const competences = data.competences || [];
  const isLanguage = (c: CompetenceDto) => {
    const type = (c.typeCompetence || '').toLowerCase();
    return type.includes('lang') || type.includes('linguist');
  };

  return {
    personal: {
      firstName: info.prenom || account?.firstName || '',
      lastName: info.nom || account?.lastName || '',
      email: info.email || account?.email || '',
      phone: info.telephone || '',
      city: info.ville || '',
      country: info.pays || '',
      jobTitle: info.titrePoste || '',
      photoUrl: null,
      linkedinUrl: info.lienLinkedin || '',
      githubUrl: info.lienGithub || '',
      portfolioUrl: info.lienPortfolio || '',
      address: (info.ville || info.pays)
        ? `${info.ville || ''}, ${info.pays || ''}`.trim().replace(/^,|,$/g, '')
        : '',
      useAsHeadline: true,
    },
    education: (data.formations || []).map(f => ({
      id: f.id ?? '',
      degree: f.diplome ?? '',
      institution: f.etablissement ?? '',
      startYear: (f.annee || 2024).toString(),
      endYear: (f.anneeFin || 2024).toString(),
      current: !f.anneeFin,
      specialization: f.specialisation || '',
      mention: (f.mention || 'Passable') as Education['mention'],
      city: f.ville || '',
    })),
    experience: (data.experiences || []).map(e => ({
      id: e.id ?? '',
      title: e.poste ?? '',
      company: e.entreprise ?? '',
      startDate: (e.dateDebut || '').substring(0, 7),
      endDate: (e.dateFin || '').substring(0, 7),
      current: !e.dateFin,
      description: e.missions || '',
      city: e.ville || '',
      type: (e.type === 'Parascolaire' || e.type === 'Extracurricular'
        ? 'Extracurricular'
        : (e.type || 'Internship')) as Experience['type'],
      taches: e.taches || [],
    })),
    skills: competences.filter(c => !isLanguage(c)).map(c => ({
      id: c.id ?? '',
      name: c.nom ?? '',
      category: (c.typeCompetence === 'Technique' || c.typeCompetence === 'Technical') ? 'Technical' : (c.typeCompetence || 'Technical'),
    })),
    languages: competences.filter(isLanguage).map(c => ({
      id: c.id ?? '',
      name: c.nom ?? '',
      level: mapIntToLevel(c.niveau || 3) as Language['level'],
    })),
    resume: info.resumeProfessionnel || '',
    projets: (data.projets || []).map(p => ({
      id: p.id ?? '',
      title: p.titreProjet ?? '',
      description: p.description ?? '',
      stack: (p.technologiesUtilisees || '').split(',').map(s => s.trim()).filter(s => s !== ''),
      githubUrl: p.lienProjet || '',
      demoUrl: p.demoUrl || '',
      imageUrl: p.imageUrl || '',
      isUniversity: p.isUniversity || false,
      taches: p.taches || [],
    })),
    certifications: (data.certifications || []).map(c => ({
      id: c.id ?? '',
      name: c.titre ?? '',
      issuer: c.organisation ?? '',
      date: c.dateObtention ?? '',
      verificationUrl: c.urlCredential ?? '',
    })),
    sectionTitles,
  };
}

// ── UI → Backend ────────────────────────────────────────────────────────────

export function toPersonalInfoDto(info: PersonalInfo, resume: string, sectionTitles: Profile['sectionTitles']): PersonalInfoDto {
  return {
    nom: info.lastName,
    prenom: info.firstName,
    email: info.email,
    telephone: info.phone,
    ville: info.city,
    pays: info.country,
    titrePoste: info.jobTitle,
    lienLinkedin: info.linkedinUrl,
    lienGithub: info.githubUrl,
    lienPortfolio: info.portfolioUrl,
    resumeProfessionnel: resume,
    titresSections: JSON.stringify(sectionTitles),
  };
}

export function toExperienceDto(exp: Experience, includeId: boolean): ExperienceDto {
  const dateD = exp.startDate ? (exp.startDate.includes('-') ? exp.startDate : exp.startDate + '-01') : null;
  const dateF = exp.endDate ? (exp.endDate.includes('-') ? exp.endDate : exp.endDate + '-01') : null;
  return {
    ...(includeId ? { id: exp.id } : {}),
    entreprise: exp.company,
    poste: exp.title,
    dateDebut: safeIsoDate(dateD),
    dateFin: safeIsoDate(dateF),
    missions: exp.description,
    ville: exp.city,
    type: exp.type,
    taches: exp.taches ?? [],
  };
}

export function toFormationDto(edu: Education, includeId: boolean): FormationDto {
  return {
    ...(includeId ? { id: edu.id } : {}),
    etablissement: edu.institution,
    diplome: edu.degree,
    annee: parseInt(edu.startYear) || 2024,
    ville: edu.city,
    specialisation: edu.specialization,
    mention: edu.mention,
    anneeFin: parseInt(edu.endYear) || null,
  };
}

export function toSkillDto(skill: Skill, includeId: boolean): CompetenceDto {
  return {
    ...(includeId ? { id: skill.id } : {}),
    nom: skill.name,
    niveau: 3,
    typeCompetence: skill.category,
  };
}

export function toLanguageDto(lang: { id?: string; name: string; level: string }, includeId: boolean): CompetenceDto {
  return {
    ...(includeId ? { id: lang.id } : {}),
    nom: lang.name,
    niveau: mapLevelToInt(lang.level),
    typeCompetence: 'Langue',
  };
}

export function toProjetDto(p: Project, includeId: boolean): ProjetDto {
  return {
    ...(includeId ? { id: p.id } : {}),
    titreProjet: p.title,
    description: p.description,
    technologiesUtilisees: p.stack.join(','),
    lienProjet: p.githubUrl,
    demoUrl: p.demoUrl,
    imageUrl: p.imageUrl,
    isUniversity: p.isUniversity,
    taches: p.taches ?? [],
  };
}

export function toCertificationDto(c: Certification, includeId: boolean): CertificationDto {
  return {
    ...(includeId ? { id: c.id } : {}),
    titre: c.name,
    organisation: c.issuer,
    dateObtention: safeIsoDate(c.date),
    urlCredential: c.verificationUrl,
  };
}

// ── Value conversions ───────────────────────────────────────────────────────

/** ISO date, or null for empty / "present" / unparsable values. */
export function safeIsoDate(dateStr: string | null | undefined): string | null {
  if (!dateStr) return null;
  try {
    const clean = dateStr.trim();
    if (!clean) return null;
    const lower = clean.toLowerCase();
    if (lower.includes('present') || lower.includes('cours') || lower.includes('now') || lower.includes('en cours') || lower === 'null') {
      return null;
    }
    const d = new Date(clean);
    if (isNaN(d.getTime())) {
      return null;
    }
    return d.toISOString();
  } catch {
    return null;
  }
}

function normalizeDiacritics(s: string): string {
  return s.normalize('NFD').replace(/[̀-ͯ]/g, '');
}

const LEVEL_TO_INT: Record<string, number> = {
  'A1': 1, 'A2': 2, 'B1': 3, 'B2': 4, 'C1': 5, 'C2': 6, 'Native': 7,
  'Natif': 7, 'Maternelle': 7, 'Langue maternelle': 7,
  'BEGINNER': 1, 'ELEMENTARY': 2, 'INTERMEDIATE': 3, 'UPPER-INTERMEDIATE': 4,
  'ADVANCED': 5, 'PROFICIENT': 6, 'FLUENT': 6,
  'COURANT': 5, 'BILINGUE': 7, 'BILINGUAL': 7,
  'Debutant': 1, 'Intermediaire': 3, 'Avancé': 5, 'Expert': 7,
  'Intermédiaire': 3, 'Débutant': 1,
  'Lu écrit parlé': 5, 'Lu, écrit, parlé': 5,
  'Bonne maîtrise': 5, 'Notions': 1, 'Scolaire': 3,
  'Élémentaire': 1, 'Elementaire': 1, 'Professionnel': 6,
  'Maternel': 7,
};

/** Language level label (CEFR, French or English wording) → 1..7. Unknown levels count as B1. */
export function mapLevelToInt(level: string): number {
  const clean = normalizeDiacritics((level || '').replace(/\s*\(.*?\)\s*/g, '').trim()).toUpperCase();
  const caseInsensitive: Record<string, number> = {};
  for (const key in LEVEL_TO_INT) {
    caseInsensitive[normalizeDiacritics(key).toUpperCase()] = LEVEL_TO_INT[key];
  }
  return caseInsensitive[clean] || 3;
}

/** 1..7 → CEFR level (7 = Native). */
export function mapIntToLevel(val: number): string {
  const levels: Record<number, string> = { 1: 'A1', 2: 'A2', 3: 'B1', 4: 'B2', 5: 'C1', 6: 'C2', 7: 'Native' };
  return levels[val] || 'B1';
}

/** French contract type (as written in CVs) → the UI experience type. */
export function mapExperienceType(type: string): Experience['type'] {
  // 'Apprenticeship' is not in Experience['type'] but is what imported apprenticeships have always used.
  const mapping: Record<string, string> = {
    'Stage': 'Internship',
    'Alternance': 'Apprenticeship',
    'CDI': 'CDI',
    'CDD': 'CDD',
    'Freelance': 'Freelance',
    'PFA': 'PFA',
    'PFE': 'PFE',
    'Parascolaire': 'Extracurricular',
    'Extracurricular': 'Extracurricular',
  };
  return (mapping[type] || 'Internship') as Experience['type'];
}
