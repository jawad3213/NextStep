import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { EmailService, EmailConnectionStatusDto } from '../../../services/email.service';

@Component({
  selector: 'app-gmail-settings',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './gmail-settings.component.html',
  styleUrl: './gmail-settings.component.scss'
})
export class GmailSettingsComponent implements OnInit {
  private readonly emailService = inject(EmailService);

  loading = signal(true);
  connecting = signal(false);
  error = signal<string | null>(null);
  status = signal<EmailConnectionStatusDto | null>(null);
  disconnecting = signal(false);
  verifying = signal(false);

  ngOnInit() {
    this.checkStatus();
  }

  checkStatus() {
    this.loading.set(true);
    this.error.set(null);
    this.emailService.getGmailStatus().subscribe({
      next: (s) => { this.status.set(s); this.loading.set(false); },
      error: () => { this.error.set('Impossible de récupérer le statut Gmail.'); this.loading.set(false); }
    });
  }

  verifyStatus() {
    if (this.verifying()) return;
    this.verifying.set(true);
    this.error.set(null);
    this.emailService.verifyGmailConnection().subscribe({
      next: (s) => { this.status.set(s); this.verifying.set(false); },
      error: () => { this.error.set('La vérification a échoué.'); this.verifying.set(false); }
    });
  }

  connectGmail() {
    if (this.connecting()) return;
    this.connecting.set(true);
    this.error.set(null);
    this.emailService.getGmailLoginUrl().subscribe({
      next: (res) => {
        if (res?.url) window.location.href = res.url;
        else { this.error.set('URL de connexion invalide.'); this.connecting.set(false); }
      },
      error: () => { this.error.set('Impossible d\'obtenir le lien de connexion Gmail.'); this.connecting.set(false); }
    });
  }

  disconnectGmail() {
    if (this.disconnecting() || !confirm('Êtes-vous sûr de vouloir déconnecter votre compte Gmail ?')) return;
    this.disconnecting.set(true);
    this.error.set(null);
    this.emailService.disconnectGmail().subscribe({
      next: () => {
        this.status.set({ isConnected: false, isTokenValid: false, errorMessage: null, emailAddress: null, provider: 'Gmail' });
        this.disconnecting.set(false);
      },
      error: () => { this.error.set('Erreur lors de la déconnexion.'); this.disconnecting.set(false); }
    });
  }
}
