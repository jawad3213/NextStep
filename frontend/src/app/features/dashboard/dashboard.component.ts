import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import Keycloak from 'keycloak-js';
import { IdentityService } from '../../services/identity.service';
import { UserProfile } from '../../core/auth/models/user-profile.model';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.css',
})
export class DashboardComponent implements OnInit {
  private readonly keycloak = inject(Keycloak);
  private readonly identityService = inject(IdentityService);


  userProfile = signal<UserProfile | null>(null);
  isLoading = signal<boolean>(true);
  errorMessage = signal<string | null>(null);

  ngOnInit(): void {
    this.loadProfile();
  }


  private loadProfile(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.identityService.getProfile().subscribe({
      next: (response) => {
        this.userProfile.set(response.data);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Erreur lors du chargement du profil :', err);
        this.errorMessage.set('Impossible de charger votre profil. Veuillez réessayer.');
        this.isLoading.set(false);
      },
    });
  }

  getInitials(): string {
    const profile = this.userProfile();
    if (!profile) return '?';
    const first = profile.prenom?.charAt(0)?.toUpperCase() ?? '';
    const last = profile.nom?.charAt(0)?.toUpperCase() ?? '';
    return first + last || profile.email.charAt(0).toUpperCase();
  }

  async logout(): Promise<void> {
    await this.keycloak.logout({
      redirectUri: window.location.origin,
    });
  }
}
