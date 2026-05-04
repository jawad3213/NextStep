import { Injectable, signal, computed, inject, effect } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Profile, ProfileStepId, PersonalInfo, Experience, Education, Skill, Project, Certification } from './profile.types';
import { environment } from '../../../environments/environment';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '../../core/auth/services/auth.service';

@Injectable({
  providedIn: 'root'
})
export class ProfileService {
  private readonly http = inject(HttpClient);
  private readonly authService = inject(AuthService);
  private readonly apiUrl = `${environment.apiBaseUrl}/profile`;

  // Initial Empty State
  private readonly emptyProfile: Profile = {
    personal: {
      firstName: '', lastName: '', email: '', phone: '',
      jobTitle: '', address: '', city: '', country: '',
      linkedinUrl: '', githubUrl: '', photoUrl: null,
      useAsHeadline: true
    },
    education: [], experience: [], skills: [], languages: [],
    resume: '', projets: [], certifications: [],
    sectionTitles: {
      formation: 'Education',
      experience: 'Work Experience',
      competences: 'Skills',
      projets: 'Personal Projects',
      certifications: 'Certifications'
    }
  };

  profile = signal<Profile>(this.emptyProfile);
  currentStep = signal<ProfileStepId>('coordonnees');

  constructor() {
    // Auto-reload profile when auth user changes (Keycloak finished loading)
    effect(() => {
      const user = this.authService.user();
      if (user) {
        console.log('Utilisateur authentifié détecté, rechargement du profil...');
        this.refreshProfile();
      }
    });
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
          photoUrl: data.personalInfo.photoUrl || null,
          linkedinUrl: data.personalInfo.lienLinkedin || '',
          githubUrl: data.personalInfo.lienGithub || '',
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
          type: (e.type === 'Parascolaire' || e.Type === 'Parascolaire' || e.type === 'Extracurricular' || e.Type === 'Extracurricular') ? 'Extracurricular' : (e.type || e.Type || 'Internship')
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
          isUniversity: p.isUniversity || p.IsUniversity || false
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
    } catch (error) {
      console.error('Erreur chargement profil:', error);
    }
  }

  async savePersonalInfo(info: PersonalInfo) {
    const dto = {
      nom: info.lastName,
      prenom: info.firstName,
      email: info.email,
      telephone: info.phone,
      ville: info.city,
      pays: info.country,
      titrePoste: info.jobTitle,
      photoUrl: info.photoUrl,
      lienLinkedin: info.linkedinUrl,
      lienGithub: info.githubUrl,
      resumeProfessionnel: this.profile().resume,
      titresSections: JSON.stringify(this.profile().sectionTitles)
    };
    return firstValueFrom(this.http.put(`${this.apiUrl}/personal-info`, dto));
  }

  async addExperience(exp: Experience, refresh = true) {
    const dateD = exp.startDate ? (exp.startDate.includes('-') ? exp.startDate : exp.startDate + '-01') : null;
    const dateF = exp.endDate ? (exp.endDate.includes('-') ? exp.endDate : exp.endDate + '-01') : null;
    
    const dto = {
      entreprise: exp.company,
      poste: exp.title,
      dateDebut: dateD ? new Date(dateD).toISOString() : null,
      dateFin: dateF ? new Date(dateF).toISOString() : null,
      missions: exp.description,
      ville: exp.city,
      type: exp.type
    };
    await firstValueFrom(this.http.post(`${this.apiUrl}/experiences`, dto));
    if (refresh) await this.loadProfile();
  }

  async updateExperience(exp: Experience) {
    const dto = {
      id: exp.id,
      entreprise: exp.company,
      poste: exp.title,
      dateDebut: exp.startDate ? new Date(exp.startDate + '-01').toISOString() : null,
      dateFin: exp.endDate ? new Date(exp.endDate + '-01').toISOString() : null,
      missions: exp.description,
      ville: exp.city,
      type: exp.type
    };
    await firstValueFrom(this.http.put(`${this.apiUrl}/experiences`, dto));
    await this.loadProfile();
  }


  async deleteExperience(id: string) {
    await firstValueFrom(this.http.delete(`${this.apiUrl}/experiences/${id}`));
    await this.loadProfile();
  }

  async addEducation(edu: Education, refresh = true) {
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
    await firstValueFrom(this.http.delete(`${this.apiUrl}/formations/${id}`));
    await this.loadProfile();
  }

  async addSkill(skill: Skill) {
    const dto = {
      nom: skill.name,
      niveau: 3,
      typeCompetence: skill.category
    };
    await firstValueFrom(this.http.post(`${this.apiUrl}/competences`, dto));
    await this.loadProfile();
  }

  isSkillSelected(skillName: string): boolean {
    if (!this.profile().skills || !skillName) return false;
    return this.profile().skills.some(s => s.name?.toLowerCase() === skillName.toLowerCase());
  }

  async updateSkill(skill: Skill) {
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
    await firstValueFrom(this.http.delete(`${this.apiUrl}/competences/${id}`));
    await this.loadProfile();
  }

  // --- Langues (mapped to Competences in DB) ---
  mapLevelToInt(level: string): number {
    const mapping: Record<string, number> = { 
      'A1': 1, 'A2': 2, 'B1': 3, 'B2': 4, 'C1': 5, 'C2': 5, 
      'Native': 5, 'Natif': 5, 'Maternelle': 5,
      'Debutant': 1, 'Intermediaire': 3, 'Avancé': 5, 'Expert': 5
    };
    return mapping[level] || 3; 
  }

  mapIntToLevel(val: number): string {
    const levels: any = { 1: 'A1', 2: 'A2', 3: 'B1', 4: 'B2', 5: 'Native' };
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

  async addLanguage(lang: any) {
    const dto = {
      nom: lang.name,
      niveau: this.mapLevelToInt(lang.level),
      typeCompetence: 'Langue'
    };
    await firstValueFrom(this.http.post(`${this.apiUrl}/competences`, dto));
    await this.loadProfile();
  }

  async updateLanguage(lang: any) {
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
    await firstValueFrom(this.http.delete(`${this.apiUrl}/competences/${id}`));
    await this.loadProfile();
  }

  async addProject(p: Project) {
    const dto = {
      titreProjet: p.title,
      description: p.description,
      technologiesUtilisees: p.stack.join(','),
      lienProjet: p.githubUrl,
      demoUrl: p.demoUrl,
      imageUrl: p.imageUrl,
      isUniversity: p.isUniversity
    };
    await firstValueFrom(this.http.post(`${this.apiUrl}/projets`, dto));
    await this.loadProfile();
  }

  async updateProject(p: Project) {
    const dto = {
      id: p.id,
      titreProjet: p.title,
      description: p.description,
      technologiesUtilisees: p.stack.join(','),
      lienProjet: p.githubUrl,
      demoUrl: p.demoUrl,
      imageUrl: p.imageUrl,
      isUniversity: p.isUniversity
    };
    await firstValueFrom(this.http.put(`${this.apiUrl}/projets`, dto));
    await this.loadProfile();
  }


  async deleteProject(id: string) {
    await firstValueFrom(this.http.delete(`${this.apiUrl}/projets/${id}`));
    await this.loadProfile();
  }

  async addCertification(c: Certification) {
    const dto = {
      titre: c.name,
      organisation: c.issuer,
      dateObtention: c.date,
      urlCredential: c.verificationUrl
    };
    await firstValueFrom(this.http.post(`${this.apiUrl}/certifications`, dto));
    await this.loadProfile();
  }

  async updateCertification(c: Certification) {
    const dto = {
      id: c.id,
      titre: c.name,
      organisation: c.issuer,
      dateObtention: c.date,
      urlCredential: c.verificationUrl
    };
    await firstValueFrom(this.http.put(`${this.apiUrl}/certifications`, dto));
    await this.loadProfile();
  }


  async deleteCertification(id: string) {
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
   * Calcul du pourcentage de complétion du profil (Granulaire)
   */
  completionPercentage = computed(() => {
    const p = this.profile();
    let score = 0;
    
    // 1. Personal Info (Total: 20%)
    if (p.personal.firstName) score += 4;
    if (p.personal.lastName) score += 4;
    if (p.personal.email) score += 4;
    if (p.personal.phone) score += 4;
    if (p.personal.jobTitle) score += 4;
    
    if (p.education.length > 0) score += 10;
    if (p.experience.length > 0) score += 15;
    if (p.skills.length > 0) score += 10;
    if (p.languages.length > 0) score += 10;
    if (p.resume && p.resume.length > 50) score += 15;
    if (p.projets.length > 0) score += 10;
    if (p.certifications.length > 0) score += 10;

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

  updateProfile(newData: Partial<Profile>) {
    this.profile.update(current => ({ ...current, ...newData }));
  }

  async importResume(file: File): Promise<void> {
    const formData = new FormData();
    formData.append('file', file);

    try {
      console.log('Parsing resume...');
      const data = await firstValueFrom(this.http.post<any>(`${this.apiUrl}/parse-resume`, formData));
      console.log('--- DEBUG: AI DATA RECEIVED ---', data);
      
      if (data) {
        console.log('Parsing successful. Clearing existing data before saving new profile...');
        await firstValueFrom(this.http.delete(`${this.apiUrl}/clear`));

        // 1. Mise à jour et sauvegarde des infos personnelles
        const personal = {
          ...this.profile().personal,
          firstName: data.personal?.prenom || this.profile().personal.firstName,
          lastName: data.personal?.nom || this.profile().personal.lastName,
          email: data.personal?.email || this.profile().personal.email,
          phone: data.personal?.telephone || this.profile().personal.phone,
          jobTitle: data.personal?.titrePoste || this.profile().personal.jobTitle,
          city: data.personal?.ville || this.profile().personal.city,
          country: data.personal?.pays || this.profile().personal.country,
        };
        
        // Mettre à jour le résumé s'il est présent
        if (data.personal?.resumeProfessionnel) {
          this.updateProfile({ resume: data.personal.resumeProfessionnel });
        }

        await this.savePersonalInfo(personal);

        // 2. Ajout des expériences
        if (data.experience && Array.isArray(data.experience)) {
          for (const exp of data.experience) {
            await this.addExperience({
              id: '',
              company: exp.entreprise || '',
              title: exp.poste || '',
              startDate: exp.dateDebut ? exp.dateDebut.substring(0, 7) : '',
              endDate: exp.dateFin ? exp.dateFin.substring(0, 7) : '',
              city: exp.ville || '',
              description: exp.missions || '',
              current: !exp.dateFin,
              type: this.mapExperienceType(exp.type || 'Stage')
            }, false); // Skip intermediate refresh
          }
        }

        // 3. Ajout des formations
        if (data.education && Array.isArray(data.education)) {
          for (const edu of data.education) {
            await this.addEducation({
              id: '',
              institution: edu.etablissement || '',
              degree: edu.diplome || '',
              startYear: edu.annee || '2024',
              endYear: edu.anneeFin || '2024',
              city: edu.ville || '',
              specialization: edu.specialisation || '',
              current: !edu.anneeFin,
              mention: 'Passable'
            }, false); // Skip intermediate refresh
          }
        }

        // 4. Ajout des compétences et langues
        let skillsData = data.skills || data.competences || data.competence;
        
        // Robustesse: Si l'IA renvoie une chaîne de caractères au lieu d'une liste
        if (typeof skillsData === 'string') {
          console.log('AI returned skills as string, converting to list...');
          skillsData = skillsData.split(',').map((s: string) => ({ nom: s.trim(), typeCompetence: 'Technical' }));
        }

        console.log('--- DEBUG: skillsData before processing ---', JSON.stringify(skillsData));

        if (skillsData && Array.isArray(skillsData) && skillsData.length > 0) {
          console.log(`Adding ${skillsData.length} skills/languages...`);
          
          let dbKeywords: any[] = [];
          try { dbKeywords = await this.getKeywords(); } catch (e) { console.warn('Could not load keywords'); }

          for (const skill of skillsData) {
            try {
              const skillName = typeof skill === 'string' ? skill : (skill.nom || skill.name || skill.title || '');
              if (!skillName || !skillName.trim()) {
                console.warn('Skipping empty skill entry:', skill);
                continue;
              }
              
              const typeRaw = (skill.typeCompetence || skill.type || '').toLowerCase();
              const isLangue = typeRaw.includes('lang') || typeRaw.includes('linguist');
              
              const match = dbKeywords.find((k: any) => k.mot?.toLowerCase() === skillName.toLowerCase());
              
              // IMPORTANT: Do NOT send 'id' field - backend expects Guid? and empty string '' causes 400 error
              const dto = {
                nom: match ? match.mot : skillName.trim(),
                niveau: isLangue ? this.mapLevelToInt(skill.niveau || skill.level || 'B1') : 3,
                typeCompetence: isLangue ? 'Langue' : (match ? match.categorie : (skill.typeCompetence || skill.type || 'Technical'))
              };
              
              console.log(`Saving skill: ${dto.nom} (type: ${dto.typeCompetence}, niveau: ${dto.niveau})`);
              const result = await firstValueFrom(this.http.post(`${this.apiUrl}/competences`, dto));
              console.log(`✅ Skill saved successfully: ${dto.nom}`, result);
            } catch (e: any) {
              console.error(`❌ Failed to add skill "${typeof skill === 'string' ? skill : skill?.nom}"`, e?.error || e?.message || e);
            }
          }
        } else {
          console.warn('⚠️ No skills data found in AI response. Keys available:', data ? Object.keys(data) : 'data is null');
        }

        // 5. Ajout des projets
        if (data.projects && Array.isArray(data.projects)) {
          console.log(`Adding ${data.projects.length} projects...`);
          for (const p of data.projects) {
            try {
              await this.addProject({
                id: '',
                title: p.titre || p.title || '',
                description: p.description || '',
                stack: p.technologies ? p.technologies.split(',') : [],
                githubUrl: p.lien || p.link || p.githubUrl || '',
                demoUrl: '',
                isUniversity: false
              });
            } catch (e) { console.error('Failed to add project', p, e); }
          }
        }

        // 5b. Ajout des activités parascolaires (Extracurricular)
        if (data.extracurricular && Array.isArray(data.extracurricular)) {
          for (const ex of data.extracurricular) {
            await this.addExperience({
              id: '',
              company: ex.organisation || '',
              title: ex.titre || '',
              startDate: ex.dateDebut ? ex.dateDebut.substring(0, 7) : '',
              endDate: ex.dateFin ? ex.dateFin.substring(0, 7) : '',
              city: '', // non fourni
              description: ex.description || '',
              current: !ex.dateFin,
              type: 'Extracurricular'
            }, false); // Skip intermediate refresh
          }
        }

        // 6. Ajout des certifications
        if (data.certifications && Array.isArray(data.certifications)) {
          console.log(`Adding ${data.certifications.length} certifications...`);
          for (const c of data.certifications) {
            try {
              await this.addCertification({
                id: '',
                name: c.titre || c.name || c.title || '',
                issuer: c.organisation || c.issuer || '',
                date: c.date || '',
                verificationUrl: c.lien || c.url || c.link || ''
              });
            } catch (e) { console.error('Failed to add certification', c, e); }
          }
        }

        // 7. Rechargement final pour tout synchroniser proprement
        await this.loadProfile();
      }
    } catch (error) {
      console.error('Erreur lors du parsing du CV:', error);
      throw error;
    }
  }

  setStep(stepId: ProfileStepId) {
    this.currentStep.set(stepId);
  }
}
