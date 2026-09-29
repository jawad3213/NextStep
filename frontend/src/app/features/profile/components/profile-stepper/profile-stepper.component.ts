import { Component, inject, computed, effect, signal, Output, EventEmitter } from '@angular/core';
import { Router, ActivatedRoute } from '@angular/router';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { ProfileService } from '../../data-access/profile.service';
import { ProfileStepId } from '../../data-access/profile.models';

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
    { id: 'projets', label: 'Projects', icon: 'rocket_launch' },
    { id: 'resume', label: 'Summary', icon: 'description' },
    { id: 'certifications', label: 'Certifications', icon: 'workspace_premium' }
  ];

  currentStep = this.profileService.currentStep;
  completionPercentage = this.profileService.completionPercentage;

  /**
   * Off until one frame after the profile first loads, then stays on. Lets the node
   * circles/progress bar render their real (often already-completed) state instantly on
   * page open instead of visibly transitioning through it — CSS transitions only kick in
   * for genuine later changes (the user completing a section while on the page).
   */
  readonly transitionsEnabled = signal(false);

  constructor() {
    effect((onCleanup) => {
      if (!this.profileService.profileLoaded()) return;
      const frame = requestAnimationFrame(() => this.transitionsEnabled.set(true));
      onCleanup(() => cancelAnimationFrame(frame));
    });
  }

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
