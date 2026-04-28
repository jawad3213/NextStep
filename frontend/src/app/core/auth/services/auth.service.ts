import { Injectable, inject, signal } from '@angular/core';
import Keycloak from 'keycloak-js';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly keycloak = inject(Keycloak);
  
  user = signal<{firstName?: string, lastName?: string, email?: string} | null>(null);

  constructor() {
    this.init();
  }

  private async init() {
    if (this.isAuthenticated()) {
      const profile = await this.keycloak.loadUserProfile();
      this.user.set({
        firstName: profile.firstName,
        lastName: profile.lastName,
        email: profile.email
      });
    }
  }

  /**
   * Log in using the standard Keycloak flow (shows the login page)
   */
  login() {
    return this.keycloak.login();
  }

  /**
   * Log in directly with Google (bypasses the Keycloak login selection)
   * This implements Step 3 of the integration guide.
   */
  loginWithGoogle() {
    return this.keycloak.login({
      idpHint: 'google'
    });
  }

  /**
   * Log in directly with GitHub
   */
  loginWithGithub() {
    return this.keycloak.login({
      idpHint: 'github'
    });
  }

  /**
   * Log out and redirect to home
   */
  logout() {
    return this.keycloak.logout({
      redirectUri: window.location.origin
    });
  }

  /**
   * Check if user is authenticated
   */
  isAuthenticated(): boolean {
    return !!this.keycloak.authenticated;
  }

  /**
   * Get the token for API calls
   */
  getToken(): string | undefined {
    return this.keycloak.token;
  }
}
