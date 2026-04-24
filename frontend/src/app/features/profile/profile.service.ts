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
      }
    ],
    experience: [],
    skills: [],
    languages: [],
    resume: '',
    projets: [],
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
