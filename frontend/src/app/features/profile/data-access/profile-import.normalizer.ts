import { Certification, Education, Experience, Project, ProfileImportSummary, Skill } from './profile.models';
import { mapExperienceType } from './profile.mapper';

/**
 * Normalises the profile extracted from a CV or a LinkedIn profile by the AI agents.
 * The agents' field names vary (French/English, snake/camel case, nested "data"), so every
 * section is read tolerantly, cleaned and de-duplicated. Pure functions, no I/O.
 */

export interface ImportedPersonalSnapshot {
  firstName: string;
  lastName: string;
  email: string;
  phone: string;
  jobTitle: string;
  city: string;
  country: string;
  linkedinUrl: string;
  githubUrl: string;
  portfolioUrl: string;
  address: string;
}

export interface ImportedProfilePayload {
  personal: ImportedPersonalSnapshot;
  resume: string;
  experience: Experience[];
  extracurriculars: Experience[];
  education: Education[];
  skills: Skill[];
  languages: { id: string; name: string; level: string }[];
  projects: Project[];
  certifications: Certification[];
}

function readImportObject(source: any, keys: string[]): any {
  for (const key of keys) {
    const value = source?.[key];
    if (value && typeof value === 'object' && !Array.isArray(value)) {
      return value;
    }
  }
  return {};
}

function readImportArray(source: any, keys: string[]): any[] {
  for (const key of keys) {
    const value = source?.[key];
    if (Array.isArray(value)) {
      return value;
    }
  }
  return [];
}

function readImportValue(source: any, keys: string[]): string {
  for (const key of keys) {
    const value = source?.[key];
    if (value === null || value === undefined) continue;
    const normalized = String(value).trim();
    if (normalized) {
      return normalized;
    }
  }
  return '';
}

function normalizeMonth(value: unknown): string {
  if (value === null || value === undefined) return '';

  const raw = String(value).trim();
  if (!raw) return '';

  const lowered = raw.toLowerCase();
  if (
    lowered.includes('present') ||
    lowered.includes('current') ||
    lowered.includes('ongoing') ||
    lowered.includes('en cours') ||
    lowered === 'null'
  ) {
    return '';
  }

  const exactMonth = raw.match(/(19|20)\d{2}[-/](0?[1-9]|1[0-2])/);
  if (exactMonth) {
    return `${exactMonth[0].slice(0, 4)}-${exactMonth[0].slice(5).padStart(2, '0')}`;
  }

  const yearOnly = raw.match(/\b(19|20)\d{2}\b/);
  if (yearOnly) {
    return `${yearOnly[0]}-01`;
  }

  const parsed = new Date(raw);
  if (!Number.isNaN(parsed.getTime())) {
    return `${parsed.getFullYear()}-${String(parsed.getMonth() + 1).padStart(2, '0')}`;
  }

  return '';
}

function normalizeYear(value: unknown): string {
  const monthValue = normalizeMonth(value);
  return monthValue ? monthValue.slice(0, 4) : '';
}

function normalizeStack(value: unknown): string[] {
  if (Array.isArray(value)) {
    return value
      .map(item => String(item).trim())
      .filter(Boolean);
  }

  if (typeof value === 'string') {
    return value
      .split(/[,\n|]/)
      .map(item => item.trim())
      .filter(Boolean);
  }

  return [];
}

function normalizeTasks(value: unknown): string[] {
  if (Array.isArray(value)) {
    return value
      .map(item => String(item).trim())
      .filter(Boolean);
  }

  if (typeof value === 'string') {
    return value
      .split(/\r?\n|[;|]/)
      .map(item => item.replace(/^[\-\u2022]\s*/, '').trim())
      .filter(Boolean);
  }

  return [];
}

function dedupeByKey<T>(items: T[], keySelector: (item: T) => string): T[] {
  const seen = new Set<string>();

  return items.filter(item => {
    const key = keySelector(item).trim().toLowerCase();
    if (!key || seen.has(key)) {
      return false;
    }

    seen.add(key);
    return true;
  });
}

