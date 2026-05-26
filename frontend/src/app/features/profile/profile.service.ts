import { Injectable, signal, computed, inject, effect } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Profile, ProfileStepId, PersonalInfo, Experience, Education, Skill, Project, Certification, ProfileImportSummary } from './profile.types';
import { environment } from '../../../environments/environment';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '../../core/auth/services/auth.service';

interface ImportedPersonalSnapshot {
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

interface ImportedProfilePayload {
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

interface ProfilePhotoUploadResponse {
  photoUrl: string;
  objectKey?: string;
  message?: string;
}

interface SignedProfilePhotoResponse {
  photoUrl?: string | null;
  objectKey?: string;
}

@Injectable({
  providedIn: 'root'
})
export class ProfileService {
  private readonly http = inject(HttpClient);
  private readonly authService = inject(AuthService);
  private readonly apiUrl = `${environment.apiBaseUrl}/profile`;
  private readonly SESSION_KEY = 'nextstep_onboarding_profile';

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
        console.log('Utilisateur authentifié détecté, rechargement du profil...');
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
      console.log('Chargement du profil depuis:', this.apiUrl);
      const data: any = await firstValueFrom(this.http.get<any>(this.apiUrl));
      console.log('--- REFRESH PROFILE RAW DATA ---', data);
      
      if (!data || !data.personalInfo) {
        console.warn('Données de profil incomplètes reçues du serveur');
        return;
      }

      const authUser = this.authService.user();
      
      // Determine section titles safely
      let sectionTitles = this.emptyProfile.sectionTitles;
      if (data.personalInfo.titresSections) {
        try {
          sectionTitles = JSON.parse(data.personalInfo.titresSections);
        } catch (e) {
          console.warn('Erreur lors du parsing des titres de sections, utilisation des titres par défaut');
        }
      }

      const rawCompetences = data.competences || data.Competences || [];
      
      const mappedProfile: Profile = {
        personal: {
          firstName: data.personalInfo.prenom || authUser?.firstName || '',
          lastName: data.personalInfo.nom || authUser?.lastName || '',
          email: data.personalInfo.email || authUser?.email || '',
          phone: data.personalInfo.telephone || '',
          city: data.personalInfo.ville || '',
          country: data.personalInfo.pays || '',
          jobTitle: data.personalInfo.titrePoste || '',
          photoUrl: null,
          linkedinUrl: data.personalInfo.lienLinkedin || '',
          githubUrl: data.personalInfo.lienGithub || '',
          portfolioUrl: data.personalInfo.lienPortfolio || '',
          address: (data.personalInfo.ville || data.personalInfo.pays) 
            ? `${data.personalInfo.ville || ''}, ${data.personalInfo.pays || ''}`.trim().replace(/^,|,$/g, '')
            : '',
          useAsHeadline: true
        },
        education: (data.formations || data.Formations || []).map((f: any) => ({
          id: f.id || f.Id,
          degree: f.diplome || f.Diplome,
          institution: f.etablissement || f.Etablissement,
          startYear: (f.annee || f.Annee || 2024).toString(),
          endYear: (f.anneeFin || f.AnneeFin || 2024).toString(),
          current: !f.anneeFin && !f.AnneeFin,
          specialization: f.specialisation || f.Specialisation || '',
          mention: f.mention || f.Mention || 'Passable',
          city: f.ville || f.Ville || ''
        })),
        experience: (data.experiences || data.Experiences || []).map((e: any) => ({
          id: e.id || e.Id,
          title: e.poste || e.Poste,
          company: e.entreprise || e.Entreprise,
          startDate: (e.dateDebut || e.DateDebut || '').substring(0, 7),
          endDate: (e.dateFin || e.DateFin || '').substring(0, 7),
          current: !e.dateFin && !e.DateFin,
          description: e.missions || e.Missions || '',
          city: e.ville || e.Ville || '',
          type: (e.type === 'Parascolaire' || e.Type === 'Parascolaire' || e.type === 'Extracurricular' || e.Type === 'Extracurricular') ? 'Extracurricular' : (e.type || e.Type || 'Internship'),
          taches: e.taches || e.Taches || []
        })),
        skills: rawCompetences.filter((c: any) => {
          const type = (c.typeCompetence || c.TypeCompetence || '').toLowerCase();
          return !type.includes('lang') && !type.includes('linguist');
        }).map((c: any) => ({
          id: c.id || c.Id,
          name: c.nom || c.Nom,
          category: (c.typeCompetence === 'Technique' || c.typeCompetence === 'Technical' || c.TypeCompetence === 'Technical') ? 'Technical' : (c.typeCompetence || c.TypeCompetence || 'Technical')
        })),
        languages: rawCompetences.filter((c: any) => {
          const type = (c.typeCompetence || c.TypeCompetence || '').toLowerCase();
          return type.includes('lang') || type.includes('linguist');
        }).map((c: any) => ({
          id: c.id || c.Id,
          name: c.nom || c.Nom,
          level: this.mapIntToLevel(c.niveau || c.Niveau || 3)
        })),
        resume: data.personalInfo.resumeProfessionnel || '',
        projets: (data.projets || data.Projets || []).map((p: any) => ({
          id: p.id || p.Id,
          title: p.titreProjet || p.TitreProjet,
          description: p.description || p.Description,
          stack: (p.technologiesUtilisees || p.TechnologiesUtilisees || '').split(',').map((s: string) => s.trim()).filter((s: string) => s !== ''),
          githubUrl: p.lienProjet || p.LienProjet || '',
          demoUrl: p.demoUrl || p.DemoUrl || '',
          imageUrl: p.imageUrl || p.ImageUrl || '',
          isUniversity: p.isUniversity || p.IsUniversity || false,
          taches: p.taches || p.Taches || []
        })),
        certifications: (data.certifications || data.Certifications || []).map((c: any) => ({
          id: c.id || c.Id,
          name: c.titre || c.Titre,
          issuer: c.organisation || c.Organisation,
          date: c.dateObtention || c.DateObtention,
          verificationUrl: c.urlCredential || c.UrlCredential
        })),
        sectionTitles: sectionTitles
      };
      
