import { cleanText, extractCandidateName, firstArray, isGenericCandidateName, normalizeKey } from '../../utils/cv-json.utils';

/**
 * Normalises the CV edited in the resume editor into the shape the backend stores
 * (CV draft, PDF export, final save): cleans text, de-duplicates activities/projects
 * and resolves the candidate identity from the CV, the pipeline profile and the live profile.
 */

/** What the normaliser needs from the running pipeline / profile. */
export interface CvPayloadContext {
  pipelineResult: any;
  liveProfilePersonal: any;
  signedProfilePhotoUrl: string | null;
}

/**
 * Words that mark an experience as an extracurricular activity, matched as whole words in
 * its title and organisation only (a real job's bullets can mention clubs or trainings).
 * Same list as the agents' CV formatter.
 */
const ACTIVITY_WORDS = new Set([
  'hackathon', 'club', 'association', 'bde', 'benevole', 'benevolat', 'volunteer',
  'volunteering', 'volontariat', 'organisateur', 'organisatrice', 'organizer',
  'community', 'communaute',
]);

export function normalizeCvForBackend(data: any, context: CvPayloadContext): any {
  data = unwrapCvPayload(data);
  const { fontFamily, sections, ...rawWithoutFontFamily } = data ?? {};
  void fontFamily;
  void sections;
  const pipelineResult: any = context.pipelineResult;
  const profileSource = pipelineResult?.profileData?.data
    ?? pipelineResult?.profileData?.profile
    ?? pipelineResult?.profileData
    ?? {};
  const profilePersonal = profileSource?.personalInfo
    ?? profileSource?.personal_info
    ?? profileSource?.personal
    ?? {};
  const liveProfilePersonal = context.liveProfilePersonal ?? {};
  const candidate = getCandidateSource(data);
  const activities = normalizeActivities(firstArray(data, ['activities', 'extracurricular', 'activites', 'activités']));
  const highlightedSkills = new Set(
    firstArray(data, ['competences_mises_en_avant'])
      .map((skill: any) => normalizeKey(typeof skill === 'string' ? skill : skill?.name ?? skill?.nom ?? skill?.label))
      .filter(Boolean)
  );
  const experience = firstArray(data, ['experience', 'experiences', 'experiences_optimisees'])
    .map((exp: any) => ({
      role: cleanText(exp?.role ?? exp?.title ?? exp?.poste ?? exp?.titre),
      company: cleanText(exp?.company ?? exp?.entreprise),
      start: toMonthValue(exp?.start ?? exp?.dateDebut ?? exp?.date_debut),
      end: toMonthValue(exp?.end ?? exp?.dateFin ?? exp?.date_fin),
      bullets: dedupeStrings(asStringArray(exp?.bullets ?? exp?.taches_optimisees ?? exp?.missions ?? exp?.taches ?? exp?.description_optimisee ?? exp?.description)),
      relevance: cleanText(exp?.niveau_pertinence ?? exp?.relevance),
      keywords: dedupeStrings(asStringArray(exp?.mots_cles_cibles ?? exp?.keywords)),
    }))
    .filter((exp: any) => {
      if (!exp.role && !exp.company) return false;
      if (!looksLikeActivity(exp)) return true;
      activities.push({
        title: exp.role || exp.company,
        role: exp.company || null,
        description: exp.bullets[0] ?? '',
        startDate: exp.start ?? null,
        endDate: exp.end ?? null,
      });
      return false;
    });
  const rawProjects = firstArray(data, ['projects', 'projets', 'projets_optimises']);
  const normalizedSections = normalizeSections(data?.sections);
  const projects = mergeNormalizedProjects(
    normalizeProjects(rawProjects),
    extractProjectsFromSections(normalizedSections)
  );

  return {
    ...rawWithoutFontFamily,
    candidate: {
      name: resolvePreferredCandidateName(candidate, profilePersonal, liveProfilePersonal),
      email: cleanText(candidate?.email ?? candidate?.mail ?? profilePersonal?.email ?? profilePersonal?.mail ?? liveProfilePersonal?.email),
      phone: cleanText(candidate?.phone ?? candidate?.telephone ?? profilePersonal?.phone ?? profilePersonal?.telephone ?? liveProfilePersonal?.phone),
      location: resolvePreferredLocation(candidate, profilePersonal, liveProfilePersonal),
      title: cleanText(candidate?.title ?? candidate?.titrePoste ?? candidate?.poste ?? profilePersonal?.title ?? profilePersonal?.titrePoste ?? profilePersonal?.poste ?? liveProfilePersonal?.jobTitle),
      photoUrl: extractCandidatePhotoUrl(candidate, profilePersonal, liveProfilePersonal, context.signedProfilePhotoUrl),
      linkedIn: candidate?.linkedIn ?? candidate?.linkedin ?? candidate?.lienLinkedin ?? profilePersonal?.linkedIn ?? profilePersonal?.linkedin ?? profilePersonal?.lienLinkedin ?? liveProfilePersonal?.linkedinUrl ?? null,
      gitHub: candidate?.gitHub ?? candidate?.github ?? candidate?.lienGithub ?? profilePersonal?.gitHub ?? profilePersonal?.github ?? profilePersonal?.lienGithub ?? liveProfilePersonal?.githubUrl ?? null,
      portfolio: candidate?.portfolio ?? candidate?.lienPortfolio ?? profilePersonal?.portfolio ?? profilePersonal?.lienPortfolio ?? liveProfilePersonal?.portfolioUrl ?? null,
    },
    summary: cleanText(data?.summary ?? data?.resume ?? data?.resumeProfessionnel ?? candidate?.resumeProfessionnel),
    experience,
    education: firstArray(data, ['education', 'formations', 'formations_optimisees']).map((edu: any) => ({
      degree: cleanText(edu?.degree ?? edu?.diplome ?? edu?.titre),
      institution: cleanText(edu?.institution ?? edu?.etablissement ?? edu?.ecole),
      year: cleanText(edu?.year ?? edu?.annee ?? edu?.anneeFin ?? edu?.dateFin),
      startYear: cleanText(edu?.startYear ?? edu?.anneeDebut ?? edu?.dateDebut),
      endYear: cleanText(edu?.endYear ?? edu?.anneeFin ?? edu?.dateFin),
    })),
    skills: [
      ...firstArray(data, ['skills', 'technicalSkills', 'technical_skills', 'competences', 'competences_reordonnees']),
      ...firstArray(data, ['softSkills', 'soft_skills', 'softskills', 'competences_comportementales'])
        .map((skill: any) => ({
          ...((skill && typeof skill === 'object') ? skill : { name: skill }),
          category: skill?.category ?? skill?.categorie ?? skill?.typeCompetence ?? skill?.type_competence ?? 'Soft Skills',
          typeCompetence: skill?.typeCompetence ?? skill?.type_competence ?? skill?.category ?? skill?.categorie ?? 'Soft Skills',
        })),
    ].map((skill: any) => {
      const name = typeof skill === 'string' ? skill : cleanText(skill?.name ?? skill?.nom ?? skill?.label);
      return {
        name: cleanText(name),
        level: toSkillLevel(skill?.level ?? skill?.niveau ?? skill?.score ?? 3),
        isMatched: !!(skill?.isMatched ?? skill?.matched ?? skill?.statut === 'correspond'),
        isHighlighted: highlightedSkills.has(normalizeKey(name)),
        category: cleanText(skill?.category ?? skill?.categorie ?? skill?.typeCompetence),
        typeCompetence: cleanText(skill?.typeCompetence ?? skill?.type_competence),
      };
    }).filter((skill: any) => !!skill.name),
    projects,
    certifications: dedupeStrings(asStringArray(firstArray(data, ['certifications', 'certificats', 'certifications_optimisees']))),
    languages: dedupeStrings(asStringArray(firstArray(data, ['languages', 'langues']))),
    activities: dedupeActivities(activities),
    sections: normalizeSections(data?.sections, projects),
    atsScore: Number(data?.atsScore ?? data?.ats_score ?? 0),
    matchingScore: Number(data?.matchingScore ?? data?.matching_score ?? 0),
    atsCoveragePct: Number(data?.atsCoveragePct ?? data?.ats_coverage_pct ?? data?.atsScore ?? data?.ats_score ?? 0),
  };
}

