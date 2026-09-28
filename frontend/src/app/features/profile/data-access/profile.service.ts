import { Injectable, signal, computed, inject, effect } from '@angular/core';
import { Profile, ProfileStepId, PersonalInfo, Experience, Education, Skill, Language, Project, Certification, ProfileImportSummary } from './profile.models';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '@core/auth/auth.service';
import { extractApiError } from '@core/http/extract-api-error';
import { ProfileApiService } from './profile-api.service';
import { KeywordDto } from './profile-api.models';
import {
  toCertificationDto,
  toExperienceDto,
  toFormationDto,
  toLanguageDto,
  toPersonalInfoDto,
  toProfile,
  toProjetDto,
  toSkillDto,
} from './profile.mapper';
import {
  ImportedProfilePayload,
  buildImportSummary,
  hasImportedContent,
  normalizeImportedPayload,
} from './profile-import.normalizer';

/** A language as edited in the UI or imported (level as written by the user). */
type LanguageInput = { id?: string; name: string; level: string };

/**
 * The user's profile as UI state (signals), plus its edits and the CV / LinkedIn import.
 * During onboarding, edits stay local (session storage) and are flushed at the end.
 * HTTP goes through ProfileApiService; DTO <-> UI mapping lives in profile.mapper.
 */
@Injectable({
  providedIn: 'root'
})
export class ProfileService {
  private readonly api = inject(ProfileApiService);
  private readonly authService = inject(AuthService);
  private readonly SESSION_KEY = 'nextstep_onboarding_profile';
  private profilePhotoObjectUrl: string | null = null;

  isOnboarding = signal(false);

  // Initial Empty State
  private readonly emptyProfile: Profile = {
    personal: {
      firstName: '', lastName: '', email: '', phone: '',
      jobTitle: '', address: '', city: '', country: '',
      linkedinUrl: '', githubUrl: '', portfolioUrl: '', photoUrl: null,
      useAsHeadline: true
    },
    education: [], experience: [], skills: [], languages: [],
    resume: '', projets: [], certifications: [],
    sectionTitles: {
      formation: 'Education',
      experience: 'Work Experience',
      competences: 'Skills',
      projets: 'Personal Projects',
      certifications: 'Certifications',
      extraCurricular: 'Extracurricular Activities',
      languages: 'Languages'
    }
  };

  profile = signal<Profile>(this.emptyProfile);
  currentStep = signal<ProfileStepId>('coordonnees');

  constructor() {
    effect(() => {
      const user = this.authService.user();
      if (user) {
        this.refreshProfile();
      }
    });

    effect(() => {
      if (this.isOnboarding()) {
        sessionStorage.setItem(this.SESSION_KEY, JSON.stringify(this.profile()));
      }
    });
  }

  private saveToSessionStorage() {
    sessionStorage.setItem(this.SESSION_KEY, JSON.stringify(this.profile()));
  }

  loadFromSessionStorage(): Profile | null {
    const data = sessionStorage.getItem(this.SESSION_KEY);
    return data ? JSON.parse(data) : null;
  }

  clearSessionStorage() {
    sessionStorage.removeItem(this.SESSION_KEY);
  }

  // Méthode publique pour forcer le rechargement
  async refreshProfile() {
    return this.loadProfile();
  }

  async loadProfile() {
    try {
      const data = await firstValueFrom(this.api.getFullProfile());

      if (!data || !data.personalInfo) {
        console.warn('Données de profil incomplètes reçues du serveur');
        return;
      }

      this.profile.set(toProfile(data, this.authService.user(), this.emptyProfile.sectionTitles));

      const profilePhotoUrl = data.personalInfo.photoUrl ? await this.loadProfilePhotoObjectUrl() : null;
      this.profile.update(profile => ({
        ...profile,
        personal: {
          ...profile.personal,
          photoUrl: profilePhotoUrl,
        }
      }));
    } catch (error) {
      console.error('Erreur chargement profil:', error);
    }
  }

  async flushOnboardingData() {
    const data = this.loadFromSessionStorage();
    if (!data) return;

    this.isOnboarding.set(false);

    await this.savePersonalInfo(data.personal);
    for (const exp of data.experience) { await this.addExperience({ ...exp, id: '' }, false); }
    for (const edu of data.education) { await this.addEducation({ ...edu, id: '' }, false); }
    for (const skill of data.skills) { await this.addSkill({ ...skill, id: '' }, false); }
    for (const lang of data.languages) { await this.addLanguage({ ...lang, id: '' }, false); }
    for (const proj of data.projets) { await this.addProject({ ...proj, id: '' }, false); }
    for (const cert of data.certifications) { await this.addCertification({ ...cert, id: '' }, false); }

    this.clearSessionStorage();
    await this.loadProfile();
  }

