import { Component, inject, computed } from '@angular/core';
import { Router, ActivatedRoute } from '@angular/router';
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
  private readonly profileService = inject(ProfileService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  
  steps: { id: ProfileStepId, label: string, icon: string }[] = [
    { id: 'coordonnees', label: 'Contact', icon: 'person' },
    { id: 'experience', label: 'Experience', icon: 'work' },
    { id: 'formation', label: 'Education', icon: 'school' },
    { id: 'competences', label: 'Skills', icon: 'bolt' },
    { id: 'resume', label: 'Summary', icon: 'article' },
    { id: 'projets', label: 'Projects', icon: 'code' },
    { id: 'certifications', label: 'Certifications', icon: 'verified' }
  ];

  currentStep = this.profileService.currentStep;
  completionPercentage = this.profileService.completionPercentage;

  currentIndex = computed(() => this.steps.findIndex(s => s.id === this.currentStep()));

  setStep(id: ProfileStepId) {
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { step: id },
      queryParamsHandling: 'merge'
    });
  }

  isComplete(id: ProfileStepId) {
    return this.profileService.isSectionComplete(id);
  }
}
