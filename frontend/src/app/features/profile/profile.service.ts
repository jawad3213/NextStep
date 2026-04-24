import { Injectable, signal, computed } from '@angular/core';
import { Profile, ProfileStepId } from './profile.types';

@Injectable({
  providedIn: 'root'
})
export class ProfileService {
  // Main Profile State
  profile = signal<Profile>({
    personal: {
      firstName: 'Said',
      lastName: 'N.',
      email: 'said.n@example.com',
      phone: '+212 600 000 000',
      jobTitle: 'Senior Fullstack Developer',
      address: 'Casablanca, Maroc',
      city: 'Casablanca',
      country: 'Maroc',
      linkedinUrl: '',
      githubUrl: '',
      photoUrl: null,
      useAsHeadline: true
    },
    education: [
      {
        id: '1',
        degree: "Cycle d'Ingénieur",
        institution: 'École Nationale des Sciences Appliquées (ENSA)',
        city: 'Tanger, Maroc',
        startYear: '2024',
        endYear: '2024',
        current: false,
        specialization: 'Génie Informatique',
        mention: 'Très bien'
      },
      {
        id: '2',
        degree: "Classes Préparatoires Intégrées",
        institution: 'École Nationale des Sciences Appliquées (ENSA)',
        city: 'Tanger, Maroc',
        startYear: '2022',
        endYear: '2024',
        current: false,
        specialization: 'Mathématiques et Physique',
        mention: 'Bien'
      }
    ],
    experience: [
      {
        id: '1',
        title: 'Fullstack Software Engineer',
        company: 'Tech Innovators',
        city: 'Casablanca, Maroc',
        startDate: '2023-09',
        endDate: '',
        current: true,
        type: 'CDI',
        description: 'Développement complet d\'une plateforme SaaS B2B. Mise en place de l\'architecture microservices, optimisation des requêtes de base de données et création d\'interfaces dynamiques avec Angular.'
      },
      {
        id: '2',
        title: 'Développeur Front-End (Stage)',
        company: 'Digital Solutions',
        city: 'Rabat, Maroc',
        startDate: '2023-04',
        endDate: '2023-08',
        current: false,
        type: 'Stage',
        description: 'Conception et développement de dashboards interactifs. Refonte de l\'interface utilisateur pour améliorer l\'UX.'
      }
    ],
    skills: [
      { id: '1', name: 'Angular', category: 'Frontend' },
      { id: '2', name: 'TypeScript', category: 'Frontend' },
      { id: '3', name: 'Node.js', category: 'Backend' }
    ],
    languages: [],
    resume: '',
    projets: [
      {
        id: '1',
        title: 'Vimo Platform - Netflix Clone',
        description: 'Plateforme de streaming vidéo cloud-native avec architecture de transcodage, lecteur HTML5 personnalisé et back-office d\'administration complet.',
        stack: ['Angular', 'Node.js', 'MinIO', 'Docker'],
        githubUrl: 'https://github.com/said/vimo-platform',
        demoUrl: 'https://vimo.app',
        isUniversity: false
      },
      {
        id: '2',
        title: 'MediConnect',
        description: 'Système de gestion hospitalière et portail patient permettant la prise de rendez-vous sécurisée et le suivi du dossier médical.',
        stack: ['Spring Boot', 'PostgreSQL', 'Angular', 'Hibernate'],
        githubUrl: 'https://github.com/said/mediconnect',
        demoUrl: '',
        isUniversity: true
      }
    ],
    certifications: []
  });

  currentStep = signal<ProfileStepId>('coordonnees');

  // Computed Progress
  completionPercentage = computed(() => {
    let filledSections = 0;
    const p = this.profile();
    
    if (p.personal.firstName && p.personal.lastName && p.personal.email) filledSections++;
    if (p.education.length > 0) filledSections++;
    if (p.experience.length > 0) filledSections++;
    if (p.skills.length > 0) filledSections++;
    if (p.resume.length > 50) filledSections++;
    if (p.projets.length > 0) filledSections++;
    if (p.certifications.length > 0) filledSections++;

    return Math.round((filledSections / 7) * 100);
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
    // In a real app: debounce then PUT to /api/profile
  }

  setStep(stepId: ProfileStepId) {
    this.currentStep.set(stepId);
  }
}
