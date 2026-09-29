import { cleanText, extractCandidateName, firstArray, isGenericCandidateName, normalizeKey } from '../../utils/cv-json.utils';
import { Candidate, Experience, Education, Skill, Project, Activity, CvGeneratedSchema, CvSectionItem, CvSection } from './resume-editor.models';

/*
 * Turns the AI pipeline output (CV JSON in one of several shapes, plus the profile snapshot)
 * into the editor's CV data, merged over the current data. Pure functions, no Angular.
 */

export function mergeGeneratedCv(current: CvGeneratedSchema, generated: any): { data: CvGeneratedSchema; sections: unknown } | null {
  const normalized = generated?.cvData
    ?? generated?.cv_data
    ?? generated?.cvGeneratedContent
    ?? generated?.cv_optimized_content
    ?? generated?.cvOptimizedContent
    ?? generated;
  const profileSource = generated?.profileData?.data
    ?? generated?.profileData?.profile
    ?? generated?.profile_data?.data
    ?? generated?.profile_data?.profile
    ?? generated?.profileData
    ?? generated?.profile_data
    ?? {};
  if (!normalized || typeof normalized !== 'object') {
    console.warn('[CV-PIPELINE] ResumeEditor hydrate skipped: invalid payload', { generated });
    return null;
  }
  const profilePersonal = profileSource?.personalInfo
    ?? profileSource?.personal_info
    ?? profileSource?.personal
    ?? {};
  const candidate = normalized?.candidate
    ?? normalized?.personal
    ?? normalized?.personalInfo
    ?? normalized?.personal_info
    ?? profilePersonal
    ?? {};
  const experience = firstArray(normalized, ['experience', 'experiences', 'experiences_optimisees']);
  const sectionExperience = extractExperienceFromSections(normalized?.sections);
  const education = firstArray(normalized, ['education', 'formations', 'formations_optimisees']);
  const skills = collectHydratedSkills(normalized);
  const sectionSkills = extractSkillsFromSections(normalized?.sections);
  const projects = firstArray(normalized, ['projects', 'projets', 'projets_optimises']);
  const sectionProjects = extractProjectsFromSections(normalized?.sections);
  const certifications = firstArray(normalized, ['certifications', 'certificats', 'certifications_optimisees']);
  const languages = firstArray(normalized, ['languages', 'langues']);
  const activities = firstArray(normalized, ['activities', 'extracurricular', 'activites', 'activités']);
  const highlightedSkills = new Set(
    firstArray(normalized, ['competences_mises_en_avant'])
      .map((skill: any) => cleanText(typeof skill === 'string' ? skill : skill?.name ?? skill?.nom ?? skill?.label).toLowerCase())
      .filter(Boolean)
  );

  const data: CvGeneratedSchema = {
    ...current,
    candidate: {
      ...current.candidate,
      name: resolvePreferredCandidateName(candidate, profilePersonal, current.candidate.name),
      email: cleanText(candidate?.email ?? candidate?.mail ?? profilePersonal?.email ?? profilePersonal?.mail) || current.candidate.email,
      phone: cleanText(candidate?.phone ?? candidate?.telephone ?? profilePersonal?.phone ?? profilePersonal?.telephone) || current.candidate.phone,
      location: resolvePreferredLocation(candidate, profilePersonal, current.candidate.location),
      title: cleanText(candidate?.title ?? candidate?.titrePoste ?? candidate?.poste ?? profilePersonal?.title ?? profilePersonal?.titrePoste ?? profilePersonal?.poste) || current.candidate.title,
      photoUrl: extractCandidatePhotoUrl(candidate, profilePersonal, profileSource, generated) ?? current.candidate.photoUrl,
      linkedIn: candidate?.linkedIn ?? candidate?.linkedin ?? candidate?.lienLinkedin ?? profilePersonal?.linkedIn ?? profilePersonal?.linkedin ?? profilePersonal?.lienLinkedin ?? current.candidate.linkedIn,
      gitHub: candidate?.gitHub ?? candidate?.github ?? candidate?.lienGithub ?? profilePersonal?.gitHub ?? profilePersonal?.github ?? profilePersonal?.lienGithub ?? current.candidate.gitHub,
      portfolio: candidate?.portfolio ?? candidate?.lienPortfolio ?? profilePersonal?.portfolio ?? profilePersonal?.lienPortfolio ?? current.candidate.portfolio,
    },
    summary: normalized?.summary ?? normalized?.resume ?? normalized?.resumeProfessionnel ?? current.summary,
    experience: mergeHydratedExperience(
      experience.length > 0 ? experience.map((e: any) => ({
        role: cleanText(e?.role ?? e?.title ?? e?.poste ?? e?.titre),
        company: cleanText(e?.company ?? e?.entreprise),
        start: toMonthValue(e?.start ?? e?.dateDebut ?? e?.date_debut),
        end: toMonthValue(e?.end ?? e?.dateFin ?? e?.date_fin),
        bullets: asStringArray(e?.bullets ?? e?.taches_optimisees ?? e?.missions ?? e?.taches ?? e?.description_optimisee ?? e?.description),
        relevance: cleanText(e?.niveau_pertinence ?? e?.relevance),
        keywords: asStringArray(e?.mots_cles_cibles ?? e?.keywords),
      })) : [],
      sectionExperience,
      projects.length > 0
        ? projects.map((project: any) => cleanText(project?.title ?? project?.titreProjet ?? project?.titre))
        : sectionProjects.map((project) => cleanText(project?.title)),
      current.experience
    ),
    education: education.length > 0
      ? education.map((education: any) => ({
          degree: String(education?.degree ?? education?.diplome ?? education?.titre ?? ''),
          institution: String(education?.institution ?? education?.etablissement ?? education?.ecole ?? ''),
          year: String(education?.year ?? education?.annee ?? education?.anneeFin ?? education?.dateFin ?? ''),
          startYear: String(education?.startYear ?? education?.anneeDebut ?? education?.dateDebut ?? ''),
          endYear: String(education?.endYear ?? education?.anneeFin ?? education?.dateFin ?? ''),
        }))
      : current.education,
    skills: mergeHydratedSkills(
      skills.length > 0 ? skills.map((skill: any) => {
        const name = typeof skill === 'string' ? skill : cleanText(skill?.name ?? skill?.nom ?? skill?.label);
        return {
          name: cleanText(name),
          level: Math.min(5, Math.max(1, Number(skill?.level ?? skill?.niveau ?? 3))),
          isMatched: !!(skill?.isMatched ?? skill?.matched ?? skill?.statut === 'correspond'),
          isHighlighted: highlightedSkills.has(normalizeKey(name)),
          category: cleanText(skill?.category ?? skill?.categorie ?? skill?.typeCompetence ?? skill?.type_competence),
          typeCompetence: cleanText(skill?.typeCompetence ?? skill?.type_competence ?? skill?.category ?? skill?.categorie),
        };
      }).filter((skill: Skill) => !!skill.name) : [],
      sectionSkills,
      current.skills
    ),
    projects: mergeHydratedProjects(
      projects.length > 0
        ? projects.map((project: any) => ({
            title: String(project?.title ?? project?.titreProjet ?? project?.titre ?? ''),
            description: String(project?.description ?? project?.description_optimisee ?? ''),
            technologies: extractProjectTechnologies(project),
            dateRealisation: toMonthValue(project?.dateRealisation ?? project?.date_realisation),
            relevance: cleanText(project?.niveau_pertinence ?? project?.relevance),
            keywords: asStringArray(project?.mots_cles_cibles ?? project?.keywords),
            bullets: asStringArray(
              project?.bullets
              ?? project?.taches_optimisees
              ?? project?.taches
              ?? project?.missions
              ?? project?.tasks
              ?? project?.responsibilities
              ?? project?.realisations
              ?? project?.description_optimisee
              ?? project?.description
            ),
          }))
        : [],
      sectionProjects,
      current.projects
    ),
    certifications: certifications.length > 0 ? asStringArray(certifications) : current.certifications,
    languages: languages.length > 0 ? asStringArray(languages) : current.languages,
    activities: activities.length > 0
      ? activities.map((activity: any) => ({
          title: cleanText(activity?.title ?? activity?.name ?? activity),
          role: cleanText(activity?.role ?? activity?.secondaryText) || null,
          description: cleanText(activity?.description) || null,
          startDate: toMonthValue(activity?.startDate ?? activity?.start ?? activity?.dateDebut ?? activity?.date_debut),
          endDate: toMonthValue(activity?.endDate ?? activity?.end ?? activity?.dateFin ?? activity?.date_fin),
        })).filter((activity: Activity) => !!activity.title || !!activity.description)
      : current.activities,
    accomplishments: Array.isArray(normalized?.accomplishments) ? normalized.accomplishments : current.accomplishments,
    atsScore: normalized?.atsScore ?? normalized?.ats_score ?? current.atsScore,
    matchingScore: normalized?.matchingScore ?? normalized?.matching_score ?? current.matchingScore,
    atsCoveragePct: normalized?.atsCoveragePct ?? normalized?.ats_coverage_pct ?? current.atsCoveragePct,
  };

  return { data, sections: normalized?.sections };

}

