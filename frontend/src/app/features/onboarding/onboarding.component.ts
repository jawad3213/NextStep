import { Component, inject, signal } from '@angular/core';
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
export class OnboardingComponent {
  private readonly onboardingService = inject(OnboardingService);
  private readonly router = inject(Router);

  currentStep = signal(1);
  isSubmitting = signal(false);
  isLoading = signal(true);

  constructor() {
    // Simulate loading for premium feel
    setTimeout(() => this.isLoading.set(false), 1200);
  }

  onboardingData: SoftOnboardingPayload = {
    objectif: 'CDI',
    niveau: 'BAC_PLUS_5',
    secteur: 'INFORMATIQUE'
  };

  objectifOptions = Object.entries(OBJECTIF_LABELS) as [ObjectifEnum, string][];
  niveauOptions = Object.entries(NIVEAU_LABELS) as [NiveauEnum, string][];
  secteurOptions = Object.entries(SECTEUR_LABELS) as [SecteurEnum, string][];

  // Icons for enums
  objectifIcons: Record<ObjectifEnum, string> = {
    STAGE: '🎓',
    ALTERNANCE: '🔄',
    PREMIER_EMPLOI: '🚀',
    CDI: '💼',
    FREELANCE: '🌐'
  };

  niveauIcons: Record<NiveauEnum, string> = {
    BAC: '📜',
    BAC_PLUS_2: '📚',
    BAC_PLUS_3: '⚖️',
    BAC_PLUS_5: '🏛️',
    DOCTORAT: '🧬'
  };

  secteurIcons: Record<SecteurEnum, string> = {
    INFORMATIQUE: '💻',
    FINANCE: '💰',
    MARKETING: '📈',
    SANTE: '🏥',
    INGENIERIE: '⚙️',
    DROIT: '⚖️',
    EDUCATION: '📖',
    COMMERCE: '🛒',
    DESIGN: '🎨',
    COMMUNICATION: '📣',
    RESSOURCES_HUMAINES: '👥',
    AUTRE: '✨'
  };

  selectObjectif(val: ObjectifEnum) {
    this.onboardingData.objectif = val;
    this.nextStep();
  }

  selectNiveau(val: NiveauEnum) {
    this.onboardingData.niveau = val;
    this.nextStep();
  }

  selectSecteur(val: SecteurEnum) {
    this.onboardingData.secteur = val;
  }

  nextStep() {
    if (this.currentStep() < 3) {
      this.currentStep.set(this.currentStep() + 1);
    }
  }

  prevStep() {
    if (this.currentStep() > 1) {
      this.currentStep.set(this.currentStep() - 1);
    }
  }

  finish() {
    this.isSubmitting.set(true);
    this.onboardingService.submitSoftOnboarding(this.onboardingData).subscribe({
      next: () => {
        this.router.navigate(['/dashboard']);
      },
      error: (err) => {
        console.error('Onboarding failed', err);
        this.isSubmitting.set(false);
      }
    });
  }
}
