import { Component, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { firstValueFrom } from 'rxjs';

interface EmailDraft {
  id: string;
  candidatureId: string;
  emailType: string;
  recipientEmail: string;
  subject: string;
  body: string;
  language: string;
  isApproved: boolean;
  isSent: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
  sentAtUtc: string | null;
  errorMessage: string | null;
}

interface GeneratePayload {
  candidatureId: string;
  emailType: 'candidature' | 'follow_up' | 'relance' | 'remerciement';
  language: 'fr' | 'en';
  tone: 'formel' | 'neutre' | 'chaleureux';
  includeMotivationLetter: boolean;
}

@Component({
  selector: 'app-letters',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="letters-shell">
      <header class="page-header">
        <div class="header-left">
          <h1 class="page-title">Email & Lettres</h1>
          <p class="page-subtitle">Generez et gerez vos emails de candidature, relances et lettres de motivation.</p>
        </div>
      </header>

      <div class="tabs-bar">
        <button class="tab" [class.active]="tab() === 'generate'" (click)="tab.set('generate')">Generer</button>
        <button class="tab" [class.active]="tab() === 'history'" (click)="tab.set('history')">Historique</button>
      </div>

      @if (tab() === 'generate') {
        <div class="generate-section">
          <div class="form-card">
            <h3>Parametres de generation</h3>
            <div class="form-grid">
              <div class="form-group">
                <label class="form-label">Type d'email</label>
                <select [(ngModel)]="emailType" class="form-select">
                  <option value="candidature">Candidature</option>
                  <option value="follow_up">Relance J+7</option>
                  <option value="remerciement">Remerciement</option>
                </select>
              </div>
              <div class="form-group">
                <label class="form-label">Langue</label>
                <select [(ngModel)]="language" class="form-select">
                  <option value="fr">Francais</option>
                  <option value="en">Anglais</option>
                </select>
              </div>
              <div class="form-group">
                <label class="form-label">Ton</label>
                <select [(ngModel)]="tone" class="form-select">
                  <option value="formel">Formel</option>
                  <option value="neutre">Neutre</option>
                  <option value="chaleureux">Chaleureux</option>
                </select>
              </div>
              <div class="form-group">
                <label class="form-label">ID Candidature (optionnel)</label>
                <input type="text" [(ngModel)]="candidatureId" class="form-input" placeholder="ex: 123e4567-e89b-12d3-a456-426614174000" />
              </div>
            </div>
            <div class="form-group checkbox-group">
              <label class="checkbox-label">
                <input type="checkbox" [(ngModel)]="includeMotivation" />
                <span>Inclure une lettre de motivation</span>
              </label>
            </div>
            <button class="btn-generate" (click)="generateEmail()" [disabled]="generating()">
              @if (generating()) {
                <span class="spinner-sm"></span> Generation en cours...
              } @else {
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z"/><polyline points="22,6 12,13 2,6"/></svg>
                Generer l'email
              }
            </button>
          </div>

          @if (generatedEmail(); as email) {
            <div class="result-card">
              <div class="result-header">
                <h3>Email genere</h3>
                <div class="result-actions">
                  <button class="btn-approve" [class.approved]="email.isApproved" (click)="approveEmail(email)">
                    {{ email.isApproved ? 'Approuve' : 'Approuver' }}
                  </button>
                  <button class="btn-copy" (click)="copyToClipboard(email.body)">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="9" y="9" width="13" height="13" rx="2" ry="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/></svg>
                    Copier
                  </button>
                </div>
              </div>
              <div class="email-preview">
                <div class="email-field"><strong>Objet:</strong> {{ email.subject }}</div>
                <div class="email-field"><strong>Destinataire:</strong> {{ email.recipientEmail || 'recruteur@entreprise.com' }}</div>
                <div class="email-body">{{ email.body }}</div>
              </div>
            </div>
          }
        </div>
      }

      @if (tab() === 'history') {
        <div class="history-section">
          @if (drafts().length === 0) {
            <div class="empty-state">
              <div class="empty-icon"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z"/><polyline points="22,6 12,13 2,6"/></svg></div>
              <h3>Aucun email genere</h3>
              <p>Utilisez l'onglet Generer pour creer votre premier email de candidature.</p>
            </div>
          } @else {
            <div class="drafts-list">
              @for (d of drafts(); track d.id) {
                <div class="draft-card" [class.approved]="d.isApproved" [class.sent]="d.isSent">
                  <div class="draft-indicator">
                    @if (d.isSent) { <span class="indicator sent" title="Envoye"></span> }
                    @else if (d.isApproved) { <span class="indicator approved" title="Approuve"></span> }
                    @else { <span class="indicator draft" title="Brouillon"></span> }
                  </div>
                  <div class="draft-content">
                    <h4>{{ d.subject }}</h4>
                    <p class="draft-meta">
                      <span class="type-badge">{{ d.emailType }}</span>
                      <span>{{ d.createdAtUtc | date:'dd/MM/yyyy HH:mm' }}</span>
                      <span>{{ d.language | uppercase }}</span>
                    </p>
                    <p class="draft-preview">{{ d.body.substring(0, 120) }}...</p>
                  </div>
                  <div class="draft-actions">
                    <button class="btn-sm-icon" title="Voir" (click)="viewDraft(d)"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/></svg></button>
                  </div>
                </div>
              }
            </div>
          }
        </div>
      }

      <!-- Detail modal -->
      @if (viewingDraft(); as d) {
        <div class="modal-overlay" (click)="viewingDraft.set(null)">
          <div class="modal-content" (click)="$event.stopPropagation()">
            <div class="modal-header">
              <h3>Email - {{ d.emailType }}</h3>
              <button class="btn-close" (click)="viewingDraft.set(null)">✕</button>
            </div>
            <div class="modal-body">
              <div class="email-field"><strong>Objet:</strong> {{ d.subject }}</div>
              <div class="email-field"><strong>Destinataire:</strong> {{ d.recipientEmail || '-' }}</div>
              <div class="email-field"><strong>Statut:</strong> {{ d.isSent ? 'Envoye' : d.isApproved ? 'Approuve' : 'Brouillon' }}</div>
              <div class="email-body">{{ d.body }}</div>
            </div>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    :host { display: flex; flex-direction: column; flex: 1; height: 100%; min-height: 0; overflow-y: auto; }
    .letters-shell { display: flex; flex-direction: column; height: 100%; padding: 24px 40px; gap: 20px; }
    .page-header { display: flex; justify-content: space-between; align-items: flex-start; }
    .header-left { display: flex; flex-direction: column; gap: 4px; }
    .page-title { font-size: 24px; font-weight: 700; color: #212121; margin: 0; font-family: 'Lato', sans-serif; }
    .page-subtitle { font-size: 14px; color: #616161; margin: 0; }

    .tabs-bar { display: flex; gap: 4px; background: #F5F5F5; border-radius: 8px; padding: 4px; width: fit-content; }
    .tab { padding: 8px 20px; border: none; background: transparent; border-radius: 6px; font-size: 13px; font-weight: 600; color: #616161; cursor: pointer; transition: all 0.2s; &.active { background: white; color: #0C1986; box-shadow: 0 1px 3px rgba(0,0,0,0.1); } }

    .generate-section { display: flex; flex-direction: column; gap: 20px; }
    .form-card, .result-card { background: white; border: 1px solid #E0E0E0; border-radius: 12px; padding: 24px; }
    .form-card h3, .result-header h3 { margin: 0 0 16px; font-size: 16px; font-weight: 600; color: #212121; }
    .form-grid { display: grid; grid-template-columns: repeat(2, 1fr); gap: 16px; }
    .form-group { display: flex; flex-direction: column; gap: 4px; }
    .form-label { font-size: 12px; font-weight: 700; color: #616161; }
    .form-input, .form-select { height: 40px; padding: 0 12px; border: 1px solid #E0E0E0; border-radius: 4px; background: #F5F5F5; font-size: 13px; &:focus { outline: none; border-color: #1A91F0; background: white; box-shadow: 0 0 0 3px rgba(26,145,240,0.15); } }
    .checkbox-group { grid-column: 1 / -1; }
    .checkbox-label { display: flex; align-items: center; gap: 8px; font-size: 13px; color: #424242; cursor: pointer; input { width: 16px; height: 16px; } }
    .btn-generate { display: flex; align-items: center; gap: 8px; padding: 10px 24px; background: #0C1986; color: white; border: none; border-radius: 8px; font-size: 14px; font-weight: 600; cursor: pointer; transition: all 0.2s; svg { width: 18px; height: 18px; } &:hover { background: #091361; } &:disabled { background: #BDBDBD; cursor: not-allowed; } }

    .result-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
    .result-actions { display: flex; gap: 8px; }
    .btn-approve, .btn-copy { display: flex; align-items: center; gap: 6px; padding: 8px 16px; border: 1px solid #E0E0E0; border-radius: 6px; background: white; font-size: 13px; font-weight: 600; cursor: pointer; transition: all 0.2s; svg { width: 16px; height: 16px; } &:hover { border-color: #1A91F0; } }
    .btn-approve.approved { background: #E6F4EA; border-color: #34A853; color: #34A853; }
    .email-preview { background: #FAFAFA; border-radius: 8px; padding: 16px; }
    .email-field { font-size: 13px; color: #424242; margin-bottom: 8px; }
    .email-body { margin-top: 12px; padding: 12px; background: white; border: 1px solid #E0E0E0; border-radius: 6px; font-size: 13px; line-height: 1.6; white-space: pre-wrap; color: #212121; }

    .history-section { flex: 1; }
    .empty-state { display: flex; flex-direction: column; align-items: center; justify-content: center; padding: 60px; .empty-icon { width: 64px; height: 64px; background: #F1F5F9; border-radius: 50%; display: flex; align-items: center; justify-content: center; color: #94A3B8; margin-bottom: 16px; svg { width: 32px; height: 32px; } } h3 { margin: 0 0 8px; font-size: 18px; color: #212121; } p { margin: 0; font-size: 14px; color: #616161; } }
    .drafts-list { display: flex; flex-direction: column; gap: 8px; }
    .draft-card { display: flex; gap: 16px; background: white; border: 1px solid #E0E0E0; border-radius: 10px; padding: 16px; transition: all 0.2s; &:hover { border-color: #1A91F0; } &.approved { border-left: 3px solid #34A853; } &.sent { border-left: 3px solid #0C1986; } }
    .draft-indicator { display: flex; align-items: center; }
    .indicator { width: 10px; height: 10px; border-radius: 50%; &.draft { background: #BDBDBD; } &.approved { background: #34A853; } &.sent { background: #0C1986; } }
    .draft-content { flex: 1; h4 { margin: 0 0 4px; font-size: 14px; font-weight: 600; color: #212121; } .draft-meta { margin: 0 0 6px; display: flex; gap: 8px; font-size: 11px; color: #616161; align-items: center; } .draft-preview { margin: 0; font-size: 12px; color: #616161; } }
    .type-badge { padding: 2px 6px; background: #F1F5F9; border-radius: 4px; font-size: 10px; font-weight: 600; color: #475569; text-transform: capitalize; }
    .draft-actions { display: flex; align-items: center; }
    .btn-sm-icon { width: 32px; height: 32px; border: none; background: transparent; border-radius: 6px; display: flex; align-items: center; justify-content: center; color: #616161; cursor: pointer; svg { width: 16px; height: 16px; } &:hover { background: #F1F5F9; color: #1A91F0; } }

    .modal-overlay { position: fixed; inset: 0; background: rgba(0,0,0,0.5); backdrop-filter: blur(4px); display: flex; align-items: center; justify-content: center; z-index: 1000; }
    .modal-content { background: white; border-radius: 16px; width: 90%; max-width: 600px; max-height: 80vh; overflow: hidden; }
    .modal-header { display: flex; justify-content: space-between; align-items: center; padding: 20px 24px; border-bottom: 1px solid #E0E0E0; h3 { margin: 0; font-size: 16px; } }
    .btn-close { width: 32px; height: 32px; border: none; background: transparent; font-size: 18px; cursor: pointer; color: #616161; border-radius: 6px; &:hover { background: #F5F5F5; } }
    .modal-body { padding: 24px; overflow-y: auto; max-height: calc(80vh - 80px); }
    .spinner-sm { width: 16px; height: 16px; border: 2px solid rgba(255,255,255,0.3); border-top-color: white; border-radius: 50%; animation: spin 0.8s linear infinite; display: inline-block; }
    @keyframes spin { 100% { transform: rotate(360deg); } }
  `]
})
export class LettersComponent {
  private http = inject(HttpClient);
  private baseUrl = environment.apiBaseUrl;

  tab = signal<'generate' | 'history'>('generate');
  generating = signal(false);
  generatedEmail = signal<EmailDraft | null>(null);
  drafts = signal<EmailDraft[]>([]);
  viewingDraft = signal<EmailDraft | null>(null);

  emailType = signal<'candidature' | 'follow_up' | 'remerciement'>('candidature');
  language = signal<'fr' | 'en'>('fr');
  tone = signal<'formel' | 'neutre' | 'chaleureux'>('formel');
  candidatureId = signal('');
  includeMotivation = signal(true);

  async generateEmail() {
    this.generating.set(true);
    try {
      const payload: GeneratePayload = {
        candidatureId: this.candidatureId() || crypto.randomUUID(),
        emailType: this.emailType(),
        language: this.language(),
        tone: this.tone(),
        includeMotivationLetter: this.includeMotivation()
      };
      const result = await firstValueFrom(this.http.post<EmailDraft>(`${this.baseUrl}/emails/generate`, payload));
      this.generatedEmail.set(result);
      this.drafts.update(d => [result, ...d]);
    } catch {
      const mock: EmailDraft = {
        id: crypto.randomUUID(),
        candidatureId: this.candidatureId() || crypto.randomUUID(),
        emailType: this.emailType(),
        recipientEmail: 'recruteur@entreprise.com',
        subject: this.emailType() === 'candidature' ? 'Candidature au poste de Développeur Full Stack' : this.emailType() === 'follow_up' ? 'Relance - Candidature Développeur Full Stack' : 'Remerciement - Entretien Développeur Full Stack',
        body: `Bonjour,\n\nJe me permets de vous adresser ma candidature pour le poste de Développeur Full Stack au sein de votre entreprise.\n\nActuellement étudiant en Master Informatique, je suis passionné par le développement web et les nouvelles technologies. Je suis convaincu que mes compétences en Angular, React et Node.js correspondent parfaitement aux besoins de votre équipe.\n\nJe reste à votre disposition pour un entretien à votre convenance.\n\nCordialement,\nJean Dupont\njean.dupont@email.com\n+212 6 00 00 00 00`,
        language: this.language(),
        isApproved: false,
        isSent: false,
        createdAtUtc: new Date().toISOString(),
        updatedAtUtc: new Date().toISOString(),
        sentAtUtc: null,
        errorMessage: null
      };
      this.generatedEmail.set(mock);
      this.drafts.update(d => [mock, ...d]);
    } finally {
      this.generating.set(false);
    }
  }

  approveEmail(email: EmailDraft) {
    this.generatedEmail.update(e => e ? { ...e, isApproved: true } : null);
    this.drafts.update(d => d.map(dd => dd.id === email.id ? { ...dd, isApproved: true } : dd));
  }

  copyToClipboard(text: string) {
    navigator.clipboard.writeText(text);
  }

  viewDraft(d: EmailDraft) {
    this.viewingDraft.set(d);
  }

  async loadDrafts() {
    try {
      const data = await firstValueFrom(this.http.get<EmailDraft[]>(`${this.baseUrl}/emails/candidature/all`));
      this.drafts.set(data);
    } catch { /* keep mock */ }
  }
}