function toMonthValue(value: any): string {
  if (typeof value !== 'string') return '';
  const v = value.trim();
  if (!v) return '';
  if (v.toLowerCase() === 'present' || v.toLowerCase() === 'présent') return '';
  const m = v.match(/^(\d{4})-(\d{2})/);
  if (m) return `${m[1]}-${m[2]}`;
  if (/^\d{4}-\d{2}$/.test(v)) return v;
  return '';
}


function asStringArray(value: any): string[] {
  const values = Array.isArray(value) ? value : [value];
  return values.map((item: any) => {
    if (typeof item === 'string') return item.trim();
    if (typeof item === 'number' || typeof item === 'boolean') return String(item);
    if (item && typeof item === 'object') {
      return cleanText(item.name ?? item.nom ?? item.label ?? item.title ?? item.titre ?? item.description ?? '');
    }
    return String(item ?? '').trim();
  }).filter(Boolean);
}

function extractProjectTechnologies(project: any): string[] {
  const raw =
    project?.technologies
    ?? project?.technologies_utilisees
    ?? project?.technologiesUtilisees
    ?? project?.technologiesUsed
    ?? project?.stack
    ?? project?.techStack
    ?? project?.outils;

  if (Array.isArray(raw)) {
    return raw
      .map((technology: any) => cleanText(technology))
      .filter(Boolean);
  }

  const text = cleanText(raw);
  if (!text) {
    return [];
  }

  return text
    .split(/[;,|]/g)
    .map((technology) => technology.trim())
    .filter(Boolean);
}