function isKnownLanguage(value: string): boolean {
  return [
    'french', 'francais', 'français', 'english', 'anglais', 'arabic', 'arabe',
    'spanish', 'espagnol', 'german', 'allemand', 'italian', 'italien',
    'russian', 'russe', 'chinese', 'chinois', 'japanese', 'japonais',
    'portuguese', 'portugais', 'dutch', 'néerlandais', 'nederlands', 'flamand',
    'turkish', 'turc', 'korean', 'coréen', 'polish', 'polonais',
    'swedish', 'suédois', 'danish', 'danois', 'norwegian', 'norvégien',
    'finnish', 'finnois', 'greek', 'grec', 'hebrew', 'hébreu',
    'hindi', 'bengali', 'bengalais', 'thai', 'thaïlandais',
    'vietnamese', 'vietnamien', 'indonesian', 'indonésien',
    'malay', 'malais', 'romanian', 'roumain', 'czech', 'tchèque',
    'hungarian', 'hongrois', 'ukrainian', 'ukrainien',
    'catalan', 'serbian', 'serbe', 'croate', 'croatian',
    'bulgarian', 'bulgare', 'slovak', 'slovaque', 'slovenian', 'slovène',
    'lithuanian', 'lituanien', 'latvian', 'letton', 'estonian', 'estonien',
    'icelandic', 'islandais', 'swahili',
    'tagalog', 'filipino', 'persian', 'farsi', 'persan',
    'urdu', 'tamil', 'tamoul', 'telugu', 'marathi',
    'gujarati', 'kannada', 'malayalam', 'burmese', 'birman',
    'khmer', 'cambodgien', 'lao', 'laotien', 'mongolian', 'mongol',
    'nepali', 'népalais', 'sinhala', 'cinghalais',
    'amharic', 'amharique', 'georgian', 'géorgien', 'armenian', 'arménien',
    'azerbaijani', 'azerbaïdjanais', 'kazakh', 'uzbek', 'ouzbek',
    'turkmen', 'turkmène', 'albanian', 'albanais',
    'bosnian', 'bosnien', 'macedonian', 'macédonien',
    'welsh', 'gallois', 'irish', 'irlandais', 'gaelic', 'gaélique',
    'maltese', 'maltais', 'luxembourgish', 'luxembourgeois',
    'esperanto', 'espéranto', 'dari', 'pashto', 'pashtou',
    'somalian', 'somali', 'hausa', 'yoruba', 'igbo',
    'zulu', 'xhosa', 'afrikaans', 'tigrinya', 'tigrigna'
  ].includes(value.trim().toLowerCase());
}