function normalizeSections(value: any, normalizedProjects: any[] = []): any[] {
  const projectTechByTitle = new Map<string, string>();
  normalizedProjects.forEach((project) => {
    const key = normalizeKey(project?.title);
    const tech = dedupeStrings(asStringArray(project?.technologies)).join(', ');
    if (key && tech) {
      projectTechByTitle.set(key, tech);
    }
  });

  return asArray(value)
    .map((section: any, index: number) => {
      const normalizedId = normalizeSectionId(section?.id ?? section?.type);
      const sectionItems = asArray(section?.items);
      return {
      id: normalizedId || `section-${index + 1}`,
      type: normalizedId || cleanText(section?.type) || 'custom',
      title: cleanText(section?.title),
      placement: section?.placement === 'sidebar' ? 'sidebar' : 'main',
      isVisible: section?.isVisible !== false,
      order: Number.isFinite(Number(section?.order)) ? Number(section.order) : index,
      text: cleanText(section?.text) || null,
      items: sectionItems
        .map((item: any, itemIndex: number) => {
          const rawPrimaryText = stringifySectionText(item?.primaryText ?? item?.primary_text);
          const rawSecondaryText = stringifySectionText(item?.secondaryText ?? item?.secondary_text);
          const projectTechByName = normalizedId === 'projects'
            ? (projectTechByTitle.get(normalizeKey(rawPrimaryText)) ?? '')
            : '';
          const projectFallbackTech = normalizedId === 'projects'
            ? dedupeStrings(asStringArray(
                item?.technologies
                ?? item?.technologies_utilisees
                ?? item?.technologiesUtilisees
                ?? item?.technologiesUsed
                ?? item?.tech_stack
                ?? item?.techStack
                ?? item?.stack
                ?? projectTechByName
                ?? normalizedProjects[itemIndex]?.technologies
              )).join(', ')
            : '';
          return {
          primaryText: rawPrimaryText,
          secondaryText: rawSecondaryText || projectFallbackTech,
          startDate: cleanText(item?.startDate ?? item?.start_date) || null,
          endDate: cleanText(item?.endDate ?? item?.end_date) || null,
          location: cleanText(item?.location) || null,
          description: cleanText(item?.description) || null,
          level: item?.level == null ? null : Math.min(5, Math.max(1, Number(item.level))),
          isMatched: !!(item?.isMatched ?? item?.is_matched),
          bullets: dedupeStrings(asStringArray(item?.bullets ?? item?.bullet_points)),
        };
        })
        .filter((item: any) =>
          !!item.primaryText ||
          !!item.secondaryText ||
          !!item.description ||
          item.bullets.length > 0
        ),
    };
    })
    .filter((section: any) => !!section.title || !!section.text || section.items.length > 0);
}

