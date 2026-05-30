import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  EmailService,
  EmailConnectionStatusDto,
  GoogleClientCredentialsSummaryDto
} from '../../../services/email.service';

@Component({
  selector: 'app-gmail-settings',
  standalone: true,
  imports: [CommonModule, FormsModule],
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

  savingCredentials = signal(false);
  deletingCredentials = signal(false);
  credentials = signal<GoogleClientCredentialsSummaryDto | null>(null);

  clientId = '';
  clientSecret = '';
  redirectUri = '';

  ngOnInit() {
    this.checkStatus();
    this.loadCredentialSummary();
  }

  checkStatus() {
    this.loading.set(true);
    this.error.set(null);
    this.emailService.getGmailStatus().subscribe({
      next: (s) => { this.status.set(s); this.loading.set(false); },
      error: () => { this.error.set('Impossible de recuperer le statut Gmail.'); this.loading.set(false); }
    });
  }

  verifyStatus() {
    if (this.verifying()) return;
    this.verifying.set(true);
    this.error.set(null);
    this.emailService.verifyGmailConnection().subscribe({
      next: (s) => { this.status.set(s); this.verifying.set(false); },
      error: () => { this.error.set('La verification a echoue.'); this.verifying.set(false); }
    });
  }

  loadCredentialSummary() {
    this.emailService.getGoogleClientCredentialsSummary().subscribe({
      next: (summary) => {
        this.credentials.set(summary);
        if (summary?.redirectUri) {
          this.redirectUri = summary.redirectUri;
        }
      },
      error: () => {
        this.credentials.set(null);
      }
    });
  }

  saveCredentials() {
    const clientId = this.clientId.trim();
    const clientSecret = this.clientSecret.trim();
    const redirectUri = this.redirectUri.trim();

    if (!clientId || !clientSecret) {
      this.error.set('Client ID et Client Secret sont obligatoires.');
      return;
    }

    this.savingCredentials.set(true);
    this.error.set(null);
    this.emailService.saveGoogleClientCredentials({
      clientId,
      clientSecret,
      redirectUri: redirectUri || null
    }).subscribe({
      next: () => {
        this.clientSecret = '';
        this.savingCredentials.set(false);
        this.loadCredentialSummary();
      },
      error: (err) => {
        this.error.set(err?.error?.error || 'Impossible d enregistrer les credentials OAuth.');
        this.savingCredentials.set(false);
      }
    });
  }

  deleteCredentials() {
    if (this.deletingCredentials()) return;
    if (!confirm('Supprimer les credentials OAuth personnalises pour Gmail ?')) return;

    this.deletingCredentials.set(true);
    this.error.set(null);
    this.emailService.deleteGoogleClientCredentials().subscribe({
      next: () => {
        this.credentials.set({
          hasCredentials: false,
          clientIdMasked: null,
          usesCustomRedirectUri: false,
          redirectUri: null,
          updatedAtUtc: null
        });
        this.clientId = '';
        this.clientSecret = '';
        this.redirectUri = '';
        this.deletingCredentials.set(false);
      },
      error: () => {
        this.error.set('Impossible de supprimer les credentials OAuth.');
        this.deletingCredentials.set(false);
      }
    });
  }

  connectGmail() {
    if (this.connecting()) return;
    this.connecting.set(true);
    this.error.set(null);
    this.emailService.getGmailLoginUrl().subscribe({
      next: (res) => {
        if (res?.url) window.location.href = res.url;
        else {
          this.error.set('URL de connexion invalide.');
          this.connecting.set(false);
        }
      },
      error: (err) => {
        this.error.set(err?.error?.error || 'Impossible d obtenir le lien de connexion Gmail.');
        this.connecting.set(false);
      }
    });
  }

  disconnectGmail() {
    if (this.disconnecting() || !confirm('Etes-vous sur de vouloir deconnecter votre compte Gmail ?')) return;
    this.disconnecting.set(true);
    this.error.set(null);
    this.emailService.disconnectGmail().subscribe({
      next: () => {
        this.status.set({ isConnected: false, isTokenValid: false, errorMessage: null, emailAddress: null, provider: 'Gmail' });
        this.disconnecting.set(false);
      },
      error: () => { this.error.set('Erreur lors de la deconnexion.'); this.disconnecting.set(false); }
    });
  }
}
