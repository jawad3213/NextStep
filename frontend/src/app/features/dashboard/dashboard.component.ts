import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import Keycloak from 'keycloak-js';
import { IdentityService } from '../../services/identity.service';
import { OnboardingService } from '../../services/onboarding.service';
import {
  UserProfile,
  ProfileStatus,
} from '../../core/auth/models/user-profile.model';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
})
export class DashboardComponent implements OnInit {
  private readonly keycloak = inject(Keycloak);
  private readonly identityService = inject(IdentityService);
  private readonly onboardingService = inject(OnboardingService);
  private readonly router = inject(Router);

  userProfile = signal<UserProfile | null>(null);
  profileStatus = signal<ProfileStatus | null>(null);
  isLoading = signal(true);

  ngOnInit(): void {
    this.loadProfile();
    this.loadProfileStatus();
  }

  private loadProfile(): void {
    this.isLoading.set(true);
    this.identityService.getProfile().subscribe({
      next: (response) => {
        this.userProfile.set(response.data);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }

  private loadProfileStatus(): void {
    this.onboardingService.getProfileStatus().subscribe({
      next: (status) => this.profileStatus.set(status),
    });
  }

  goToProfile(): void {
    this.router.navigate(['/profile']);
  }

  getInitials(): string {
    const profile = this.userProfile();
    if (!profile) return '?';
    const first = profile.prenom?.charAt(0)?.toUpperCase() ?? '';
    const last = profile.nom?.charAt(0)?.toUpperCase() ?? '';
    return first + last || profile.email.charAt(0).toUpperCase();
  }

  getGreeting(): string {
    const hour = new Date().getHours();
    if (hour < 12) return 'Bonjour';
    if (hour < 18) return 'Bon après-midi';
    return 'Bonsoir';
  }

  async logout(): Promise<void> {
    await this.keycloak.logout({
      redirectUri: window.location.origin,
    });
  }
}