      this.profile.set(mappedProfile);

      this.profile.update(profile => ({
        ...profile,
        personal: {
          ...profile.personal,
          photoUrl: data.personalInfo.photoUrl ? this.profilePhotoEndpoint() : null,
        }
      }));
    } catch (error) {
      console.error('Erreur chargement profil:', error);
    }
  }

  async flushOnboardingData() {
    const data = this.loadFromSessionStorage();
    if (!data) return;
    this.profile.set(data);
    this.isOnboarding.set(false);
    await firstValueFrom(this.http.delete(`${this.apiUrl}/clear`));
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
    const dto = {
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
      resumeProfessionnel: this.profile().resume,
      titresSections: JSON.stringify(this.profile().sectionTitles)
    };
    return firstValueFrom(this.http.put(`${this.apiUrl}/personal-info`, dto));
  }

  async uploadProfilePhoto(file: File): Promise<string> {
    const formData = new FormData();
    formData.append('file', file);

    const response = await firstValueFrom(
      this.http.post<ProfilePhotoUploadResponse>(`${this.apiUrl}/photo`, formData)
    );

    if (!response?.photoUrl) {
      throw new Error('Profile photo upload succeeded but no photo URL was returned.');
    }

    const photoUrl = this.profilePhotoEndpoint();

    this.profile.update(profile => ({
      ...profile,
      personal: {
        ...profile.personal,
        photoUrl,
      }
    }));

    return photoUrl;
  }

  private profilePhotoEndpoint(): string {
    return `${this.apiUrl}/photo?v=${Date.now()}`;
  }

  async getSignedProfilePhotoUrl(): Promise<string | null> {
    try {
      const response = await firstValueFrom(
        this.http.get<SignedProfilePhotoResponse>(`${this.apiUrl}/photo/signed`)
      );
      return response?.photoUrl ? this.profilePhotoEndpoint() : null;
    } catch (error) {
      console.warn('Unable to get signed profile photo URL', error);
      return null;
    }
  }

