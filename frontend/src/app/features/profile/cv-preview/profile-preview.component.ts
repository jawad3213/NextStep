import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { ProfileService } from '../profile.service';

@Component({
  selector: 'app-profile-preview',
  standalone: true,
  imports: [CommonModule, MatIconModule, MatSelectModule],
  templateUrl: './profile-preview.component.html',
  styleUrl: './profile-preview.component.scss'
})
export class ProfilePreviewComponent {
  private profileService = inject(ProfileService);
  
  profile = this.profileService.profile;
  currentStep = this.profileService.currentStep;
  
  selectedTemplate = 'modern';
  templates = [
    { id: 'modern', label: 'Modern' },
    { id: 'classic', label: 'Classic' },
    { id: 'creative', label: 'Creative' }
  ];

  atsScore = 75; // Mock score for now

  get atsColorClass(): string {
    if (this.atsScore > 70) return 'text-success bg-success-bg';
    if (this.atsScore > 40) return 'text-warning bg-warning-bg';
    return 'text-danger bg-danger-bg';
  }
}