  async savePersonalInfo(info: PersonalInfo) {
    if (this.isOnboarding()) return;
    return firstValueFrom(this.api.updatePersonalInfo(toPersonalInfoDto(info, this.profile().resume, this.profile().sectionTitles)));
  }

  async uploadProfilePhoto(file: File): Promise<string> {
    const response = await firstValueFrom(this.api.uploadPhoto(file));

    if (!response?.photoUrl) {
      throw new Error('Profile photo upload succeeded but no photo URL was returned.');
    }

    const photoUrl = await this.loadProfilePhotoObjectUrl();
    if (!photoUrl) {
      throw new Error('Profile photo upload succeeded but the image could not be loaded.');
    }

    this.profile.update(profile => ({
      ...profile,
      personal: {
        ...profile.personal,
        photoUrl,
      }
    }));

    return photoUrl;
  }

  private async loadProfilePhotoObjectUrl(): Promise<string | null> {
    try {
      const blob = await firstValueFrom(this.api.getPhoto());

      if (!blob.size) return null;

      if (this.profilePhotoObjectUrl) {
        URL.revokeObjectURL(this.profilePhotoObjectUrl);
      }

      this.profilePhotoObjectUrl = URL.createObjectURL(blob);
      return this.profilePhotoObjectUrl;
    } catch (error) {
      console.warn('Unable to load profile photo', error);
      return null;
    }
  }

  async getSignedProfilePhotoUrl(): Promise<string | null> {
    try {
      const response = await firstValueFrom(this.api.getSignedPhotoUrl());
      return response?.photoUrl ? await this.loadProfilePhotoObjectUrl() : null;
    } catch (error) {
      console.warn('Unable to get signed profile photo URL', error);
      return null;
    }
  }

  async addExperience(exp: Experience, refresh = true) {
    if (this.isOnboarding()) {
      this.profile.update(p => ({ ...p, experience: [...p.experience, { ...exp, id: exp.id || crypto.randomUUID() }] }));
      return;
    }
    await firstValueFrom(this.api.addExperience(toExperienceDto(exp, false)));
    if (refresh) await this.loadProfile();
  }

  async updateExperience(exp: Experience) {
    if (this.isOnboarding()) {
      this.profile.update(p => ({ ...p, experience: p.experience.map(e => e.id === exp.id ? exp : e) }));
      return;
    }
    await firstValueFrom(this.api.updateExperience(toExperienceDto(exp, true)));
    await this.loadProfile();
  }


  async deleteExperience(id: string) {
    if (this.isOnboarding()) {
      this.profile.update(p => ({ ...p, experience: p.experience.filter(e => e.id !== id) }));
      return;
    }
    await firstValueFrom(this.api.deleteExperience(id));
    await this.loadProfile();
  }

  async addEducation(edu: Education, refresh = true) {
    if (this.isOnboarding()) {
      this.profile.update(p => ({ ...p, education: [...p.education, { ...edu, id: edu.id || crypto.randomUUID() }] }));
      return;
    }
    await firstValueFrom(this.api.addEducation(toFormationDto(edu, false)));
    if (refresh) await this.loadProfile();
  }

  async updateEducation(edu: Education) {
    if (this.isOnboarding()) {
      this.profile.update(p => ({ ...p, education: p.education.map(e => e.id === edu.id ? edu : e) }));
      return;
    }
    await firstValueFrom(this.api.updateEducation(toFormationDto(edu, true)));
    await this.loadProfile();
  }


  async deleteEducation(id: string) {
    if (this.isOnboarding()) {
      this.profile.update(p => ({ ...p, education: p.education.filter(e => e.id !== id) }));
      return;
    }
    await firstValueFrom(this.api.deleteEducation(id));
    await this.loadProfile();
  }

  async addSkill(skill: Skill, refresh = true) {
    if (this.isOnboarding()) {
      this.profile.update(p => ({ ...p, skills: [...p.skills, { ...skill, id: skill.id || crypto.randomUUID() }] }));
      return;
    }
    await firstValueFrom(this.api.addSkill(toSkillDto(skill, false)));
    if (refresh) await this.loadProfile();
  }