  private safeIsoDate(dateStr: string | null | undefined): string | null {
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

  async addExperience(exp: Experience, refresh = true) {
    if (this.isOnboarding()) {
      this.profile.update(p => ({ ...p, experience: [...p.experience, { ...exp, id: exp.id || crypto.randomUUID() }] }));
      return;
    }
    const dateD = exp.startDate ? (exp.startDate.includes('-') ? exp.startDate : exp.startDate + '-01') : null;
    const dateF = exp.endDate ? (exp.endDate.includes('-') ? exp.endDate : exp.endDate + '-01') : null;
    
    const dto = {
      entreprise: exp.company,
      poste: exp.title,
      dateDebut: this.safeIsoDate(dateD),
      dateFin: this.safeIsoDate(dateF),
      missions: exp.description,
      ville: exp.city,
      type: exp.type,
      taches: exp.taches ?? []
    };
    await firstValueFrom(this.http.post(`${this.apiUrl}/experiences`, dto));
    if (refresh) await this.loadProfile();
  }

  async updateExperience(exp: Experience) {
    if (this.isOnboarding()) {
      this.profile.update(p => ({ ...p, experience: p.experience.map(e => e.id === exp.id ? exp : e) }));
      return;
    }
    const dateD = exp.startDate ? (exp.startDate.includes('-') ? exp.startDate : exp.startDate + '-01') : null;
    const dateF = exp.endDate ? (exp.endDate.includes('-') ? exp.endDate : exp.endDate + '-01') : null;

    const dto = {
      id: exp.id,
      entreprise: exp.company,
      poste: exp.title,
      dateDebut: this.safeIsoDate(dateD),
      dateFin: this.safeIsoDate(dateF),
      missions: exp.description,
      ville: exp.city,
      type: exp.type,
      taches: exp.taches ?? []
    };
    await firstValueFrom(this.http.put(`${this.apiUrl}/experiences`, dto));
    await this.loadProfile();
  }


  async deleteExperience(id: string) {
    if (this.isOnboarding()) {
      this.profile.update(p => ({ ...p, experience: p.experience.filter(e => e.id !== id) }));
      return;
    }
    await firstValueFrom(this.http.delete(`${this.apiUrl}/experiences/${id}`));
    await this.loadProfile();
  }

  async addEducation(edu: Education, refresh = true) {
    if (this.isOnboarding()) {
      this.profile.update(p => ({ ...p, education: [...p.education, { ...edu, id: edu.id || crypto.randomUUID() }] }));
      return;
    }
    const dto = {
      etablissement: edu.institution,
      diplome: edu.degree,
      annee: parseInt(edu.startYear) || 2024,
      ville: edu.city,
      specialisation: edu.specialization,
      mention: edu.mention,
      anneeFin: parseInt(edu.endYear) || null
    };
    await firstValueFrom(this.http.post(`${this.apiUrl}/formations`, dto));
    if (refresh) await this.loadProfile();
  }

  async updateEducation(edu: Education) {
    if (this.isOnboarding()) {
      this.profile.update(p => ({ ...p, education: p.education.map(e => e.id === edu.id ? edu : e) }));
      return;
    }
    const dto = {
      id: edu.id,
      etablissement: edu.institution,
      diplome: edu.degree,
      annee: parseInt(edu.startYear) || 2024,
      ville: edu.city,
      specialisation: edu.specialization,
      mention: edu.mention,
      anneeFin: parseInt(edu.endYear) || null
    };
    await firstValueFrom(this.http.put(`${this.apiUrl}/formations`, dto));
    await this.loadProfile();
  }


  async deleteEducation(id: string) {
    if (this.isOnboarding()) {
      this.profile.update(p => ({ ...p, education: p.education.filter(e => e.id !== id) }));
      return;
    }
    await firstValueFrom(this.http.delete(`${this.apiUrl}/formations/${id}`));
    await this.loadProfile();
  }

  async addSkill(skill: Skill, refresh = true) {
    if (this.isOnboarding()) {
      this.profile.update(p => ({ ...p, skills: [...p.skills, { ...skill, id: skill.id || crypto.randomUUID() }] }));
      return;
    }
    const dto = {
      nom: skill.name,
      niveau: 3,
      typeCompetence: skill.category
    };
    await firstValueFrom(this.http.post(`${this.apiUrl}/competences`, dto));
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
    const dto = {
      id: skill.id,
      nom: skill.name,
      niveau: 3,
      typeCompetence: skill.category
    };
    await firstValueFrom(this.http.put(`${this.apiUrl}/competences`, dto));
    await this.loadProfile();
  }

  async deleteSkill(id: string) {
    if (this.isOnboarding()) {
      this.profile.update(p => ({ ...p, skills: p.skills.filter(s => s.id !== id) }));
      return;
    }
    await firstValueFrom(this.http.delete(`${this.apiUrl}/competences/${id}`));
    await this.loadProfile();
  }

  // --- Langues (mapped to Competences in DB) ---
  private normalizeDiacritics(s: string): string {
    return s.normalize('NFD').replace(/[\u0300-\u036f]/g, '');
  }

  mapLevelToInt(level: string): number {
    const mapping: Record<string, number> = { 
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
      'Maternel': 7
    };
    const clean = this.normalizeDiacritics((level || '').replace(/\s*\(.*?\)\s*/g, '').trim()).toUpperCase();
    const caseInsensitiveMap: Record<string, number> = {};
    for (const key in mapping) {
      caseInsensitiveMap[this.normalizeDiacritics(key).toUpperCase()] = mapping[key];
    }
    return caseInsensitiveMap[clean] || 3; 
  }

  mapIntToLevel(val: number): string {
    const levels: any = { 
      1: 'A1', 
      2: 'A2', 
      3: 'B1', 
      4: 'B2', 
      5: 'C1', 
      6: 'C2', 
      7: 'Native' 
    };
    return levels[val] || 'B1';
  }

  mapExperienceType(type: string): any {
    const mapping: Record<string, string> = {
      'Stage': 'Internship',
      'Alternance': 'Apprenticeship',
      'CDI': 'CDI',
      'CDD': 'CDD',
      'Freelance': 'Freelance',
      'PFA': 'PFA',
      'PFE': 'PFE',
      'Parascolaire': 'Extracurricular',
      'Extracurricular': 'Extracurricular'
    };
    return mapping[type] || 'Internship';
  }

  async addLanguage(lang: any, refresh = true) {
    if (this.isOnboarding()) {
      this.profile.update(p => ({ ...p, languages: [...p.languages, { ...lang, id: lang.id || crypto.randomUUID() }] }));
      return;
    }
    const dto = {
      nom: lang.name,
      niveau: this.mapLevelToInt(lang.level),
      typeCompetence: 'Langue'
    };
    await firstValueFrom(this.http.post(`${this.apiUrl}/competences`, dto));
    if (refresh) await this.loadProfile();
  }

  async updateLanguage(lang: any) {
    if (this.isOnboarding()) {
      this.profile.update(p => ({ ...p, languages: p.languages.map(l => l.id === lang.id ? lang : l) }));
      return;
    }
    const dto = {
      id: lang.id,
      nom: lang.name,
      niveau: this.mapLevelToInt(lang.level),
      typeCompetence: 'Langue'
    };
    await firstValueFrom(this.http.put(`${this.apiUrl}/competences`, dto));
    await this.loadProfile();
  }

  async deleteLanguage(id: string) {
    if (this.isOnboarding()) {
      this.profile.update(p => ({ ...p, languages: p.languages.filter(l => l.id !== id) }));
      return;
    }
    await firstValueFrom(this.http.delete(`${this.apiUrl}/competences/${id}`));
    await this.loadProfile();
  }

  async addProject(p: Project, refresh = true) {
    if (this.isOnboarding()) {
      this.profile.update(profile => ({ ...profile, projets: [...profile.projets, { ...p, id: p.id || crypto.randomUUID() }] }));
      return;
    }
    const dto = {
      titreProjet: p.title,
      description: p.description,
      technologiesUtilisees: p.stack.join(','),
      lienProjet: p.githubUrl,
      demoUrl: p.demoUrl,
      imageUrl: p.imageUrl,
      isUniversity: p.isUniversity,
      taches: p.taches ?? []
    };
    await firstValueFrom(this.http.post(`${this.apiUrl}/projets`, dto));
    if (refresh) await this.loadProfile();
  }

  async updateProject(p: Project) {
    if (this.isOnboarding()) {
      this.profile.update(profile => ({ ...profile, projets: profile.projets.map(pr => pr.id === p.id ? p : pr) }));
      return;
    }
    const dto = {
      id: p.id,
      titreProjet: p.title,
      description: p.description,
      technologiesUtilisees: p.stack.join(','),
      lienProjet: p.githubUrl,
      demoUrl: p.demoUrl,
      imageUrl: p.imageUrl,
      isUniversity: p.isUniversity,
      taches: p.taches ?? []
    };
    await firstValueFrom(this.http.put(`${this.apiUrl}/projets`, dto));
    await this.loadProfile();
  }


  async deleteProject(id: string) {
    if (this.isOnboarding()) {
      this.profile.update(p => ({ ...p, projets: p.projets.filter(pr => pr.id !== id) }));
      return;
    }
    await firstValueFrom(this.http.delete(`${this.apiUrl}/projets/${id}`));
    await this.loadProfile();
  }

  async addCertification(c: Certification, refresh = true) {
    if (this.isOnboarding()) {
      this.profile.update(p => ({ ...p, certifications: [...p.certifications, { ...c, id: c.id || crypto.randomUUID() }] }));
      return;
    }
    const dto = {
      titre: c.name,
      organisation: c.issuer,
      dateObtention: this.safeIsoDate(c.date),
      urlCredential: c.verificationUrl
    };
    await firstValueFrom(this.http.post(`${this.apiUrl}/certifications`, dto));
    if (refresh) await this.loadProfile();
  }

  async updateCertification(c: Certification) {
    if (this.isOnboarding()) {
      this.profile.update(p => ({ ...p, certifications: p.certifications.map(cert => cert.id === c.id ? c : cert) }));
      return;
    }
    const dto = {
      id: c.id,
      titre: c.name,
      organisation: c.issuer,
      dateObtention: this.safeIsoDate(c.date),
      urlCredential: c.verificationUrl
    };
    await firstValueFrom(this.http.put(`${this.apiUrl}/certifications`, dto));
    await this.loadProfile();
  }


  async deleteCertification(id: string) {
    if (this.isOnboarding()) {
      this.profile.update(p => ({ ...p, certifications: p.certifications.filter(c => c.id !== id) }));
      return;
    }
    await firstValueFrom(this.http.delete(`${this.apiUrl}/certifications/${id}`));
    await this.loadProfile();
  }

  // --- Nouveaux appels backend ---

  async getKeywords(): Promise<{mot: string, categorie: string}[]> {
    try {
      return await firstValueFrom(this.http.get<{mot: string, categorie: string}[]>(`${this.apiUrl}/keywords`));
    } catch (e) {
      console.error('Erreur chargement keywords', e);
      return [];
    }
  }

  async generateResume(profileData: any): Promise<string> {
    try {
      const res = await firstValueFrom(this.http.post<any>(`${this.apiUrl}/generate-resume`, profileData));
      return res.resume || '';
    } catch (e) {
      console.error('Erreur génération CV IA', e);
      return 'Generation error.';
    }
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

  private readImportObject(source: any, keys: string[]): any {
    for (const key of keys) {
      const value = source?.[key];
      if (value && typeof value === 'object' && !Array.isArray(value)) {
        return value;
      }
    }
    return {};
  }

  private readImportArray(source: any, keys: string[]): any[] {
    for (const key of keys) {
      const value = source?.[key];
      if (Array.isArray(value)) {
        return value;
      }
    }
    return [];
  }

  private readImportValue(source: any, keys: string[]): string {
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

  private normalizeMonth(value: unknown): string {
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

  private normalizeYear(value: unknown): string {
    const monthValue = this.normalizeMonth(value);
    return monthValue ? monthValue.slice(0, 4) : '';
  }

  private normalizeStack(value: unknown): string[] {
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

  private normalizeTasks(value: unknown): string[] {
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

  private dedupeByKey<T>(items: T[], keySelector: (item: T) => string): T[] {
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

  private isKnownLanguage(value: string): boolean {
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

  private normalizeImportedPayload(rawData: any): ImportedProfilePayload {
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
    const personalSource = this.readImportObject(source, ['personal', 'personalInfo', 'personal_info', 'contact']);

    const personal: ImportedPersonalSnapshot = {
      firstName: this.readImportValue(personalSource, ['prenom', 'firstName', 'first_name']),
      lastName: this.readImportValue(personalSource, ['nom', 'lastName', 'last_name']),
      email: this.readImportValue(personalSource, ['email']),
      phone: this.readImportValue(personalSource, ['telephone', 'phone', 'mobile']),
      jobTitle: this.readImportValue(personalSource, ['titrePoste', 'jobTitle', 'title', 'headline']),
      city: this.readImportValue(personalSource, ['ville', 'city']),
      country: this.readImportValue(personalSource, ['pays', 'country']),
      linkedinUrl: this.readImportValue(personalSource, ['lienLinkedin', 'linkedinUrl', 'linkedin']),
      githubUrl: this.readImportValue(personalSource, ['lienGithub', 'githubUrl', 'github']),
      portfolioUrl: this.readImportValue(personalSource, ['lienPortfolio', 'portfolioUrl', 'portfolio', 'website', 'siteWeb']),
      address: this.readImportValue(personalSource, ['adresse', 'address'])
    };

    const resume = this.readImportValue(personalSource, ['resumeProfessionnel', 'summary', 'resume', 'about'])
      || this.readImportValue(source, ['resumeProfessionnel', 'summary', 'resume', 'about']);

    const normalizedExperience = this.dedupeByKey(
      this.readImportArray(source, ['experience', 'experiences', 'workExperience', 'workExperiences']).map((entry: any) => ({
        id: '',
        company: this.readImportValue(entry, ['entreprise', 'company', 'organisation']),
        title: this.readImportValue(entry, ['poste', 'title', 'role', 'position']),
        startDate: this.normalizeMonth(entry?.dateDebut ?? entry?.startDate),
        endDate: this.normalizeMonth(entry?.dateFin ?? entry?.endDate),
        city: this.readImportValue(entry, ['ville', 'city']),
        description: this.readImportValue(entry, ['missions', 'description', 'summary']),
        current: !this.normalizeMonth(entry?.dateFin ?? entry?.endDate),
        type: this.mapExperienceType(this.readImportValue(entry, ['type', 'employmentType', 'contractType']) || 'Stage'),
        taches: this.normalizeTasks(entry?.taches ?? entry?.tasks ?? entry?.responsibilities)
      })).filter((entry) => entry.company || entry.title || entry.description),
      (entry) => `${entry.company}|${entry.title}|${entry.startDate}|${entry.endDate}`
    );

    const normalizedExtracurriculars = this.dedupeByKey(
      this.readImportArray(source, ['extracurricular', 'extracurriculars', 'parascolaire', 'activities']).map((entry: any) => ({
        id: '',
        company: this.readImportValue(entry, ['organisation', 'company', 'entreprise', 'club']),
        title: this.readImportValue(entry, ['titre', 'title', 'role', 'poste']) || 'Activity',
        startDate: this.normalizeMonth(entry?.dateDebut ?? entry?.startDate),
        endDate: this.normalizeMonth(entry?.dateFin ?? entry?.endDate),
        city: this.readImportValue(entry, ['ville', 'city']),
        description: this.readImportValue(entry, ['description', 'missions', 'summary']),
        current: !this.normalizeMonth(entry?.dateFin ?? entry?.endDate),
        type: 'Extracurricular' as const,
        taches: this.normalizeTasks(entry?.taches ?? entry?.tasks ?? entry?.responsibilities)
      })).filter((entry) => entry.company || entry.title || entry.description),
      (entry) => `${entry.company}|${entry.title}|${entry.startDate}|${entry.endDate}`
    );

    const normalizedEducation = this.dedupeByKey(
      this.readImportArray(source, ['education', 'educations', 'formation', 'formations']).map((entry: any) => {
        const startYear = this.normalizeYear(entry?.annee ?? entry?.startYear ?? entry?.dateDebut);
        const endYear = this.normalizeYear(entry?.anneeFin ?? entry?.endYear ?? entry?.dateFin);

        return {
          id: '',
          institution: this.readImportValue(entry, ['etablissement', 'institution', 'school']),
          degree: this.readImportValue(entry, ['diplome', 'degree', 'title']),
          city: this.readImportValue(entry, ['ville', 'city']),
          startYear: startYear || endYear || new Date().getFullYear().toString(),
          endYear,
          current: !endYear,
          specialization: this.readImportValue(entry, ['specialisation', 'specialization', 'fieldOfStudy']),
          mention: 'Passable' as const
        };
      }).filter((entry) => entry.institution || entry.degree),
      (entry) => `${entry.institution}|${entry.degree}|${entry.startYear}|${entry.endYear}`
    );

    const explicitLanguages = this.readImportArray(source, ['languages', 'langues', 'langue', 'language']).map((entry: any) => ({
      id: '',
      name: typeof entry === 'string' ? entry.trim() : this.readImportValue(entry, ['nom', 'name', 'language', 'langue']),
      level: typeof entry === 'string' ? 'B2' : (this.readImportValue(entry, ['niveau', 'level', 'proficiency']) || 'B2')
    })).filter((entry) => entry.name);

    const skillLikeEntries = this.readImportArray(source, ['skills', 'competences', 'compétences', 'competencies', 'competency', 'skill']);
    const normalizedSkills = skillLikeEntries.map((entry: any) => ({
      name: typeof entry === 'string' ? entry.trim() : this.readImportValue(entry, ['nom', 'name', 'skill']),
      rawType: typeof entry === 'string' ? '' : this.readImportValue(entry, ['typeCompetence', 'type', 'category']),
      level: typeof entry === 'string' ? 'B2' : (this.readImportValue(entry, ['niveau', 'level']) || 'B2')
    })).filter((entry) => entry.name);

    const inferredLanguages = normalizedSkills
      .filter((entry) => entry.rawType.toLowerCase().includes('lang') || entry.rawType.toLowerCase().includes('linguist') || this.isKnownLanguage(entry.name))
      .map((entry) => ({ id: '', name: entry.name, level: entry.level }));

    const languages = this.dedupeByKey(
      [...explicitLanguages, ...inferredLanguages],
      (entry) => entry.name
    );

    const skills = this.dedupeByKey(
      normalizedSkills
        .filter((entry) => !languages.some((language) => language.name.trim().toLowerCase() === entry.name.trim().toLowerCase()))
        .map((entry) => ({
          id: '',
          name: entry.name,
          category: entry.rawType || 'Technical'
        })),
      (entry) => entry.name
    );

    const projects = this.dedupeByKey(
      this.readImportArray(source, ['projects', 'projets']).map((entry: any) => ({
        id: '',
        title: this.readImportValue(entry, ['titre', 'title', 'name']),
        description: this.readImportValue(entry, ['description', 'summary']),
        stack: this.normalizeStack(entry?.technologies ?? entry?.technologiesUtilisees ?? entry?.stack),
        githubUrl: this.readImportValue(entry, ['lien', 'githubUrl', 'lienProjet', 'url']),
        demoUrl: this.readImportValue(entry, ['demoUrl', 'liveUrl']),
        imageUrl: this.readImportValue(entry, ['imageUrl']),
        isUniversity: Boolean(entry?.isUniversity ?? entry?.isAcademic),
        taches: this.normalizeTasks(entry?.taches ?? entry?.tasks ?? entry?.responsibilities)
      })).filter((entry) => entry.title || entry.description),
      (entry) => `${entry.title}|${entry.githubUrl}|${entry.demoUrl}`
    );

    const certifications = this.dedupeByKey(
      this.readImportArray(source, ['certifications', 'certification', 'certifs', 'licenses']).map((entry: any) => ({
        id: '',
        name: this.readImportValue(entry, ['titre', 'name', 'title']),
        issuer: this.readImportValue(entry, ['organisation', 'issuer', 'organisme']),
        date: this.normalizeMonth(entry?.date ?? entry?.dateObtention ?? entry?.issuedAt),
        verificationUrl: this.readImportValue(entry, ['lien', 'url', 'verificationUrl', 'credentialUrl'])
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

  private buildImportSummary(payload: ImportedProfilePayload): ProfileImportSummary {
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

  private hasImportedContent(payload: ImportedProfilePayload): boolean {
    const summary = this.buildImportSummary(payload);
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

  async importResume(file: File): Promise<void> {
    this.parsingEvents.set([]);
    this.lastImportSummary.set(null);
    const formData = new FormData();
    formData.append('file', file);

    try {
      // Simulation du feed pendant la requête
      this.addParsingEvent('info', 'Connecting to NextStep AI agents...');
      
      const simulateEvents = async () => {
        const events: {type: 'info' | 'success', msg: string, entity?: string}[] = [
          { type: 'info', msg: 'Reading PDF binary data...' },
          { type: 'success', msg: 'Text extraction complete', entity: 'PDF Source' },
          { type: 'info', msg: 'Waking up Llama-3.3 agents...' },
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
      const rawData = await firstValueFrom(
        this.http.post<any>(`${this.apiUrl}/parse-resume`, formData)
      );

      const normalizedPayload = this.normalizeImportedPayload(rawData);
      if (!this.hasImportedContent(normalizedPayload)) {
        throw new Error('No structured profile data could be extracted from this resume.');
      }

      this.lastImportSummary.set(this.buildImportSummary(normalizedPayload));
      this.addParsingEvent('success', 'AI Analysis successful!');
      await this.processExtractedData(normalizedPayload);
    } catch (error) {
      this.addParsingEvent('info', 'Error during parsing', 'Process halted');
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

      const rawData = await firstValueFrom(
        this.http.post<any>(`${this.apiUrl}/import-linkedin`, { url, rawText })
      );

      const normalizedPayload = this.normalizeImportedPayload(rawData);
      if (!this.hasImportedContent(normalizedPayload)) {
        throw new Error('No structured profile data could be extracted from this LinkedIn profile.');
      }

      this.lastImportSummary.set(this.buildImportSummary(normalizedPayload));
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
    this.isOnboarding.set(false); // Bypass local signal mode to hit actual backend endpoints
    try {
      // Clear existing profile data for a clean import
      this.addParsingEvent('info', 'Smart Overwrite: Clearing current profile...');
      await firstValueFrom(this.http.delete(`${this.apiUrl}/clear`));

      this.addParsingEvent('info', 'Synchronizing with profile...', 'Updating sections');

      // 1. Personal Info
      const currentProfile = this.profile();
      const personal = {
        ...currentProfile.personal,
        firstName: data.personal.firstName || currentProfile.personal.firstName,
        lastName: data.personal.lastName || currentProfile.personal.lastName,
        email: data.personal.email || currentProfile.personal.email,
        phone: data.personal.phone || currentProfile.personal.phone,
        jobTitle: data.personal.jobTitle || currentProfile.personal.jobTitle,
        city: data.personal.city || currentProfile.personal.city,
        country: data.personal.country || currentProfile.personal.country,
        linkedinUrl: data.personal.linkedinUrl || currentProfile.personal.linkedinUrl,
        githubUrl: data.personal.githubUrl || currentProfile.personal.githubUrl,
        portfolioUrl: data.personal.portfolioUrl || currentProfile.personal.portfolioUrl,
        address: data.personal.address || currentProfile.personal.address
      };

      this.updateProfile({
        personal,
        resume: data.resume || currentProfile.resume
      });

      await this.savePersonalInfo(personal);
      this.addParsingEvent('success', 'Profile identity updated', `${personal.firstName} ${personal.lastName}`.trim() || personal.email);

      // 2. Experiences (Work)
      for (const exp of data.experience) {
        this.addParsingEvent('info', 'Mapping experience', exp.company || exp.title);
        await this.addExperience(exp, false);
        this.addParsingEvent('success', 'Experience synced', exp.company || exp.title);
      }

      for (const extra of data.extracurriculars) {
        this.addParsingEvent('info', 'Mapping extracurricular activity', extra.company || extra.title);
        await this.addExperience(extra, false);
        this.addParsingEvent('success', 'Extracurricular synced', extra.company || extra.title);
      }

      // 3. Education
      for (const edu of data.education) {
        this.addParsingEvent('info', 'Mapping education', edu.institution || edu.degree);
        await this.addEducation(edu, false);
        this.addParsingEvent('success', 'Education synced', edu.degree || edu.institution);
      }

      // 4. Skills & Languages
      for (const lang of data.languages) {
        this.addParsingEvent('info', 'Mapping language', lang.name);
        await this.addLanguage(lang, false);
        this.addParsingEvent('success', 'Language added', lang.name);
      }

      for (const skill of data.skills) {
        const rawName = (skill.name || '').trim();
        const normalizedName = rawName
          .normalize('NFD')
          .replace(/[\u0300-\u036f]/g, '')
          .toLowerCase();

        const looksLikeLanguage = [
          'french', 'francais', 'english', 'anglais', 'arabic', 'arabe',
          'spanish', 'espagnol', 'german', 'allemand', 'italian', 'italien',
          'russian', 'russe', 'chinese', 'chinois', 'japanese', 'japonais',
          'portuguese', 'portugais'
        ].some((lang) => normalizedName === lang || normalizedName.startsWith(`${lang} `) || normalizedName.includes(` ${lang} `));

        if (looksLikeLanguage) {
          const levelMatch = rawName.match(/\b(A1|A2|B1|B2|C1|C2|Native|Fluent|Advanced|Proficient|Beginner|Elementary|Intermediate|Upper-Intermediate)\b/i);
          const level = levelMatch?.[1] || 'B2';
          this.addParsingEvent('info', 'Mapping language from skills', rawName);
          await this.addLanguage({ id: '', name: rawName.split(/[-(|]/)[0].trim(), level }, false);
          this.addParsingEvent('success', 'Language added', rawName);
          continue;
        }

        this.addParsingEvent('info', 'Mapping skill', rawName);
        await this.addSkill(skill, false);
        this.addParsingEvent('success', 'Skill added', rawName);
      }

      for (const project of data.projects) {
        this.addParsingEvent('info', 'Mapping project', project.title);
        await this.addProject(project, false);
        this.addParsingEvent('success', 'Project synced', project.title);
      }

      for (const cert of data.certifications) {
        this.addParsingEvent('info', 'Mapping certification', cert.name);
        await this.addCertification(cert, false);
        this.addParsingEvent('success', 'Cert synced', cert.name);
      }

      this.addParsingEvent('success', 'Profile fully synchronized!');
      await this.loadProfile();
    } catch (e) {
      console.error('Error integrating data', e);
      throw e;
    } finally {
      this.isOnboarding.set(wasOnboarding);
    }
  }

  setStep(stepId: ProfileStepId) {
    this.currentStep.set(stepId);
  }
}
