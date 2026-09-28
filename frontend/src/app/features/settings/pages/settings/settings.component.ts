import { Component, inject, signal, OnInit, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ProfileApiService } from '@features/profile/data-access/profile-api.service';
import { AuthService } from '@core/auth/auth.service';
import { ProfileService } from '@features/profile/data-access/profile.service';
import { EmailService } from '@features/applications/data-access/email.service';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './settings.component.html',
  styleUrl: './settings.component.scss'
})
export class SettingsComponent implements OnInit {
  private authService = inject(AuthService);
  private profileApi = inject(ProfileApiService);
  private router = inject(Router);
  private emailService = inject(EmailService);
  private profileService = inject(ProfileService);

  gmailConnected = signal(false);
  gmailEmailAddress = signal<string | null>(null);
  emailNotifs = signal(true);
  interviewReminders = signal(true);
  language = signal('fr');
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
    effect(() => {
      localStorage.setItem('ns_settings_language', this.language());
    });
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
        this.gmailEmailAddress.set(status.emailAddress);
      },
      error: () => {
        this.gmailConnected.set(false);
        this.gmailEmailAddress.set(null);
      }
    });
  }

  loadPreferences() {
    const savedEmailNotifs = localStorage.getItem('ns_settings_email_notifs');
    if (savedEmailNotifs !== null) this.emailNotifs.set(savedEmailNotifs === 'true');

    const savedInterviewReminders = localStorage.getItem('ns_settings_interview_reminders');
    if (savedInterviewReminders !== null) this.interviewReminders.set(savedInterviewReminders === 'true');

    const savedLanguage = localStorage.getItem('ns_settings_language');
    if (savedLanguage !== null) this.language.set(savedLanguage);

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

  toggleGmail() {
    if (this.gmailConnected()) {
      if (confirm('Etes-vous sur de vouloir deconnecter votre compte Gmail ?')) {
        this.emailService.disconnectGmail().subscribe({
          next: () => {
            this.gmailConnected.set(false);
            this.gmailEmailAddress.set(null);
          },
          error: (err) => {
            console.error('Erreur deconnexion Gmail:', err);
            alert('Impossible de deconnecter le compte Gmail.');
          }
        });
      }
    } else {
      this.emailService.getGmailLoginUrl().subscribe({
        next: (res) => {
          if (res?.url) {
            window.location.href = res.url;
          } else {
            alert('Lien de connexion Gmail indisponible.');
          }
        },
        error: (err) => {
          console.error('Erreur obtention lien Gmail:', err);
          alert('Impossible d\'obtenir le lien de connexion Gmail. Veuillez verifier les credentials.');
        }
      });
    }
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
      alert('URL du profil LinkedIn enregistree avec succes.');
      await this.profileService.refreshProfile();
    } catch (err) {
      console.error('Erreur lors de l\'enregistrement de l\'URL LinkedIn:', err);
      alert('Erreur lors de l\'enregistrement de l\'URL.');
    }
  }

  async clearData() {
    if (confirm('Etes-vous sur de vouloir effacer toutes vos donnees ? Cette action est irreversible.')) {
      try {
        await firstValueFrom(this.profileApi.clearProfile());
        window.location.reload();
      } catch (err) {
        console.error('Failed to clear profile data:', err);
        alert('Echec de la suppression des donnees. Veuillez reessayer.');
      }
    }
  }
}