function extractProjectsFromSections(sections: any): Project[] {
  if (!Array.isArray(sections)) {
    return [];
  }

  return sections
    .filter((section: any) => normalizeSectionId(section?.id ?? section?.type) === 'projects')
    .flatMap((section: any) => Array.isArray(section?.items) ? section.items : [])
    .map((item: any) => ({
      title: cleanText(item?.primaryText ?? item?.primary_text),
      description: cleanText(item?.description),
      technologies: asStringArray(
        item?.secondaryText
        ?? item?.secondary_text
        ?? item?.technologies
        ?? item?.technologies_utilisees
        ?? item?.technologiesUsed
        ?? item?.tech_stack
        ?? item?.techStack
        ?? item?.stack
      ),
      dateRealisation: toMonthValue(item?.startDate ?? item?.start_date),
      bullets: asStringArray(item?.bullets ?? item?.bullet_points),
      relevance: '',
      keywords: [],
    }))
    .filter((project) => !!project.title || !!project.description || project.bullets.length > 0);
}

function extractExperienceFromSections(sections: any): Experience[] {
  if (!Array.isArray(sections)) {
    return [];
  }

  return sections
    .filter((section: any) => normalizeSectionId(section?.id ?? section?.type) === 'experience')
    .flatMap((section: any) => Array.isArray(section?.items) ? section.items : [])
    .map((item: any) => ({
      role: cleanText(item?.primaryText ?? item?.primary_text),
      company: cleanText(item?.secondaryText ?? item?.secondary_text),
      start: toMonthValue(item?.startDate ?? item?.start_date),
      end: toMonthValue(item?.endDate ?? item?.end_date),
      bullets: asStringArray(item?.bullets ?? item?.bullet_points),
      relevance: '',
      keywords: [],
    }))
    .filter((item) => !!item.role || !!item.company || item.bullets.length > 0);
}

