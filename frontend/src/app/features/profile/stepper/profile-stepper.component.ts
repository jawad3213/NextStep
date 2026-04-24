import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { ProfileService } from '../profile.service';
import { ProfileStepId } from '../profile.types';

@Component({
  selector: 'app-profile-stepper',
  standalone: true,
  imports: [CommonModule, MatIconModule],
  templateUrl: './profile-stepper.component.html',
  styleUrl: './profile-stepper.component.scss'
})
export class ProfileStepperComponent {
  private profileService = inject(ProfileService);
  
  steps: { id: ProfileStepId, label: string, icon: string }[] = [
    { id: 'coordonnees', label: 'Coordonnées', icon: 'person' },
    { id: 'formation', label: 'Formation', icon: 'school' },
    { id: 'experience', label: 'Expérience', icon: 'work' },
    { id: 'competences', label: 'Compétences', icon: 'bolt' },
    { id: 'resume', label: 'Résumé', icon: 'article' },
    { id: 'projets', label: 'Projets', icon: 'code' },
    { id: 'certifications', label: 'Certifications', icon: 'verified' }
  ];

  currentStep = this.profileService.currentStep;
  completionPercentage = this.profileService.completionPercentage;

  setStep(id: ProfileStepId) {
    this.profileService.setStep(id);
  }

  isComplete(id: ProfileStepId) {
    return this.profileService.isSectionComplete(id);
  }
}
