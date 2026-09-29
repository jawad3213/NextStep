import { Component, inject, signal, computed, OnInit, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ProfileApiService } from '@features/profile/data-access/profile-api.service';
import { AuthService } from '@core/auth/auth.service';
import { ProfileService } from '@features/profile/data-access/profile.service';
import { EmailService } from '@features/applications/data-access/email.service';
import { LanguageService, AppLang } from '@core/i18n/language.service';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { ToastService } from '@core/notifications/toast.service';
import { extractApiError } from '@core/http/extract-api-error';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslatePipe],
  templateUrl: './settings.component.html',
  styleUrl: './settings.component.scss'
})
export class SettingsComponent implements OnInit {
  private authService = inject(AuthService);
  private profileApi = inject(ProfileApiService);
  private router = inject(Router);
  private emailService = inject(EmailService);
  private profileService = inject(ProfileService);
  private toast = inject(ToastService);
  readonly langService = inject(LanguageService);

  gmailConnected = signal(false);
  /** Connected, but Google access was lost (revoked, permission missing...): offer to reconnect. */
  gmailNeedsReconnect = signal(false);
  gmailBusy = signal(false);
  gmailEmailAddress = signal<string | null>(null);
  emailNotifs = signal(true);
  interviewReminders = signal(true);
  /** Two-way bound to the select — keep in sync with LanguageService */
  language = computed(() => this.langService.lang());
  timezone = signal('Africa/Casablanca');
  linkedinUrl = signal('');

  constructor() {
    // Automatically save preferences to localStorage on change
    effect(() => {
      localStorage.setItem('ns_settings_email_notifs', this.emailNotifs().toString());
    });
    effect(() => {
      localStorage.setItem('ns_settings_interview_reminders', this.interviewReminders().toString());
    });
    // Language is persisted by LanguageService itself — no effect needed here.
    effect(() => {
      localStorage.setItem('ns_settings_timezone', this.timezone());
    });

    // Automatically sync LinkedIn URL from profile
    effect(() => {
      const profile = this.profileService.profile();
      if (profile?.personal?.linkedinUrl !== undefined) {
        this.linkedinUrl.set(profile.personal.linkedinUrl || '');
      }
    });
  }

  ngOnInit() {
    this.loadGmailStatus();
    this.loadPreferences();
  }

  loadGmailStatus() {
    this.emailService.getGmailStatus().subscribe({
      next: (status) => {
        this.gmailConnected.set(status.isConnected && status.isTokenValid);
        this.gmailNeedsReconnect.set(status.isConnected && !status.isTokenValid && status.needsReconnect !== false);
        this.gmailEmailAddress.set(status.emailAddress);
      },
      error: () => {
        this.gmailConnected.set(false);
        this.gmailNeedsReconnect.set(false);
        this.gmailEmailAddress.set(null);
      }
    });
  }

  loadPreferences() {
    const savedEmailNotifs = localStorage.getItem('ns_settings_email_notifs');
    if (savedEmailNotifs !== null) this.emailNotifs.set(savedEmailNotifs === 'true');

    const savedInterviewReminders = localStorage.getItem('ns_settings_interview_reminders');
    if (savedInterviewReminders !== null) this.interviewReminders.set(savedInterviewReminders === 'true');

    // Language is restored by LanguageService on boot — no action needed.

    const savedTimezone = localStorage.getItem('ns_settings_timezone');
    if (savedTimezone !== null) this.timezone.set(savedTimezone);
  }

  editProfile() {
    this.router.navigate(['/profile'], {
      queryParams: { step: 'coordonnees' }
    });
  }

  exportProfile() {
    this.profileService.downloadProfileJson();
  }

  onLangChange(lang: string): void {
    if (lang === 'en' || lang === 'fr') {
      // Persists server-side (used for generated CVs/resumes) and updates the UI right away.
      void this.profileService.setLanguagePreference(lang as AppLang);
    }
  }

  toggleGmail() {
    if (this.gmailBusy()) return;
    if (this.gmailConnected()) {
      if (!confirm('Disconnect your Gmail account? NextStep will stop sending emails and checking replies.')) return;
      this.gmailBusy.set(true);
      this.emailService.disconnectGmail().subscribe({
        next: () => {
          this.gmailConnected.set(false);
          this.gmailNeedsReconnect.set(false);
          this.gmailEmailAddress.set(null);
          this.gmailBusy.set(false);
          this.toast.success('Gmail disconnected.');
        },
        error: (err) => {
          this.gmailBusy.set(false);
          this.toast.error(extractApiError(err).message || 'Unable to disconnect the Gmail account.');
        }
      });
      return;
    }

    // Connect or reconnect: Google sends the browser back to Settings > Gmail with the result.
    this.gmailBusy.set(true);
    this.emailService.getGmailLoginUrl().subscribe({
      next: (res) => {
        if (res?.url?.startsWith('https://accounts.google.com/')) {
          window.location.href = res.url;
        } else {
          this.gmailBusy.set(false);
          this.toast.error('The Gmail connection link is invalid. Please try again.');
        }
      },
      error: (err) => {
        this.gmailBusy.set(false);
        this.toast.error(extractApiError(err).message || 'Unable to start the Gmail connection.');
      }
    });
  }

  manageGmailCredentials() {
    this.router.navigate(['/email/settings']);
  }

  async saveLinkedinUrl() {
    const url = this.linkedinUrl().trim();
    const currentPersonal = this.profileService.profile().personal;
    
    const updatedPersonal = {
      ...currentPersonal,
      linkedinUrl: url
    };

    try {
      await this.profileService.savePersonalInfo(updatedPersonal);
      alert('LinkedIn profile URL saved successfully.');
      await this.profileService.refreshProfile();
    } catch (err) {
      console.error('Error saving LinkedIn URL:', err);
      alert('Error saving the URL.');
    }
  }

  async clearData() {
    if (confirm('Are you sure you want to delete all your data? This action is irreversible.')) {
      try {
        await firstValueFrom(this.profileApi.clearProfile());
        window.location.reload();
      } catch (err) {
        console.error('Failed to clear profile data:', err);
        alert('Failed to delete data. Please try again.');
      }
    }
  }
}