function extractSkillsFromSections(sections: any): Skill[] {
  if (!Array.isArray(sections)) {
    return [];
  }

  return sections
    .filter((section: any) => {
      const normalizedId = normalizeSectionId(section?.id ?? section?.type);
      return normalizedId === 'skills' || normalizedId === 'softskills';
    })
    .flatMap((section: any) => {
      const normalizedId = normalizeSectionId(section?.id ?? section?.type);
      const category = normalizedId === 'softskills' ? 'Soft Skills' : 'Technical';
      return (Array.isArray(section?.items) ? section.items : []).map((item: any) => ({
        name: cleanText(item?.primaryText ?? item?.primary_text),
        level: Math.min(5, Math.max(1, Number(item?.level ?? 3))),
        isMatched: !!(item?.isMatched ?? item?.is_matched),
        category,
        typeCompetence: category,
      }));
    })
    .filter((item) => !!item.name);
}

function collectHydratedSkills(source: any): any[] {
  const technical = firstArray(source, [
    'skills',
    'technicalSkills',
    'technical_skills',
    'competences',
    'competences_reordonnees',
    'competences_mises_en_avant',
  ]).map((skill: any) => ({
    ...((skill && typeof skill === 'object') ? skill : { name: skill }),
    category: skill?.category ?? skill?.categorie ?? skill?.typeCompetence ?? skill?.type_competence ?? 'Technical',
    typeCompetence: skill?.typeCompetence ?? skill?.type_competence ?? skill?.category ?? skill?.categorie ?? 'Technical',
  }));

  const soft = firstArray(source, [
    'softSkills',
    'soft_skills',
    'softskills',
    'competences_comportementales',
    'soft_skills_reordonnees',
  ]).map((skill: any) => ({
    ...((skill && typeof skill === 'object') ? skill : { name: skill }),
    category: skill?.category ?? skill?.categorie ?? skill?.typeCompetence ?? skill?.type_competence ?? 'Soft Skills',
    typeCompetence: skill?.typeCompetence ?? skill?.type_competence ?? skill?.category ?? skill?.categorie ?? 'Soft Skills',
  }));

  return [...technical, ...soft];
}

function mergeHydratedExperience(
  primary: Experience[],
  fallback: Experience[],
  knownProjectTitles: string[],
  current: Experience[]
): Experience[] {
  const projectTitleSet = new Set(
    knownProjectTitles
      .map((title) => normalizeKey(title))
      .filter(Boolean)
  );
  const source = [...fallback, ...primary];
  const incoming = new Map<string, Experience>();

  for (const rawItem of source) {
    const item = normalizeExperienceEntry(rawItem);
    if (!item) {
      continue;
    }

    if (looksLikeProjectEntry(item, projectTitleSet)) {
      continue;
    }

    const key = getExperienceMergeKey(item);
    const existing = incoming.get(key);
    incoming.set(key, existing ? mergeExperienceEntry(existing, item) : item);
  }

  const values = [...incoming.values()];
  return values.length > 0 ? values : current;
}

