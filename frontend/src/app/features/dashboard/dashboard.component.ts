import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import Keycloak from 'keycloak-js';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.css'
})
export class DashboardComponent {
  private keycloak = inject(Keycloak);

  async logout() {
    await this.keycloak.logout({
      redirectUri: window.location.origin
    });
  }
}
// Force trigger rebuild for Docker watcher
