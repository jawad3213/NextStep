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
      formation: 'Formation',
      experience: 'Expérience Professionnelle',
      competences: 'Compétences',
      projets: 'Projets Personnels',
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
        education: (data.formations || []).map((f: any) => ({
          id: f.id,
          degree: f.diplome,
          institution: f.etablissement,
          startYear: f.annee?.toString() || '2024',
          endYear: f.anneeFin?.toString() || '2024',
          current: !f.anneeFin,
          specialization: f.specialisation || '',
          mention: f.mention || 'Passable',
          city: f.ville || ''
        })),
        experience: (data.experiences || []).map((e: any) => ({
          id: e.id,
          title: e.poste,
          company: e.entreprise,
          startDate: e.dateDebut ? e.dateDebut.substring(0, 7) : '',
          endDate: e.dateFin ? e.dateFin.substring(0, 7) : '',
          current: !e.dateFin,
          description: e.missions || '',
          city: e.ville || '',
          type: e.type || 'Stage'
        })),
        skills: (data.competences || []).map((c: any) => ({
          id: c.id,
          name: c.nom,
          category: c.typeCompetence || 'Technique'
        })),
        languages: [],
        resume: data.personalInfo.resumeProfessionnel || '',
        projets: (data.projets || []).map((p: any) => ({
          id: p.id,
          title: p.titreProjet,
          description: p.description,
          stack: p.technologiesUtilisees?.split(',') || [],
          githubUrl: p.lienProjet || '',
          demoUrl: p.demoUrl || '',
          imageUrl: p.imageUrl || '',
          isUniversity: p.isUniversity || false
        })),
        certifications: (data.certifications || []).map((c: any) => ({
          id: c.id,
          name: c.titre,
          issuer: c.organisation,
          date: c.dateObtention,
          verificationUrl: c.urlCredential
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

  async addExperience(exp: Experience) {
    const dto = {
      entreprise: exp.company,
      poste: exp.title,
      dateDebut: exp.startDate ? new Date(exp.startDate + '-01').toISOString() : null,
      dateFin: exp.endDate ? new Date(exp.endDate + '-01').toISOString() : null,
      missions: exp.description,
      ville: exp.city,
      type: exp.type
    };
    await firstValueFrom(this.http.post(`${this.apiUrl}/experiences`, dto));
    await this.loadProfile();
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

  async addEducation(edu: Education) {
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
    await this.loadProfile();
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
      return 'Erreur de génération.';
    }
  }

  /**
   * Calcul du pourcentage de complétion du profil (Granulaire)
   */
  completionPercentage = computed(() => {
    const p = this.profile();
    let score = 0;
    
    // 1. Infos Personnelles (Total: 25%)
    if (p.personal.firstName) score += 5;
    if (p.personal.lastName) score += 5;
    if (p.personal.email) score += 5;
    if (p.personal.phone) score += 5;
    if (p.personal.jobTitle) score += 5;
    
    // 2. Sections (Total: 75%)
    if (p.education.length > 0) score += 15;
    if (p.experience.length > 0) score += 15;
    if (p.skills.length > 0) score += 15;
    if (p.resume && p.resume.length > 50) score += 15;
    if (p.projets.length > 0) score += 15;

    return Math.min(score, 100);
  });

  isSectionComplete(stepId: ProfileStepId): boolean {
    const p = this.profile();
    switch(stepId) {
      case 'coordonnees': return !!(p.personal.firstName && p.personal.lastName && p.personal.email);
      case 'formation': return p.education.length > 0;
      case 'experience': return p.experience.length > 0;
      case 'competences': return p.skills.length > 0;
      case 'resume': return p.resume.length > 50;
      case 'projets': return p.projets.length > 0;
      case 'certifications': return p.certifications.length > 0;
      default: return false;
    }
  }

  updateProfile(newData: Partial<Profile>) {
    this.profile.update(current => ({ ...current, ...newData }));
  }

  setStep(stepId: ProfileStepId) {
    this.currentStep.set(stepId);
  }
}