  isSkillSelected(skillName: string): boolean {
    if (!this.profile().skills || !skillName) return false;
    return this.profile().skills.some(s => s.name?.toLowerCase() === skillName.toLowerCase());
  }

  async updateSkill(skill: Skill) {
    if (this.isOnboarding()) {
      this.profile.update(p => ({ ...p, skills: p.skills.map(s => s.id === skill.id ? skill : s) }));
      return;
    }
    await firstValueFrom(this.api.updateSkill(toSkillDto(skill, true)));
    await this.loadProfile();
  }

  async deleteSkill(id: string) {
    if (this.isOnboarding()) {
      this.profile.update(p => ({ ...p, skills: p.skills.filter(s => s.id !== id) }));
      return;
    }
    await firstValueFrom(this.api.deleteSkill(id));
    await this.loadProfile();
  }

  // --- Languages (stored as competences) ---

  async addLanguage(lang: LanguageInput, refresh = true) {
    if (this.isOnboarding()) {
      this.profile.update(p => ({ ...p, languages: [...p.languages, { ...lang, id: lang.id || crypto.randomUUID() } as Language] }));
      return;
    }
    await firstValueFrom(this.api.addSkill(toLanguageDto(lang, false)));
    if (refresh) await this.loadProfile();
  }

  async updateLanguage(lang: LanguageInput) {
    if (this.isOnboarding()) {
      this.profile.update(p => ({ ...p, languages: p.languages.map(l => l.id === lang.id ? lang as Language : l) }));
      return;
    }
    await firstValueFrom(this.api.updateSkill(toLanguageDto(lang, true)));
    await this.loadProfile();
  }

  async deleteLanguage(id: string) {
    if (this.isOnboarding()) {
      this.profile.update(p => ({ ...p, languages: p.languages.filter(l => l.id !== id) }));
      return;
    }
    await firstValueFrom(this.api.deleteSkill(id));
    await this.loadProfile();
  }

  async addProject(p: Project, refresh = true) {
    if (this.isOnboarding()) {
      this.profile.update(profile => ({ ...profile, projets: [...profile.projets, { ...p, id: p.id || crypto.randomUUID() }] }));
      return;
    }
    await firstValueFrom(this.api.addProject(toProjetDto(p, false)));
    if (refresh) await this.loadProfile();
  }

  async updateProject(p: Project) {
    if (this.isOnboarding()) {
      this.profile.update(profile => ({ ...profile, projets: profile.projets.map(pr => pr.id === p.id ? p : pr) }));
      return;
    }
    await firstValueFrom(this.api.updateProject(toProjetDto(p, true)));
    await this.loadProfile();
  }


  async deleteProject(id: string) {
    if (this.isOnboarding()) {
      this.profile.update(p => ({ ...p, projets: p.projets.filter(pr => pr.id !== id) }));
      return;
    }
    await firstValueFrom(this.api.deleteProject(id));
    await this.loadProfile();
  }

  async addCertification(c: Certification, refresh = true) {
    if (this.isOnboarding()) {
      this.profile.update(p => ({ ...p, certifications: [...p.certifications, { ...c, id: c.id || crypto.randomUUID() }] }));
      return;
    }
    await firstValueFrom(this.api.addCertification(toCertificationDto(c, false)));
    if (refresh) await this.loadProfile();
  }

  async updateCertification(c: Certification) {
    if (this.isOnboarding()) {
      this.profile.update(p => ({ ...p, certifications: p.certifications.map(cert => cert.id === c.id ? c : cert) }));
      return;
    }
    await firstValueFrom(this.api.updateCertification(toCertificationDto(c, true)));
    await this.loadProfile();
  }


  async deleteCertification(id: string) {
    if (this.isOnboarding()) {
      this.profile.update(p => ({ ...p, certifications: p.certifications.filter(c => c.id !== id) }));
      return;
    }
    await firstValueFrom(this.api.deleteCertification(id));
    await this.loadProfile();
  }

  // --- Nouveaux appels backend ---

  async getKeywords(): Promise<KeywordDto[]> {
    try {
      return await firstValueFrom(this.api.getKeywords());
    } catch (e) {
      console.error('Erreur chargement keywords', e);
      return [];
    }
  }

  async generateResume(profileData: unknown): Promise<string> {
    try {
      const res = await firstValueFrom(this.api.generateResume(profileData));
      return res.resume || '';
    } catch (e) {
      console.error('Erreur génération CV IA', e);
      return 'Generation error.';
    }
  }