function stringifySectionText(value: any): string {
  if (typeof value === 'string') return cleanText(value);
  if (typeof value === 'number' || typeof value === 'boolean') return String(value);
  if (value && typeof value === 'object') {
    return cleanText(
      value.name
      ?? value.label
      ?? value.title
      ?? value.value
      ?? value.primaryText
      ?? ''
    );
  }
  return '';
}

function normalizeActivities(value: any): any[] {
  return asArray(value)
    .map((activity: any) => {
      if (typeof activity === 'string') {
        return {
          title: cleanText(activity),
          role: null,
          description: '',
          startDate: null,
          endDate: null,
        };
      }
      return {
        title: cleanText(activity?.title ?? activity?.name),
        role: cleanText(activity?.role) || null,
        description: cleanText(activity?.description),
        startDate: toMonthValue(activity?.startDate ?? activity?.start ?? activity?.dateDebut ?? activity?.date_debut),
        endDate: toMonthValue(activity?.endDate ?? activity?.end ?? activity?.dateFin ?? activity?.date_fin),
      };
    })
    .filter((activity: any) => !!activity.title || !!activity.description);
}

function normalizeProjects(value: any): any[] {
  const seen = new Set<string>();
  return asArray(value)
    .map((project: any) => {
      const description = cleanText(project?.description ?? project?.description_optimisee);
      const bullets = dedupeStrings(asStringArray(project?.bullets ?? project?.taches_optimisees ?? project?.taches ?? project?.missions))
        .filter((bullet) => !isSameMeaning(bullet, description));
      return {
        title: cleanText(project?.title ?? project?.name ?? project?.titreProjet ?? project?.titre),
        description,
        technologies: dedupeStrings(asStringArray(
          project?.technologies
          ?? project?.technologies_utilisees
          ?? project?.technologiesUtilisees
          ?? project?.technologiesUsed
          ?? project?.stack
          ?? project?.techStack
          ?? project?.outils
        )),
        dateRealisation: toMonthValue(project?.dateRealisation ?? project?.date_realisation),
        relevance: cleanText(project?.niveau_pertinence ?? project?.relevance),
        keywords: dedupeStrings(asStringArray(project?.mots_cles_cibles ?? project?.keywords)),
        bullets,
      };
    })
    .filter((project: any) => {
      if (!project.title && !project.description && project.bullets.length === 0) return false;
      const key = normalizeKey(`${project.title}|${project.description ?? ''}`);
      if (seen.has(key)) return false;
      seen.add(key);
      return true;
    });
}

