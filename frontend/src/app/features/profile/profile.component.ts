import { Component, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ProfileService } from './profile.service';
import { ProfileStepId } from './profile.types';

// Sub-components
import { ProfileStepperComponent } from './stepper/profile-stepper.component';
import { ProfilePreviewComponent } from './cv-preview/profile-preview.component';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [
    CommonModule,
    MatIconModule,
    MatTooltipModule,
    ProfileStepperComponent,
    ProfilePreviewComponent
  ],
  templateUrl: './profile.component.html',
  styleUrl: './profile.component.scss'
})
export class ProfileComponent {
  profileService = inject(ProfileService);
  
  // UI State
  isPreviewOpen = signal(false);
  isSaving = signal(false);
  lastSaved = signal<Date | null>(new Date());
  showToast = signal(false);
  
  // Section States
  isAddingFormation = signal(false);
  
  profile = this.profileService.profile;
  currentStep = this.profileService.currentStep;
  
  steps: { id: ProfileStepId, label: string }[] = [
    { id: 'coordonnees', label: 'Coordonnées' },
    { id: 'formation', label: 'Formation' },
    { id: 'experience', label: 'Expérience' },
    { id: 'competences', label: 'Compétences' },
    { id: 'resume', label: 'Résumé' },
    { id: 'projets', label: 'Projets' },
    { id: 'certifications', label: 'Certifications' }
  ];

  currentIndex = computed(() => this.steps.findIndex(s => s.id === this.currentStep()));
  
  nextStepName = computed(() => {
    const nextIdx = this.currentIndex() + 1;
    return nextIdx < this.steps.length ? this.steps[nextIdx].label : 'Terminer';
  });

  // Section Avancement (Progress in active section)
  sectionProgress = signal(45); // Mock 45% progress in current section

  // Step Logic
  goToStep(id: ProfileStepId) {
    this.profileService.setStep(id);
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  next() {
    const nextIdx = this.currentIndex() + 1;
    if (nextIdx < this.steps.length) {
      this.goToStep(this.steps[nextIdx].id);
    }
  }

  previous() {
    const prevIdx = this.currentIndex() - 1;
    if (prevIdx >= 0) {
      this.goToStep(this.steps[prevIdx].id);
    }
  }

  updateField(field: string, value: any) {
    this.isSaving.set(true);
    this.profileService.updateProfile({ 
      personal: { ...this.profile().personal, [field]: value } 
    });
    
    // Simulate auto-save feedback
    setTimeout(() => {
      this.isSaving.set(false);
      this.lastSaved.set(new Date());
    }, 1000);
  }

  togglePreview() {
    this.isPreviewOpen.update(v => !v);
  }

  save() {
    this.isSaving.set(true);
    // Simulate backend save
    setTimeout(() => {
      this.isSaving.set(false);
      this.lastSaved.set(new Date());
      this.showToast.set(true);
      
      // Hide toast after 3s
      setTimeout(() => {
        this.showToast.set(false);
      }, 3000);
    }, 600);
  }


  // LinkedIn Import State
  showImportBlock = signal(true);
  importLinkedIn() {
    // Simulate import
    setTimeout(() => this.showImportBlock.set(false), 800);
  }

  // Formation Methods
  toggleAddFormation() {
    this.isAddingFormation.update(v => !v);
  }

  saveFormation() {
    this.isSaving.set(true);
    setTimeout(() => {
      this.isSaving.set(false);
      this.isAddingFormation.set(false);
      this.showToast.set(true);
      setTimeout(() => this.showToast.set(false), 3000);
    }, 600);
  }
}
