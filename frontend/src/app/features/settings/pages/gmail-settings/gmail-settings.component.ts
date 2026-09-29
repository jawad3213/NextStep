import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { extractApiError } from '@core/http/extract-api-error';
import { ToastService } from '@core/notifications/toast.service';
import { EmailConnectionStatusDto, GoogleClientCredentialsSummaryDto } from '@features/applications/data-access/email.models';
import { EmailService } from '@features/applications/data-access/email.service';

/** Longest OAuth error message shown from the URL (it comes from our backend, but the URL is editable). */
const MAX_OAUTH_MESSAGE_LENGTH = 300;

@Component({
  selector: 'app-gmail-settings',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './gmail-settings.component.html',
  styleUrl: './gmail-settings.component.scss'
})
export class GmailSettingsComponent implements OnInit {
  private readonly emailService = inject(EmailService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  loading = signal(true);
  connecting = signal(false);
  error = signal<string | null>(null);
  success = signal<string | null>(null);
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
    this.readOAuthResult();
    this.checkStatus();
    this.loadCredentialSummary();
  }

  /** Google sends the browser back here with ?gmailOAuth=success|error&gmailError=... */
  private readOAuthResult() {
    const params = this.route.snapshot.queryParamMap;
    const result = params.get('gmailOAuth');
    if (!result) return;

    if (result === 'success') {
      this.success.set('Gmail connected. You can now send application emails from NextStep.');
      this.toast.success('Gmail connected.');
    } else {
      const message = (params.get('gmailError') || 'The Gmail connection failed. Please try again.')
        .slice(0, MAX_OAUTH_MESSAGE_LENGTH);
      this.error.set(message);
    }

    // Remove the parameters so a refresh does not show the message again.
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { gmailOAuth: null, gmailError: null },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  checkStatus() {
    this.loading.set(true);
    this.emailService.getGmailStatus().subscribe({
      next: (s) => { this.status.set(s); this.loading.set(false); },
      error: (err) => {
        this.error.set(extractApiError(err).message || 'Unable to load the Gmail connection status.');
        this.loading.set(false);
      }
    });
  }

  verifyStatus() {
    if (this.verifying()) return;
    this.verifying.set(true);
    this.error.set(null);
    this.success.set(null);
    this.emailService.verifyGmailConnection().subscribe({
      next: (s) => {
        this.status.set(s);
        this.verifying.set(false);
        if (s.isConnected && s.isTokenValid) this.toast.success('Gmail connection is working.');
      },
      error: (err) => {
        this.error.set(extractApiError(err).message || 'The Gmail check failed. Please try again.');
        this.verifying.set(false);
      }
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
      error: () => this.credentials.set(null),
    });
  }

  saveCredentials() {
    const clientId = this.clientId.trim();
    const clientSecret = this.clientSecret.trim();
    const redirectUri = this.redirectUri.trim();

    if (!clientId || !clientSecret) {
      this.error.set('Client ID and Client Secret are both required.');
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
        this.toast.success('Google OAuth credentials saved. Connect Gmail again to use them.');
        this.loadCredentialSummary();
      },
      error: (err) => {
        this.error.set(extractApiError(err).message || 'Failed to save the OAuth credentials.');
        this.savingCredentials.set(false);
      }
    });
  }

  deleteCredentials() {
    if (this.deletingCredentials()) return;
    if (!confirm('Delete your custom Google OAuth credentials? Gmail will then use the server configuration.')) return;

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
        this.toast.success('Custom OAuth credentials deleted.');
      },
      error: (err) => {
        this.error.set(extractApiError(err).message || 'Failed to delete the OAuth credentials.');
        this.deletingCredentials.set(false);
      }
    });
  }

  connectGmail() {
    if (this.connecting()) return;
    this.connecting.set(true);
    this.error.set(null);
    this.success.set(null);
    this.emailService.getGmailLoginUrl().subscribe({
      next: (res) => {
        if (res?.url?.startsWith('https://accounts.google.com/')) {
          window.location.href = res.url;
        } else {
          this.error.set('The Gmail connection link is invalid. Please try again.');
          this.connecting.set(false);
        }
      },
      error: (err) => {
        this.error.set(extractApiError(err).message || 'Unable to start the Gmail connection.');
        this.connecting.set(false);
      }
    });
  }

  disconnectGmail() {
    if (this.disconnecting() || !confirm('Disconnect your Gmail account? NextStep will stop sending emails and checking replies.')) return;
    this.disconnecting.set(true);
    this.error.set(null);
    this.success.set(null);
    this.emailService.disconnectGmail().subscribe({
      next: () => {
        this.status.set({ isConnected: false, isTokenValid: false, needsReconnect: false, errorMessage: null, emailAddress: null, provider: 'Gmail' });
        this.disconnecting.set(false);
        this.toast.success('Gmail disconnected.');
      },
      error: (err) => {
        this.error.set(extractApiError(err).message || 'Failed to disconnect Gmail.');
        this.disconnecting.set(false);
      }
    });
  }
}