function extractProjectsFromSections(sections: any[]): any[] {
  return asArray(sections)
    .filter((section: any) => normalizeSectionId(section?.id ?? section?.type) === 'projects')
    .flatMap((section: any) => asArray(section?.items))
    .map((item: any) => ({
      title: cleanText(item?.primaryText ?? item?.primary_text),
      description: cleanText(item?.description),
      technologies: dedupeStrings(asStringArray(
        item?.secondaryText
        ?? item?.secondary_text
        ?? item?.technologies
        ?? item?.technologies_utilisees
        ?? item?.technologiesUsed
        ?? item?.tech_stack
        ?? item?.techStack
        ?? item?.stack
      )),
      dateRealisation: toMonthValue(item?.startDate ?? item?.start_date),
      relevance: '',
      keywords: [],
      bullets: dedupeStrings(asStringArray(item?.bullets ?? item?.bullet_points)),
    }))
    .filter((project: any) => !!project.title || !!project.description || project.bullets.length > 0);
}

function mergeNormalizedProjects(primary: any[], fallback: any[]): any[] {
  const merged = new Map<string, any>();

  for (const project of fallback) {
    const key = normalizeKey(project?.title);
    if (!key) continue;
    merged.set(key, project);
  }

  for (const project of primary) {
    const key = normalizeKey(project?.title);
    if (!key) continue;
    const existing = merged.get(key);
    merged.set(key, {
      ...existing,
      ...project,
      technologies: (project?.technologies?.length ? project.technologies : existing?.technologies) ?? [],
      bullets: (project?.bullets?.length ? project.bullets : existing?.bullets) ?? [],
      description: project?.description || existing?.description || '',
      dateRealisation: project?.dateRealisation || existing?.dateRealisation || '',
    });
  }

  return Array.from(merged.values());
}

export function unwrapCvPayload(value: any): any {
  if (!value || typeof value !== 'object') return value;
  return value.cvData
    ?? value.cv_data
    ?? value.cvGeneratedContent
    ?? value.cv_optimized_content
    ?? value.cvOptimizedContent
    ?? value.data
    ?? value;
}

export function getCandidateSource(data: any): any {
  return data?.candidate
    ?? data?.personal
    ?? data?.personalInfo
    ?? data?.personal_info
    ?? data?.informations_personnelles
    ?? {};
}