  /**
   * Generates a clean, structured JSON representation of the current profile.
   */
  exportProfileJson(): { filename: string; jsonContent: string; data: any } {
    const p = this.profile();
    const data = {
      metadata: {
        exportedAt: new Date().toISOString(),
        format: 'NextStep-Profile-JSON',
        version: '1.0'
      },
      profile: {
        personal: {
          firstName: p.personal?.firstName || '',
          lastName: p.personal?.lastName || '',
          email: p.personal?.email || '',
          phone: p.personal?.phone || '',
          jobTitle: p.personal?.jobTitle || '',
          address: p.personal?.address || '',
          city: p.personal?.city || '',
          country: p.personal?.country || '',
          linkedinUrl: p.personal?.linkedinUrl || '',
          githubUrl: p.personal?.githubUrl || '',
          portfolioUrl: p.personal?.portfolioUrl || ''
        },
        summary: p.resume || '',
        education: p.education || [],
        experience: p.experience || [],
        skills: p.skills || [],
        languages: p.languages || [],
        projects: p.projets || [],
        certifications: p.certifications || [],
        sectionTitles: p.sectionTitles || {}
      }
    };

    const jsonContent = JSON.stringify(data, null, 2);
    const cleanFirstName = (p.personal?.firstName || '').toLowerCase().replace(/[^a-z0-9]/gi, '_');
    const cleanLastName = (p.personal?.lastName || '').toLowerCase().replace(/[^a-z0-9]/gi, '_');
    const namePart = [cleanFirstName, cleanLastName].filter(Boolean).join('_') || 'mon_profil';
    const datePart = new Date().toISOString().split('T')[0];
    const filename = `profil_${namePart}_${datePart}.json`;

    return { filename, jsonContent, data };
  }

