import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { AuthService } from '../../core/auth/services/auth.service';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="settings-shell">
      <header class="page-header">
        <div class="header-left">
          <h1 class="page-title">Parametres</h1>
          <p class="page-subtitle">Gerez votre compte, vos connexions et vos preferences.</p>
        </div>
      </header>

      <div class="settings-sections">
        <!-- Profile -->
        <div class="settings-card">
          <h3><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg> Compte</h3>
          <div class="setting-row">
            <div class="setting-info">
              <strong>Informations personnelles</strong>
              <p>Nom, email, telephone et photo de profil</p>
            </div>
            <button class="btn-edit" (click)="editProfile()">Modifier</button>
          </div>
          <div class="setting-row">
            <div class="setting-info">
              <strong>Changer le mot de passe</strong>
              <p>Mettez a jour votre mot de passe Keycloak</p>
            </div>
            <button class="btn-edit" (click)="changePassword()">Modifier</button>
          </div>
        </div>

        <!-- Email & Notifications -->
        <div class="settings-card">
          <h3><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z"/><polyline points="22,6 12,13 2,6"/></svg> Notifications & Email</h3>
          <div class="setting-row">
            <div class="setting-info">
              <strong>Connexion Gmail</strong>
              <p>Connectez votre boite Gmail pour la detection automatique des reponses</p>
            </div>
            <button class="btn-connect" [class.connected]="gmailConnected()" (click)="toggleGmail()">
              {{ gmailConnected() ? 'Connecte' : 'Connecter' }}
            </button>
          </div>
          <div class="setting-row">
            <div class="setting-info">
              <strong>Notifications email</strong>
              <p>Recevoir des alertes de relance et de nouvelles offres</p>
            </div>
            <label class="toggle">
              <input type="checkbox" [(ngModel)]="emailNotifs" />
              <span class="toggle-slider"></span>
            </label>
          </div>
          <div class="setting-row">
            <div class="setting-info">
              <strong>Rappels entretiens</strong>
              <p>Notifications 24h avant un entretien planifie</p>
            </div>
            <label class="toggle">
              <input type="checkbox" [(ngModel)]="interviewReminders" />
              <span class="toggle-slider"></span>
            </label>
          </div>
        </div>

        <!-- LinkedIn -->
        <div class="settings-card">
          <h3><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M16 8a6 6 0 0 1 6 6v7h-4v-7a2 2 0 0 0-2-2 2 2 0 0 0-2 2v7h-4v-7a6 6 0 0 1 6-6z"/><rect x="2" y="9" width="4" height="12"/><circle cx="4" cy="4" r="2"/></svg> LinkedIn</h3>
          <div class="setting-row">
            <div class="setting-info">
              <strong>Importation automatique du profil</strong>
              <p>Synchronisez votre profil LinkedIn pour importer experiences et competences</p>
            </div>
            <button class="btn-connect" [class.connected]="linkedinConnected()" (click)="toggleLinkedin()">
              {{ linkedinConnected() ? 'Connecte' : 'Connecter' }}
            </button>
          </div>
          <div class="setting-row">
            <div class="setting-info">
              <strong>URL du profil LinkedIn</strong>
              <p>Ajoutez votre profil pour la recherche d'offres personnalisee</p>
            </div>
          </div>
          <div class="setting-row">
            <input type="text" [(ngModel)]="linkedinUrl" class="setting-input" placeholder="https://www.linkedin.com/in/votreprofil/" />
            <button class="btn-save-sm" (click)="saveLinkedinUrl()">Enregistrer</button>
          </div>
        </div>

        <!-- Preferences -->
        <div class="settings-card">
          <h3><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.1a2 2 0 0 1 1 1.72v.51a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.39a2 2 0 0 0-.73-2.73l-.15-.1a2 2 0 0 1-1-1.72v-.51a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0 .73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2z"/><circle cx="12" cy="12" r="3"/></svg> Preferences</h3>
          <div class="setting-row">
            <div class="setting-info">
              <strong>Langue de l'interface</strong>
              <p>Choisissez la langue d'affichage de l'application</p>
            </div>
            <select [(ngModel)]="language" class="setting-select">
              <option value="fr">Francais</option>
              <option value="en">English</option>
            </select>
          </div>
          <div class="setting-row">
            <div class="setting-info">
              <strong>Fuseau horaire</strong>
              <p>Afrique/Casablanca (UTC+1)</p>
            </div>
            <select [(ngModel)]="timezone" class="setting-select">
              <option value="Africa/Casablanca">Afrique/Casablanca (UTC+1)</option>
              <option value="Europe/Paris">Europe/Paris (UTC+2)</option>
            </select>
          </div>
        </div>

        <!-- Danger Zone -->
        <div class="settings-card danger">
          <h3><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><line x1="15" y1="9" x2="9" y2="15"/><line x1="9" y1="9" x2="15" y2="15"/></svg> Zone de danger</h3>
          <div class="setting-row">
            <div class="setting-info">
              <strong>Supprimer toutes les donnees</strong>
              <p>Cette action effacera votre profil, vos offres et vos CV de maniere irreversible</p>
            </div>
            <button class="btn-danger" (click)="clearData()">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="3 6 5 6 21 6"/><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/></svg>
              Effacer
            </button>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    :host { display: flex; flex-direction: column; flex: 1; height: 100%; min-height: 0; overflow-y: auto; }
    .settings-shell { display: flex; flex-direction: column; padding: 24px 40px; gap: 20px; }
    .page-header { display: flex; justify-content: space-between; align-items: flex-start; }
    .header-left { display: flex; flex-direction: column; gap: 4px; }
    .page-title { font-size: 24px; font-weight: 700; color: #212121; margin: 0; font-family: 'Lato', sans-serif; }
    .page-subtitle { font-size: 14px; color: #616161; margin: 0; }
    .settings-sections { display: flex; flex-direction: column; gap: 16px; }
    .settings-card { background: white; border: 1px solid #E0E0E0; border-radius: 12px; padding: 24px; &.danger { border-color: #FCE8E6; h3 svg { stroke: #D93025; } } }
    .settings-card h3 { margin: 0 0 16px; font-size: 16px; font-weight: 700; color: #212121; display: flex; align-items: center; gap: 10px; svg { width: 20px; height: 20px; stroke: #0C1986; } }
    .setting-row { display: flex; justify-content: space-between; align-items: center; padding: 12px 0; border-bottom: 1px solid #F1F5F9; &:last-child { border: none; } }
    .setting-info { flex: 1; strong { display: block; font-size: 14px; color: #212121; margin-bottom: 2px; } p { margin: 0; font-size: 12px; color: #616161; } }

    .btn-edit, .btn-connect, .btn-save-sm { padding: 8px 16px; border: 1px solid #E0E0E0; border-radius: 8px; background: white; font-size: 13px; font-weight: 600; cursor: pointer; white-space: nowrap; transition: all 0.2s; &:hover { border-color: #1A91F0; color: #1A91F0; } }
    .btn-connect.connected { background: #E6F4EA; border-color: #34A853; color: #34A853; }
    .btn-save-sm { background: #0C1986; color: white; border: none; &:hover { background: #091361; color: white; } }
    .btn-danger { display: flex; align-items: center; gap: 6px; padding: 8px 16px; background: #FCE8E6; border: 1px solid #D93025; border-radius: 8px; color: #D93025; font-size: 13px; font-weight: 600; cursor: pointer; svg { width: 16px; height: 16px; } &:hover { background: #D93025; color: white; } }

    .toggle { position: relative; display: inline-block; width: 44px; height: 24px; input { opacity: 0; width: 0; height: 0; &:checked + .toggle-slider { background: #0C1986; } &:checked + .toggle-slider:before { transform: translateX(20px); } } }
    .toggle-slider { position: absolute; cursor: pointer; inset: 0; background: #BDBDBD; border-radius: 24px; transition: 0.3s; &:before { content: ''; position: absolute; height: 18px; width: 18px; left: 3px; bottom: 3px; background: white; border-radius: 50%; transition: 0.3s; } }

    .setting-input { flex: 1; height: 36px; padding: 0 12px; border: 1px solid #E0E0E0; border-radius: 6px; background: #F5F5F5; font-size: 13px; margin-right: 8px; &:focus { outline: none; border-color: #1A91F0; background: white; } }
    .setting-select { height: 36px; padding: 0 32px 0 12px; border: 1px solid #E0E0E0; border-radius: 6px; background: #F5F5F5; font-size: 13px; appearance: none; background-image: url("data:image/svg+xml,%3Csvg viewBox='0 0 24 24' fill='none' stroke='%239E9E9E' stroke-width='2'%3E%3Cpolyline points='6 9 12 15 18 9'/%3E%3C/svg%3E"); background-repeat: no-repeat; background-position: right 8px center; background-size: 16px; }
  `]
})
export class SettingsComponent {
  private authService = inject(AuthService);
  private http = inject(HttpClient);
  private baseUrl = environment.apiBaseUrl;

  gmailConnected = signal(false);
  linkedinConnected = signal(false);
  emailNotifs = signal(true);
  interviewReminders = signal(true);
  language = signal('fr');
  timezone = signal('Africa/Casablanca');
  linkedinUrl = signal('');

  editProfile() {
    this.authService.login();
  }

  changePassword() {
    const keycloak = (window as any).keycloak;
    if (keycloak?.accountUrl) {
      window.open(`${keycloak.accountUrl}/password`, '_blank');
    }
  }

  toggleGmail() {
    this.gmailConnected.update(v => !v);
  }

  toggleLinkedin() {
    this.linkedinConnected.update(v => !v);
  }

  saveLinkedinUrl() {
    if (this.linkedinUrl()) {
      this.linkedinConnected.set(true);
    }
  }

  async clearData() {
    if (confirm('Etes-vous sur de vouloir effacer toutes vos donnees ? Cette action est irreversible.')) {
      try {
        await this.http.delete(`${this.baseUrl}/profile/clear`).toPromise();
        window.location.reload();
      } catch {}
    }
  }
}