function resolvePreferredCandidateName(candidate: any, profilePersonal: any, liveProfilePersonal: any): string {
  const candidateName = extractCandidateName(candidate);
  const profileName = extractCandidateName(profilePersonal);
  const liveName = extractCandidateName(liveProfilePersonal);
  if (candidateName && !isGenericCandidateName(candidateName)) {
    return candidateName;
  }
  return profileName || liveName || candidateName;
}

function resolvePreferredLocation(candidate: any, profilePersonal: any, liveProfilePersonal: any): string {
  const candidateLocation = cleanText(
    candidate?.location ?? [candidate?.ville ?? candidate?.city, candidate?.pays ?? candidate?.country].filter(Boolean).join(', ')
  );
  const profileLocation = cleanText(
    profilePersonal?.location ?? [profilePersonal?.ville ?? profilePersonal?.city, profilePersonal?.pays ?? profilePersonal?.country].filter(Boolean).join(', ')
  );
  const liveLocation = cleanText(
    [liveProfilePersonal?.city, liveProfilePersonal?.country].filter(Boolean).join(', ')
  );
  return candidateLocation || profileLocation || liveLocation;
}

function extractCandidatePhotoUrl(candidate: any, profilePersonal: any, liveProfilePersonal: any, signedProfilePhotoUrl: string | null): string | null {
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
    liveProfilePersonal?.photoUrl,
    liveProfilePersonal?.photo_url,
    liveProfilePersonal?.profilePhoto,
    liveProfilePersonal?.profile_photo,
    liveProfilePersonal?.avatar,
    signedProfilePhotoUrl,
  ];

  for (const value of candidates) {
    const clean = cleanText(value);
    if (clean) return clean;
  }

  return null;
}

function normalizeSectionId(sectionId: any): string {
  const normalized = cleanText(sectionId).toLowerCase();
  const aliases: Record<string, string> = {
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

function dedupeActivities(activities: any[]): any[] {
  const seen = new Set<string>();
  return activities.filter((activity) => {
    const key = normalizeKey(`${activity.role ?? ''}|${activity.title ?? ''}`);
    if (!key || seen.has(key)) return false;
    seen.add(key);
    return true;
  });
}

function looksLikeActivity(exp: any): boolean {
  const words = normalizeKey(`${exp?.role ?? ''} ${exp?.company ?? ''}`).split(' ');
  return words.some(word => ACTIVITY_WORDS.has(word));
}

function isSameMeaning(left: string, right: string): boolean {
  const a = normalizeKey(left);
  const b = normalizeKey(right);
  if (!a || !b) return false;
  return a === b || a.includes(b) || b.includes(a);
}

function dedupeStrings(values: string[]): string[] {
  const seen = new Set<string>();
  return values.filter((value) => {
    const clean = cleanText(value);
    const key = normalizeKey(clean);
    if (!key || seen.has(key)) return false;
    seen.add(key);
    return true;
  });
}

function asArray(value: any): any[] {
  return Array.isArray(value) ? value : [];
}

function asStringArray(value: any): string[] {
  const values = Array.isArray(value) ? value : [value];
  return values.map((item: any) => {
    if (typeof item === 'string') return item.trim();
    if (typeof item === 'number' || typeof item === 'boolean') return String(item);
    if (item && typeof item === 'object')
      return cleanText(item.name ?? item.nom ?? item.label ?? item.title ?? item.titre ?? item.description ?? '');
    return String(item ?? '').trim();
  }).filter(Boolean);
}

function toSkillLevel(value: any): number {
  if (typeof value === 'number') return Math.min(5, Math.max(1, Math.round(value)));
  const normalized = normalizeKey(value);
  if (['expert', 'avance', 'advanced', 'proficient', 'native', 'maternelle'].includes(normalized)) return 5;
  if (['intermediaire', 'intermediate', 'courant', 'upper intermediate'].includes(normalized)) return 4;
  if (['elementaire', 'elementary', 'debutant', 'beginner'].includes(normalized)) return 2;
  return 3;
}

function toMonthValue(value: any): string {
  const text = String(value ?? '').trim();
  const match = text.match(/^(\d{4})-(\d{2})/);
  return match ? `${match[1]}-${match[2]}` : '';
}
