import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { OnboardingService } from '../../services/onboarding.service';
import { FormsModule } from '@angular/forms';
import { 
  ObjectifEnum, 
  NiveauEnum, 
  SecteurEnum, 
  OBJECTIF_LABELS, 
  NIVEAU_LABELS, 
  SECTEUR_LABELS,
  SoftOnboardingPayload
} from '../../core/auth/models/user-profile.model';

@Component({
  selector: 'app-onboarding',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './onboarding.component.html',
  styleUrl: './onboarding.component.scss'
})
export class OnboardingComponent implements OnInit {
  private readonly onboardingService = inject(OnboardingService);
  private readonly router = inject(Router);
  private readonly STORAGE_KEY = 'nextstep_onboarding_temp';

  isSubmitting = signal(false);
  currentYear = new Date().getFullYear();

  // Progressive reveal: tracks which sections are visible
  showSection2 = signal(false);
  showSection3 = signal(false);

  onboardingData: SoftOnboardingPayload = {
    objectif: '' as any,
    niveau: '' as any,
    secteur: '' as any
  };

  objectifOptions = Object.entries(OBJECTIF_LABELS) as [ObjectifEnum, string][];
  niveauOptions = Object.entries(NIVEAU_LABELS) as [NiveauEnum, string][];
  secteurOptions = Object.entries(SECTEUR_LABELS) as [SecteurEnum, string][];

  ngOnInit() {
    this.loadPersistedData();
  }

  private saveToStorage() {
    localStorage.setItem(this.STORAGE_KEY, JSON.stringify(this.onboardingData));
  }

  private loadPersistedData() {
    const saved = localStorage.getItem(this.STORAGE_KEY);
    if (saved) {
      try {
        const data = JSON.parse(saved);
        this.onboardingData = data;
        
        // Restore visibility based on data
        if (this.onboardingData.objectif) this.showSection2.set(true);
        if (this.onboardingData.niveau) this.showSection3.set(true);
      } catch (e) {
        console.error('Failed to parse saved onboarding data', e);
      }
    }
  }

  selectObjectif(val: ObjectifEnum) {
    this.onboardingData.objectif = val;
    this.saveToStorage();
    // Reveal section 2 with a slight delay for smooth animation
    setTimeout(() => this.showSection2.set(true), 150);
  }

  selectNiveau(val: NiveauEnum) {
    this.onboardingData.niveau = val;
    this.saveToStorage();
    // Reveal section 3
    setTimeout(() => this.showSection3.set(true), 150);
  }

  selectSecteur(val: SecteurEnum) {
    this.onboardingData.secteur = val;
    this.saveToStorage();
  }

  finish() {
    this.isSubmitting.set(true);
    this.onboardingService.submitSoftOnboarding(this.onboardingData).subscribe({
      next: () => {
        localStorage.removeItem(this.STORAGE_KEY);
        localStorage.setItem('nextstep_soft_onboarding_done', 'true');
        this.router.navigate(['/profile'], {
          queryParams: { step: 'coordonnees' }
        });
      },
      error: (err) => {
        console.error('Onboarding failed', err);
        this.isSubmitting.set(false);
      }
    });
  }
}