  /**
   * Triggers client-side download of the profile JSON file.
   */
  downloadProfileJson(): void {
    const { filename, jsonContent } = this.exportProfileJson();
    const blob = new Blob([jsonContent], { type: 'application/json;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  }

  /**
   * Calcul du pourcentage de complétion du profil (equilibre par etape)
   * Chaque section du stepper a un poids significatif. Aucune section
   * ne peut etre ignoree sans descendre sous le seuil de 85%.
   */
  completionPercentage = computed(() => {
    const p = this.profile();
    let score = 0;
    
    // 1. Contact Info (12%) — equilibre sur plusieurs champs
    if (p.personal.firstName) score += 2;
    if (p.personal.lastName) score += 2;
    if (p.personal.email) score += 2;
    if (p.personal.phone) score += 2;
    if (p.personal.jobTitle) score += 2;
    if (p.personal.city) score += 1;
    if (p.personal.country) score += 1;
    
    // 2. Education (14%)
    if (p.education.length > 0) score += 14;
    
    // 3. Experience (14%)
    if (p.experience.length > 0) score += 14;
    
    // 4. Skills (16%) — poids fort: indispensable pour depasser 85%
    if (p.skills.length > 0) score += 16;
    
    // 5. Languages (4%) — bonus, ne compense pas un manque de competences
    if (p.languages.length > 0) score += 4;
    
    // 6. Projects (14%)
    if (p.projets.length > 0) score += 14;
    
    // 7. Resume (14%)
    if (p.resume && p.resume.length > 50) score += 14;
    
    // 8. Certifications (12%)
    if (p.certifications.length > 0) score += 12;

    return Math.min(score, 100);
  });

  isSectionComplete(stepId: ProfileStepId): boolean {
    const p = this.profile();
    switch(stepId) {
      case 'coordonnees': return !!(p.personal.firstName && p.personal.lastName && p.personal.email);
      case 'formation': return p.education.length > 0;
      case 'experience': return p.experience.length > 0;
      case 'competences': return p.skills.length > 0 || p.languages.length > 0;
      case 'resume': return p.resume.length > 50;
      case 'projets': return p.projets.length > 0;
      case 'certifications': return p.certifications.length > 0;
      default: return false;
    }
  }

  readonly stepLabels: Record<ProfileStepId, string> = {
    coordonnees: 'Contact Info',
    experience: 'Experience',
    formation: 'Education',
    competences: 'Skills & Languages',
    projets: 'Projects',
    resume: 'Summary',
    certifications: 'Certifications'
  };

  missingSections = computed(() => {
    const p = this.profile();
    const result: { id: ProfileStepId; label: string }[] = [];
    const steps: ProfileStepId[] = ['coordonnees', 'experience', 'formation', 'competences', 'projets', 'resume', 'certifications'];
    for (const id of steps) {
      if (!this.isSectionComplete(id)) {
        result.push({ id, label: this.stepLabels[id] });
      }
    }
    return result;
  });

  updateProfile(newData: Partial<Profile>) {
    this.profile.update(current => ({ ...current, ...newData }));
  }

  lastImportSummary = signal<ProfileImportSummary | null>(null);
  parsingEvents = signal<{type: 'info' | 'success', message: string, entity?: string, timestamp: string}[]>([]);

  private addParsingEvent(type: 'info' | 'success', message: string, entity?: string) {
    const timestamp = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' });
    this.parsingEvents.update(prev => [...prev, { type, message, entity, timestamp }]);
  }

  async importResume(file: File): Promise<void> {
    this.parsingEvents.set([]);
    this.lastImportSummary.set(null);
    try {
      // Simulation du feed pendant la requête
      this.addParsingEvent('info', 'Connecting to NextStep AI agents...');
      
      const simulateEvents = async () => {
        const events: {type: 'info' | 'success', msg: string, entity?: string}[] = [
          { type: 'info', msg: 'Reading PDF binary data...' },
          { type: 'success', msg: 'Text extraction complete', entity: 'PDF Source' },
          { type: 'info', msg: 'Waking up NextStep AI agents...' },
          { type: 'info', msg: 'Analyzing professional patterns...' },
          { type: 'info', msg: 'Extracting semantic entities...' }
        ];

        for (const e of events) {
          if (this.parsingEvents().length > 10) break; // Arrêt si fini
          this.addParsingEvent(e.type, e.msg, e.entity);
          await new Promise(r => setTimeout(r, 800));
        }
      };

      // On lance la simulation en parallèle
      void simulateEvents();

      // Véritable appel API
      const rawData = await firstValueFrom(this.api.parseResume(file));

      const normalizedPayload = normalizeImportedPayload(rawData);
      if (!hasImportedContent(normalizedPayload)) {
        throw new Error('No structured profile data could be extracted from this resume.');
      }

      this.lastImportSummary.set(buildImportSummary(normalizedPayload));
      this.addParsingEvent('success', 'AI Analysis successful!');
      await this.processExtractedData(normalizedPayload);
    } catch (error) {
      const message = extractApiError(error).message;
      this.addParsingEvent('info', message, 'Process halted');
      console.error('Erreur lors du parsing du CV:', error);
      throw error;
    }
  }

  async importLinkedIn(url: string, rawText?: string): Promise<void> {
    this.parsingEvents.set([]);
    this.lastImportSummary.set(null);
    try {
      this.addParsingEvent('info', 'Connecting to NextStep AI agents...');
      
      const simulateEvents = async () => {
        const events: {type: 'info' | 'success', msg: string, entity?: string}[] = [
          { type: 'info', msg: 'Interpreting LinkedIn profile handle...' },
          { type: 'info', msg: 'Performing deep search on public profiles...' },
          { type: 'info', msg: 'Synthesizing professional background...' }
        ];

        for (const e of events) {
          if (this.parsingEvents().length > 10) break; // Arrêt si fini
          this.addParsingEvent(e.type, e.msg, e.entity);
          await new Promise(r => setTimeout(r, 800));
        }
      };

      void simulateEvents();

      const rawData = await firstValueFrom(this.api.importLinkedIn(url, rawText));

      const normalizedPayload = normalizeImportedPayload(rawData);
      if (!hasImportedContent(normalizedPayload)) {
        throw new Error('No structured profile data could be extracted from this LinkedIn profile.');
      }

      this.lastImportSummary.set(buildImportSummary(normalizedPayload));
      this.addParsingEvent('success', 'LinkedIn Import successful!');
      await this.processExtractedData(normalizedPayload);
    } catch (error) {
      this.addParsingEvent('info', 'Error during LinkedIn import', 'Process halted');
      console.error('Erreur lors de l\'import LinkedIn:', error);
      throw error;
    }
  }

  private async processExtractedData(data: ImportedProfilePayload): Promise<void> {
    const wasOnboarding = this.isOnboarding();
    this.isOnboarding.set(false);
    const errors: string[] = [];
    try {
      this.addParsingEvent('info', 'Smart Overwrite: Clearing current profile...');
      await firstValueFrom(this.api.clearProfile());

      this.addParsingEvent('info', 'Synchronizing with profile...', 'Updating sections');

      const currentProfile = this.profile();
      const personal = {
        ...currentProfile.personal,
        firstName: data.personal.firstName || currentProfile.personal.firstName,
        lastName: data.personal.lastName || currentProfile.personal.lastName,
        // The account email comes from the login (Keycloak); a CV import never replaces it.
        email: currentProfile.personal.email || data.personal.email,
        phone: data.personal.phone || currentProfile.personal.phone,
        jobTitle: data.personal.jobTitle || currentProfile.personal.jobTitle,
        city: data.personal.city || currentProfile.personal.city,
        country: data.personal.country || currentProfile.personal.country,
        linkedinUrl: data.personal.linkedinUrl || currentProfile.personal.linkedinUrl,
        githubUrl: data.personal.githubUrl || currentProfile.personal.githubUrl,
        portfolioUrl: data.personal.portfolioUrl || currentProfile.personal.portfolioUrl,
        address: data.personal.address || currentProfile.personal.address
      };

      this.updateProfile({ personal, resume: data.resume || currentProfile.resume });
      await this.savePersonalInfo(personal);
      this.addParsingEvent('success', 'Profile identity updated', `${personal.firstName} ${personal.lastName}`.trim() || personal.email);

      const safeAdd = async <T>(label: string, name: string, fn: () => Promise<void>) => {
        try {
          await fn();
          this.addParsingEvent('success', label, name);
        } catch (err) {
          console.error(`Failed to add ${label}`, name, err);
          errors.push(`${label}: ${name}`);
          this.addParsingEvent('info', `${label} failed: ${name}`, 'Skipped');
        }
      };

      for (const exp of data.experience) {
        this.addParsingEvent('info', 'Mapping experience', exp.company || exp.title);
        await safeAdd('Experience', exp.company || exp.title, () => this.addExperience(exp, false));
      }

      for (const extra of data.extracurriculars) {
        this.addParsingEvent('info', 'Mapping extracurricular activity', extra.company || extra.title);
        await safeAdd('Extracurricular', extra.company || extra.title, () => this.addExperience(extra, false));
      }

      for (const edu of data.education) {
        this.addParsingEvent('info', 'Mapping education', edu.institution || edu.degree);
        await safeAdd('Education', edu.institution || edu.degree, () => this.addEducation(edu, false));
      }

      for (const lang of data.languages) {
        this.addParsingEvent('info', 'Mapping language', lang.name);
        await safeAdd('Language', lang.name, () => this.addLanguage(lang, false));
      }

      for (const skill of data.skills) {
        const rawName = (skill.name || '').trim();
        const normalizedName = rawName.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase();
        const looksLikeLanguage = [
          'french', 'francais', 'english', 'anglais', 'arabic', 'arabe',
          'spanish', 'espagnol', 'german', 'allemand', 'italian', 'italien',
          'russian', 'russe', 'chinese', 'chinois', 'japanese', 'japonais',
          'portuguese', 'portugais'
        ].some((l) => normalizedName === l || normalizedName.startsWith(`${l} `) || normalizedName.includes(` ${l} `));

        if (looksLikeLanguage) {
          const levelMatch = rawName.match(/\b(A1|A2|B1|B2|C1|C2|Native|Fluent|Advanced|Proficient|Beginner|Elementary|Intermediate|Upper-Intermediate)\b/i);
          const level = levelMatch?.[1] || 'B2';
          this.addParsingEvent('info', 'Mapping language from skills', rawName);
          await safeAdd('Language', rawName, () => this.addLanguage({ id: '', name: rawName.split(/[-(|]/)[0].trim(), level }, false));
          continue;
        }

        this.addParsingEvent('info', 'Mapping skill', rawName);
        await safeAdd('Skill', rawName, () => this.addSkill(skill, false));
      }

      for (const project of data.projects) {
        this.addParsingEvent('info', 'Mapping project', project.title);
        await safeAdd('Project', project.title, () => this.addProject(project, false));
      }

      for (const cert of data.certifications) {
        this.addParsingEvent('info', 'Mapping certification', cert.name);
        await safeAdd('Certification', cert.name, () => this.addCertification(cert, false));
      }

      if (errors.length > 0) {
        this.addParsingEvent('info', `${errors.length} section(s) skipped — see console for details`);
      }
      this.addParsingEvent('success', 'Profile fully synchronized!');
      await this.loadProfile();
    } catch (e) {
      console.error('Error integrating data', e);
      throw e;
    } finally {
      this.isOnboarding.set(false);
    }
  }

  setStep(stepId: ProfileStepId) {
    this.currentStep.set(stepId);
  }
}