function mergeHydratedSkills(primary: Skill[], fallback: Skill[], current: Skill[]): Skill[] {
  const incoming = [...fallback, ...primary].filter((item) => !!cleanText(item?.name));
  if (incoming.length === 0) {
    return current;
  }

  const merged = new Map<string, Skill>();
  for (const skill of incoming) {
    const sectionKey = isSoftSkillCategory(skill?.category ?? skill?.typeCompetence) ? 'softskills' : 'skills';
    const key = `${sectionKey}::${normalizeKey(skill?.name)}`;
    const existing = merged.get(key);
    merged.set(key, {
      ...existing,
      ...skill,
      name: cleanText(skill?.name) || existing?.name || '',
      level: Math.min(5, Math.max(1, Number(skill?.level ?? existing?.level ?? 3))),
      isMatched: !!(skill?.isMatched ?? existing?.isMatched),
      category: cleanText(skill?.category ?? skill?.typeCompetence) || existing?.category || (sectionKey === 'softskills' ? 'Soft Skills' : 'Technical'),
      typeCompetence: cleanText(skill?.typeCompetence ?? skill?.category) || existing?.typeCompetence || (sectionKey === 'softskills' ? 'Soft Skills' : 'Technical'),
    });
  }

  return [...merged.values()];
}

function mergeHydratedProjects(primary: Project[], fallback: Project[], current: Project[]): Project[] {
  const incoming = [...fallback, ...primary].filter((project) =>
    !!cleanText(project?.title) || !!cleanText(project?.description) || (project?.bullets?.length ?? 0) > 0
  );
  const sourceProjects = incoming.length > 0 ? incoming : current;
  const merged = new Map<string, Project>();

  for (const project of sourceProjects) {
    const key = cleanText(project?.title).toLowerCase();
    if (!key) {
      continue;
    }

    const existing = merged.get(key);
    merged.set(key, {
      ...existing,
      ...project,
      title: project.title || existing?.title || '',
      description: project.description || existing?.description,
      technologies: project.technologies?.length ? project.technologies : (existing?.technologies ?? []),
      dateRealisation: project.dateRealisation || existing?.dateRealisation,
      bullets: project.bullets?.length ? project.bullets : (existing?.bullets ?? []),
      relevance: project.relevance || existing?.relevance,
      keywords: project.keywords?.length ? project.keywords : (existing?.keywords ?? []),
    });
  }

  return Array.from(merged.values());
}

function normalizeExperienceEntry(item: Experience | null | undefined): Experience | null {
  const role = cleanText(item?.role);
  const company = cleanText(item?.company);
  const start = toMonthValue(item?.start);
  const end = toMonthValue(item?.end);
  const bullets = dedupeStrings(asStringArray(item?.bullets));
  const relevance = cleanText(item?.relevance);
  const keywords = dedupeStrings(asStringArray(item?.keywords));

  if (!role && !company && bullets.length === 0) {
    return null;
  }

  return {
    role,
    company,
    start,
    end,
    bullets,
    relevance,
    keywords,
  };
}

function looksLikeProjectEntry(item: Experience, projectTitleSet: Set<string>): boolean {
  const roleKey = normalizeKey(item.role);
  if (!roleKey || !projectTitleSet.has(roleKey)) {
    return false;
  }

  const hasCompany = !!cleanText(item.company);
  const hasDates = !!cleanText(item.start) || !!cleanText(item.end);
  return !hasCompany && !hasDates;
}

function getExperienceMergeKey(item: Experience): string {
  const role = normalizeKey(item.role);
  const company = normalizeKey(item.company);
  const start = cleanText(item.start).toLowerCase();
  const end = cleanText(item.end).toLowerCase();
  const bullets = dedupeStrings(item.bullets)
    .map((bullet: string) => normalizeKey(bullet))
    .filter(Boolean)
    .join('|');

  return [role, company, start, end, bullets].join('::');
}

