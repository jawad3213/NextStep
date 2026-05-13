import { Component, inject, computed, Output, EventEmitter } from '@angular/core';
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
  @Output() uploadResume = new EventEmitter<void>();
  @Output() importLinkedIn = new EventEmitter<void>();
  private readonly profileService = inject(ProfileService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  
  steps: { id: ProfileStepId, label: string, icon: string }[] = [
    { id: 'coordonnees', label: 'Contact', icon: 'contact_mail' },
    { id: 'experience', label: 'Experience', icon: 'business_center' },
    { id: 'formation', label: 'Education', icon: 'school' },
    { id: 'competences', label: 'Skills', icon: 'psychology' },
    { id: 'resume', label: 'Summary', icon: 'description' },
    { id: 'certifications', label: 'Certifications', icon: 'workspace_premium' }
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
