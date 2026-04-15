import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ProfileService, FullProfile } from '../../services/profile.service';
import { OnboardingService } from '../../services/onboarding.service';
import { OBJECTIF_LABELS, NIVEAU_LABELS, SECTEUR_LABELS } from '../../core/auth/models/user-profile.model';
import { Router } from '@angular/router';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './profile.component.html',
  styleUrl: './profile.component.scss'
})
export class ProfileComponent implements OnInit {
  private readonly profileService = inject(ProfileService);
  private readonly onboardingService = inject(OnboardingService);
  private readonly router = inject(Router);

  profile = signal<FullProfile | null>(null);
  profileScore = signal(0);
  isLoaded = signal(false);

  // Labels for display (cast to any Record to allow string indexing in template)
  objectifLabels: Record<string, string> = OBJECTIF_LABELS;
  niveauLabels: Record<string, string> = NIVEAU_LABELS;
  secteurLabels: Record<string, string> = SECTEUR_LABELS;

  ngOnInit(): void {
    this.refreshProfile();
  }

  refreshProfile(): void {
    this.profileService.getFullProfile().subscribe(data => {
      this.profile.set(data);
      this.isLoaded.set(true);
    });
    this.onboardingService.getStatus().subscribe(status => {
      this.profileScore.set(status.profileScore);
    });
  }

  goBack(): void {
    this.router.navigate(['/dashboard']);
  }

  updatePersonalInfo(data: any): void {
    this.profileService.updatePersonalInfo(data).subscribe(() => this.refreshProfile());
  }

  addExperience(): void { console.log('Add Experience'); }
  addProject(): void { console.log('Add Project'); }
  addSkill(): void { console.log('Add Skill'); }
  addEducation(): void { console.log('Add Education'); }
}