function mergeExperienceEntry(existing: Experience, incoming: Experience): Experience {
  return {
    role: incoming.role || existing.role,
    company: incoming.company || existing.company,
    start: incoming.start || existing.start,
    end: incoming.end || existing.end,
    bullets: dedupeStrings([...(existing.bullets ?? []), ...(incoming.bullets ?? [])]),
    relevance: incoming.relevance || existing.relevance,
    keywords: dedupeStrings([...(existing.keywords ?? []), ...(incoming.keywords ?? [])]),
  };
}

function dedupeStrings(values: string[]): string[] {
  const seen = new Set<string>();
  return values.filter((value) => {
    const clean = cleanText(value);
    const key = normalizeKey(clean);
    if (!key || seen.has(key)) {
      return false;
    }

    seen.add(key);
    return true;
  });
}


export function isSoftSkillCategory(value: any): boolean {
  const normalized = normalizeKey(value);
  return normalized === 'soft'
    || normalized === 'soft skill'
    || normalized === 'soft skills'
    || normalized === 'comportemental'
    || normalized === 'behavioral'
    || normalized === 'behavioural';
}


function resolvePreferredCandidateName(candidate: any, profilePersonal: any, fallback: string): string {
  const candidateName = extractCandidateName(candidate);
  const profileName = extractCandidateName(profilePersonal);
  if (candidateName && !isGenericCandidateName(candidateName)) {
    return candidateName;
  }
  if (profileName) {
    return profileName;
  }
  return candidateName || fallback;
}

function resolvePreferredLocation(candidate: any, profilePersonal: any, fallback: string): string {
  const candidateLocation = cleanText(
    candidate?.location ?? [candidate?.ville ?? candidate?.city, candidate?.pays ?? candidate?.country].filter(Boolean).join(', ')
  );
  const profileLocation = cleanText(
    profilePersonal?.location ?? [profilePersonal?.ville ?? profilePersonal?.city, profilePersonal?.pays ?? profilePersonal?.country].filter(Boolean).join(', ')
  );
  return candidateLocation || profileLocation || fallback;
}

function extractCandidatePhotoUrl(candidate: any, profilePersonal: any, profileSource?: any, generated?: any): string | null {
  const candidates = [
    candidate?.photoUrl,
    candidate?.photo_url,
    candidate?.profilePhoto,
    candidate?.profile_photo,
    candidate?.avatar,
    profilePersonal?.photoUrl,
    profilePersonal?.photo_url,
    profilePersonal?.profilePhoto,
    profilePersonal?.profile_photo,
    profilePersonal?.avatar,
    profileSource?.photoUrl,
    profileSource?.photo_url,
    profileSource?.profilePhoto,
    profileSource?.profile_photo,
    profileSource?.avatar,
    generated?.profileData?.photoUrl,
    generated?.profileData?.photo_url,
    generated?.profileData?.profilePhoto,
    generated?.profileData?.profile_photo,
  ];

  for (const value of candidates) {
    const clean = cleanText(value);
    if (clean) return clean;
  }

  return null;
}


export function normalizeSectionId(sectionId: string | null | undefined): string {
  const normalized = cleanText(sectionId).toLowerCase();
  const aliases: Record<string, string> = {
    header: 'header',
    summary: 'summary',
    resume: 'summary',
    experience: 'experience',
    experiences: 'experience',
    project: 'projects',
    projects: 'projects',
    education: 'education',
    skill: 'skills',
    skills: 'skills',
    softskills: 'softskills',
    'soft-skills': 'softskills',
    soft_skills: 'softskills',
    certification: 'certifications',
    certifications: 'certifications',
    language: 'languages',
    languages: 'languages',
    activity: 'activities',
    activities: 'activities',
    achievement: 'accomplishments',
    achievements: 'accomplishments',
    accomplishments: 'accomplishments',
  };
  return aliases[normalized] ?? normalized;
}