export function normalizeImportedPayload(rawData: any): ImportedProfilePayload {
  const parsedData = typeof rawData === 'string'
    ? (() => {
        try {
          return JSON.parse(rawData);
        } catch {
          return {};
        }
      })()
    : (rawData ?? {});

  const source = parsedData?.data && typeof parsedData.data === 'object' ? parsedData.data : parsedData;
  const personalSource = readImportObject(source, ['personal', 'personalInfo', 'personal_info', 'contact']);

  const personal: ImportedPersonalSnapshot = {
    firstName: readImportValue(personalSource, ['prenom', 'firstName', 'first_name']),
    lastName: readImportValue(personalSource, ['nom', 'lastName', 'last_name']),
    email: readImportValue(personalSource, ['email']),
    phone: readImportValue(personalSource, ['telephone', 'phone', 'mobile']),
    jobTitle: readImportValue(personalSource, ['titrePoste', 'jobTitle', 'title', 'headline']),
    city: readImportValue(personalSource, ['ville', 'city']),
    country: readImportValue(personalSource, ['pays', 'country']),
    linkedinUrl: readImportValue(personalSource, ['lienLinkedin', 'linkedinUrl', 'linkedin']),
    githubUrl: readImportValue(personalSource, ['lienGithub', 'githubUrl', 'github']),
    portfolioUrl: readImportValue(personalSource, ['lienPortfolio', 'portfolioUrl', 'portfolio', 'website', 'siteWeb']),
    address: readImportValue(personalSource, ['adresse', 'address'])
  };

  const resume = readImportValue(personalSource, ['resumeProfessionnel', 'summary', 'resume', 'about'])
    || readImportValue(source, ['resumeProfessionnel', 'summary', 'resume', 'about']);

  const normalizedExperience = dedupeByKey(
    readImportArray(source, ['experience', 'experiences', 'workExperience', 'workExperiences']).map((entry: any) => ({
      id: '',
      company: readImportValue(entry, ['entreprise', 'company', 'organisation']),
      title: readImportValue(entry, ['poste', 'title', 'role', 'position']),
      startDate: normalizeMonth(entry?.dateDebut ?? entry?.startDate),
      endDate: normalizeMonth(entry?.dateFin ?? entry?.endDate),
      city: readImportValue(entry, ['ville', 'city']),
      description: readImportValue(entry, ['missions', 'description', 'summary']),
      current: !normalizeMonth(entry?.dateFin ?? entry?.endDate),
      type: mapExperienceType(readImportValue(entry, ['type', 'employmentType', 'contractType']) || 'Stage'),
      taches: normalizeTasks(entry?.taches ?? entry?.tasks ?? entry?.responsibilities)
    })).filter((entry) => entry.company || entry.title || entry.description),
    (entry) => `${entry.company}|${entry.title}|${entry.startDate}|${entry.endDate}`
  );

  const normalizedExtracurriculars = dedupeByKey(
    readImportArray(source, ['extracurricular', 'extracurriculars', 'parascolaire', 'activities']).map((entry: any) => ({
      id: '',
      company: readImportValue(entry, ['organisation', 'company', 'entreprise', 'club']),
      title: readImportValue(entry, ['titre', 'title', 'role', 'poste']) || 'Activity',
      startDate: normalizeMonth(entry?.dateDebut ?? entry?.startDate),
      endDate: normalizeMonth(entry?.dateFin ?? entry?.endDate),
      city: readImportValue(entry, ['ville', 'city']),
      description: readImportValue(entry, ['description', 'missions', 'summary']),
      current: !normalizeMonth(entry?.dateFin ?? entry?.endDate),
      type: 'Extracurricular' as const,
      taches: normalizeTasks(entry?.taches ?? entry?.tasks ?? entry?.responsibilities)
    })).filter((entry) => entry.company || entry.title || entry.description),
    (entry) => `${entry.company}|${entry.title}|${entry.startDate}|${entry.endDate}`
  );

  const normalizedEducation = dedupeByKey(
    readImportArray(source, ['education', 'educations', 'formation', 'formations']).map((entry: any) => {
      const startYear = normalizeYear(entry?.annee ?? entry?.startYear ?? entry?.dateDebut);
      const endYear = normalizeYear(entry?.anneeFin ?? entry?.endYear ?? entry?.dateFin);

      return {
        id: '',
        institution: readImportValue(entry, ['etablissement', 'institution', 'school']),
        degree: readImportValue(entry, ['diplome', 'degree', 'title']),
        city: readImportValue(entry, ['ville', 'city']),
        startYear: startYear || endYear || new Date().getFullYear().toString(),
        endYear,
        current: !endYear,
        specialization: readImportValue(entry, ['specialisation', 'specialization', 'fieldOfStudy']),
        mention: 'Passable' as const
      };
    }).filter((entry) => entry.institution || entry.degree),
    (entry) => `${entry.institution}|${entry.degree}|${entry.startYear}|${entry.endYear}`
  );

  const explicitLanguages = readImportArray(source, ['languages', 'langues', 'langue', 'language']).map((entry: any) => ({
    id: '',
    name: typeof entry === 'string' ? entry.trim() : readImportValue(entry, ['nom', 'name', 'language', 'langue']),
    level: typeof entry === 'string' ? 'B2' : (readImportValue(entry, ['niveau', 'level', 'proficiency']) || 'B2')
  })).filter((entry) => entry.name);

  const skillLikeEntries = readImportArray(source, ['skills', 'competences', 'compétences', 'competencies', 'competency', 'skill']);
  const normalizedSkills = skillLikeEntries.map((entry: any) => ({
    name: typeof entry === 'string' ? entry.trim() : readImportValue(entry, ['nom', 'name', 'skill']),
    rawType: typeof entry === 'string' ? '' : readImportValue(entry, ['typeCompetence', 'type', 'category']),
    level: typeof entry === 'string' ? 'B2' : (readImportValue(entry, ['niveau', 'level']) || 'B2')
  })).filter((entry) => entry.name);

  const inferredLanguages = normalizedSkills
    .filter((entry) => entry.rawType.toLowerCase().includes('lang') || entry.rawType.toLowerCase().includes('linguist') || isKnownLanguage(entry.name))
    .map((entry) => ({ id: '', name: entry.name, level: entry.level }));

  const languages = dedupeByKey(
    [...explicitLanguages, ...inferredLanguages],
    (entry) => entry.name
  );

  const skills = dedupeByKey(
    normalizedSkills
      .filter((entry) => !languages.some((language) => language.name.trim().toLowerCase() === entry.name.trim().toLowerCase()))
      .map((entry) => ({
        id: '',
        name: entry.name,
        category: entry.rawType || 'Technical'
      })),
    (entry) => entry.name
  );

  const projects = dedupeByKey(
    readImportArray(source, ['projects', 'projets']).map((entry: any) => ({
      id: '',
      title: readImportValue(entry, ['titre', 'title', 'name']),
      description: readImportValue(entry, ['description', 'summary']),
      stack: normalizeStack(entry?.technologies ?? entry?.technologiesUtilisees ?? entry?.stack),
      githubUrl: readImportValue(entry, ['lien', 'githubUrl', 'lienProjet', 'url']),
      demoUrl: readImportValue(entry, ['demoUrl', 'liveUrl']),
      imageUrl: readImportValue(entry, ['imageUrl']),
      isUniversity: Boolean(entry?.isUniversity ?? entry?.isAcademic),
      taches: normalizeTasks(entry?.taches ?? entry?.tasks ?? entry?.responsibilities)
    })).filter((entry) => entry.title || entry.description),
    (entry) => `${entry.title}|${entry.githubUrl}|${entry.demoUrl}`
  );

  const certifications = dedupeByKey(
    readImportArray(source, ['certifications', 'certification', 'certifs', 'licenses']).map((entry: any) => ({
      id: '',
      name: readImportValue(entry, ['titre', 'name', 'title']),
      issuer: readImportValue(entry, ['organisation', 'issuer', 'organisme']),
      date: normalizeMonth(entry?.date ?? entry?.dateObtention ?? entry?.issuedAt),
      verificationUrl: readImportValue(entry, ['lien', 'url', 'verificationUrl', 'credentialUrl'])
    })).filter((entry) => entry.name || entry.issuer),
    (entry) => `${entry.name}|${entry.issuer}|${entry.date}`
  );

  return {
    personal,
    resume,
    experience: normalizedExperience,
    extracurriculars: normalizedExtracurriculars,
    education: normalizedEducation,
    skills,
    languages,
    projects,
    certifications
  };
}

export function buildImportSummary(payload: ImportedProfilePayload): ProfileImportSummary {
  const personalFields = [
    payload.personal.firstName,
    payload.personal.lastName,
    payload.personal.email,
    payload.personal.phone,
    payload.personal.jobTitle,
    payload.personal.city,
    payload.personal.country,
    payload.personal.linkedinUrl,
    payload.personal.githubUrl
  ].filter(Boolean).length;

  return {
    personalFields,
    experienceCount: payload.experience.length,
    educationCount: payload.education.length,
    skillCount: payload.skills.length,
    languageCount: payload.languages.length,
    projectCount: payload.projects.length,
    certificationCount: payload.certifications.length,
    hasSummary: payload.resume.trim().length > 0
  };
}

export function hasImportedContent(payload: ImportedProfilePayload): boolean {
  const summary = buildImportSummary(payload);
  return (
    summary.personalFields > 0 ||
    summary.experienceCount > 0 ||
    summary.educationCount > 0 ||
    summary.skillCount > 0 ||
    summary.languageCount > 0 ||
    summary.projectCount > 0 ||
    summary.certificationCount > 0 ||
    summary.hasSummary
  );
}
