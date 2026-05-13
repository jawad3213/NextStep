import { Component, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { firstValueFrom } from 'rxjs';

interface Candidature {
  idCandidature: string;
  idUtilisateur: string;
  idOffre: string;
  dateCreation: string;
  inclureLettreMotivation: boolean;
  statut: 'envoye' | 'vu' | 'entretien' | 'offre' | 'refuse';
  offreTitre?: string;
  entreprise?: string;
  notes?: string;
}

@Component({
  selector: 'app-applications',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="kanban-shell">
      <header class="page-header">
        <div class="header-left">
          <h1 class="page-title">Applications</h1>
          <p class="page-subtitle">Suivez l'avancement de vos candidatures en mode Kanban.</p>
        </div>
        <button class="btn-add" (click)="showNewForm.set(true)">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>
          Nouvelle candidature
        </button>
      </header>

      <div class="kanban-board">
        @for (col of columns; track col.key) {
          <div class="kanban-column" [class.highlight]="dragOverColumn() === col.key" (dragover)="onDragOver($event, col.key)" (dragleave)="onDragLeave()" (drop)="onDrop($event, col.key)">
            <div class="column-header">
              <div class="column-indicator" [style.background]="col.color"></div>
              <h3>{{ col.label }}</h3>
              <span class="column-count">{{ getColumnItems(col.key).length }}</span>
            </div>
            <div class="column-body">
              @for (item of getColumnItems(col.key); track item.idCandidature) {
                <div class="kanban-card" draggable="true" (dragstart)="onDragStart(item)" (click)="selectCandidature(item)">
                  <div class="card-company-badge" [style.background]="getBubbleColor(item.entreprise || '')">{{ getInitials(item.entreprise || 'Offre') }}</div>
                  <h4>{{ item.offreTitre || 'Poste' }}</h4>
                  <p class="card-company">{{ item.entreprise || 'Entreprise' }}</p>
                  <p class="card-date">{{ item.dateCreation | date:'dd MMM' }}</p>
                </div>
              }
              @if (getColumnItems(col.key).length === 0) {
                <div class="column-empty">
                  <p>Aucune candidature</p>
                </div>
              }
            </div>
          </div>
        }
      </div>

      <!-- New candidature modal -->
      @if (showNewForm()) {
        <div class="modal-overlay" (click)="showNewForm.set(false)">
          <div class="modal-content" (click)="$event.stopPropagation()">
            <div class="modal-header">
              <h3>Nouvelle candidature</h3>
              <button class="btn-close" (click)="showNewForm.set(false)">✕</button>
            </div>
            <div class="modal-body">
              <div class="form-group">
                <label class="form-label">Titre du poste</label>
                <input type="text" [(ngModel)]="formTitre" class="form-input" placeholder="Developpeur Full Stack" />
              </div>
              <div class="form-group">
                <label class="form-label">Entreprise</label>
                <input type="text" [(ngModel)]="formEntreprise" class="form-input" placeholder="TechCorp" />
              </div>
              <div class="form-group">
                <label class="form-label">Statut</label>
                <select [(ngModel)]="formStatut" class="form-select">
                  <option value="envoye">Envoye</option>
                  <option value="vu">Vu</option>
                  <option value="entretien">Entretien</option>
                  <option value="offre">Offre</option>
                  <option value="refuse">Refuse</option>
                </select>
              </div>
              <div class="form-group">
                <label class="form-label">Notes</label>
                <textarea [(ngModel)]="formNotes" class="form-textarea" rows="3" placeholder="Notes..."></textarea>
              </div>
              <div class="form-actions">
                <button class="btn-cancel" (click)="showNewForm.set(false)">Annuler</button>
                <button class="btn-save" (click)="createCandidature()">Ajouter</button>
              </div>
            </div>
          </div>
        </div>
      }

      <!-- Detail modal -->
      @if (selectedCandidature(); as sel) {
        <div class="modal-overlay" (click)="selectedCandidature.set(null)">
          <div class="modal-content" (click)="$event.stopPropagation()">
            <div class="modal-header">
              <h3>{{ sel.offreTitre || 'Candidature' }}</h3>
              <button class="btn-close" (click)="selectedCandidature.set(null)">✕</button>
            </div>
            <div class="modal-body">
              <p><strong>Entreprise:</strong> {{ sel.entreprise }}</p>
              <p><strong>Statut:</strong> <span class="status-chip" [style.background]="getStatusColor(sel.statut) + '20'" [style.color]="getStatusColor(sel.statut)">{{ sel.statut }}</span></p>
              <p><strong>Date:</strong> {{ sel.dateCreation | date:'dd/MM/yyyy' }}</p>
              <p><strong>Notes:</strong> {{ sel.notes || 'Aucune note' }}</p>
              <div class="modal-actions">
                @if (sel.statut === 'envoye') { <button class="btn-status" (click)="updateStatus(sel, 'vu')">Marquer comme vu</button> }
                @if (sel.statut === 'vu') { <button class="btn-status" (click)="updateStatus(sel, 'entretien')">Entretien planifie</button> }
                @if (sel.statut === 'entretien') { <button class="btn-status" (click)="updateStatus(sel, 'offre')">Offre recue</button> }
                @if (sel.statut !== 'refuse' && sel.statut !== 'offre') { <button class="btn-status danger" (click)="updateStatus(sel, 'refuse')">Refuser</button> }
              </div>
            </div>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    :host { display: flex; flex-direction: column; flex: 1; height: 100%; min-height: 0; overflow: hidden; }
    .kanban-shell { display: flex; flex-direction: column; height: 100%; padding: 24px 40px; gap: 20px; overflow: hidden; }
    .page-header { display: flex; justify-content: space-between; align-items: flex-start; }
    .header-left { display: flex; flex-direction: column; gap: 4px; }
    .page-title { font-size: 24px; font-weight: 700; color: #212121; margin: 0; font-family: 'Lato', sans-serif; }
    .page-subtitle { font-size: 14px; color: #616161; margin: 0; }
    .btn-add { display: flex; align-items: center; gap: 8px; padding: 10px 20px; background: #0C1986; color: white; border: none; border-radius: 8px; font-size: 14px; font-weight: 600; cursor: pointer; transition: all 0.2s; svg { width: 18px; height: 18px; } &:hover { background: #091361; } }

    .kanban-board { display: grid; grid-template-columns: repeat(5, 1fr); gap: 12px; flex: 1; min-height: 0; overflow: hidden; }
    .kanban-column { background: #F8F9FA; border-radius: 12px; display: flex; flex-direction: column; overflow: hidden; transition: all 0.2s; &.highlight { background: #EEF2FF; box-shadow: inset 0 0 0 2px #0C1986; } }
    .column-header { display: flex; align-items: center; gap: 8px; padding: 16px; border-bottom: 1px solid #E0E0E0; flex-shrink: 0; h3 { margin: 0; font-size: 13px; font-weight: 700; color: #212121; text-transform: uppercase; letter-spacing: 0.03em; } }
    .column-indicator { width: 4px; height: 20px; border-radius: 2px; }
    .column-count { margin-left: auto; padding: 2px 8px; background: #E0E0E0; border-radius: 100px; font-size: 11px; font-weight: 700; color: #616161; }
    .column-body { flex: 1; padding: 12px; overflow-y: auto; display: flex; flex-direction: column; gap: 8px; }
    .column-empty p { text-align: center; color: #9E9E9E; font-size: 12px; padding: 20px 0; margin: 0; }

    .kanban-card { background: white; border: 1px solid #E0E0E0; border-radius: 10px; padding: 12px; cursor: grab; transition: all 0.2s; &:hover { border-color: #1A91F0; box-shadow: 0 4px 12px rgba(0,0,0,0.06); } &:active { cursor: grabbing; } }
    .card-company-badge { width: 28px; height: 28px; border-radius: 6px; display: flex; align-items: center; justify-content: center; color: white; font-weight: 700; font-size: 11px; margin-bottom: 8px; }
    h4 { margin: 0 0 4px; font-size: 13px; font-weight: 600; color: #212121; }
    .card-company { margin: 0 0 4px; font-size: 11px; color: #616161; }
    .card-date { margin: 0; font-size: 10px; color: #9E9E9E; }

    .modal-overlay { position: fixed; inset: 0; background: rgba(0,0,0,0.5); backdrop-filter: blur(4px); display: flex; align-items: center; justify-content: center; z-index: 1000; }
    .modal-content { background: white; border-radius: 16px; width: 90%; max-width: 480px; max-height: 80vh; overflow: hidden; }
    .modal-header { display: flex; justify-content: space-between; align-items: center; padding: 20px 24px; border-bottom: 1px solid #E0E0E0; h3 { margin: 0; font-size: 16px; } }
    .btn-close { width: 32px; height: 32px; border: none; background: transparent; font-size: 18px; cursor: pointer; color: #616161; border-radius: 6px; &:hover { background: #F5F5F5; } }
    .modal-body { padding: 24px; display: flex; flex-direction: column; gap: 16px; }
    .form-group { display: flex; flex-direction: column; gap: 4px; }
    .form-label { font-size: 12px; font-weight: 700; color: #616161; }
    .form-input, .form-select { height: 40px; padding: 0 12px; border: 1px solid #E0E0E0; border-radius: 4px; background: #F5F5F5; font-size: 13px; &:focus { outline: none; border-color: #1A91F0; background: white; } }
    .form-textarea { padding: 12px; border: 1px solid #E0E0E0; border-radius: 4px; background: #F5F5F5; font-size: 13px; resize: vertical; &:focus { outline: none; border-color: #1A91F0; background: white; } }
    .form-actions, .modal-actions { display: flex; gap: 8px; justify-content: flex-end; }
    .btn-cancel { padding: 8px 16px; border: 1px solid #E0E0E0; border-radius: 6px; background: white; font-size: 13px; font-weight: 600; cursor: pointer; }
    .btn-save, .btn-status { padding: 8px 16px; border: none; border-radius: 6px; background: #0C1986; color: white; font-size: 13px; font-weight: 600; cursor: pointer; transition: all 0.2s; &:hover { background: #091361; } &.danger { background: #D93025; &:hover { background: #C62828; } } }
    .status-chip { padding: 2px 8px; border-radius: 100px; font-size: 12px; font-weight: 600; }
  `]
})
export class ApplicationsComponent {
  private http = inject(HttpClient);
  private baseUrl = environment.apiBaseUrl;

  candidatures = signal<Candidature[]>([
    { idCandidature: '1', idUtilisateur: 'u1', idOffre: 'o1', dateCreation: '2026-04-20', inclureLettreMotivation: true, statut: 'envoye', offreTitre: 'Full Stack Developer', entreprise: 'Capgemini' },
    { idCandidature: '2', idUtilisateur: 'u1', idOffre: 'o2', dateCreation: '2026-04-18', inclureLettreMotivation: true, statut: 'vu', offreTitre: 'Java Spring Dev', entreprise: 'CGI' },
    { idCandidature: '3', idUtilisateur: 'u1', idOffre: 'o3', dateCreation: '2026-04-15', inclureLettreMotivation: false, statut: 'entretien', offreTitre: 'DevOps Engineer', entreprise: 'OCP' },
    { idCandidature: '4', idUtilisateur: 'u1', idOffre: 'o4', dateCreation: '2026-04-10', inclureLettreMotivation: true, statut: 'offre', offreTitre: 'Data Engineer', entreprise: 'Maroc Telecom', notes: 'Offre recue le 25/04 - salaire 18000 DH' },
    { idCandidature: '5', idUtilisateur: 'u1', idOffre: 'o5', dateCreation: '2026-04-05', inclureLettreMotivation: true, statut: 'refuse', offreTitre: 'Frontend Dev', entreprise: 'Attijariwafa' },
  ]);

  showNewForm = signal(false);
  selectedCandidature = signal<Candidature | null>(null);
  dragOverColumn = signal<string | null>(null);
  draggedItem = signal<Candidature | null>(null);

  formTitre = signal('');
  formEntreprise = signal('');
  formStatut = signal<Candidature['statut']>('envoye');
  formNotes = signal('');

  columns = [
    { key: 'envoye', label: 'Envoye', color: '#94A3B8' },
    { key: 'vu', label: 'Vu', color: '#1A91F0' },
    { key: 'entretien', label: 'Entretien', color: '#F59B00' },
    { key: 'offre', label: 'Offre', color: '#34A853' },
    { key: 'refuse', label: 'Refuse', color: '#D93025' },
  ];

  getColumnItems(status: string) {
    return this.candidatures().filter(c => c.statut === status);
  }

  onDragStart(c: Candidature) { this.draggedItem.set(c); }
  onDragOver(e: DragEvent, col: string) { e.preventDefault(); this.dragOverColumn.set(col); }
  onDragLeave() { this.dragOverColumn.set(null); }
  onDrop(e: DragEvent, col: string) {
    e.preventDefault();
    this.dragOverColumn.set(null);
    const item = this.draggedItem();
    if (item) this.updateStatus(item, col as Candidature['statut']);
    this.draggedItem.set(null);
  }

  async updateStatus(c: Candidature, newStatus: Candidature['statut']) {
    this.candidatures.update(list => list.map(c2 => c2.idCandidature === c.idCandidature ? { ...c2, statut: newStatus } : c2));
    this.selectedCandidature.update(s => s?.idCandidature === c.idCandidature ? { ...s, statut: newStatus } : s);
    try { await firstValueFrom(this.http.post(`${this.baseUrl}/candidatures`, { idOffre: c.idOffre, inclureLettreMotivation: true })); } catch {}
  }

  async createCandidature() {
    if (!this.formTitre() || !this.formEntreprise()) return;
    const newC: Candidature = {
      idCandidature: crypto.randomUUID(),
      idUtilisateur: 'u1',
      idOffre: crypto.randomUUID(),
      dateCreation: new Date().toISOString(),
      inclureLettreMotivation: true,
      statut: this.formStatut(),
      offreTitre: this.formTitre(),
      entreprise: this.formEntreprise(),
      notes: this.formNotes()
    };
    this.candidatures.update(list => [newC, ...list]);
    this.showNewForm.set(false);
    this.formTitre.set(''); this.formEntreprise.set(''); this.formStatut.set('envoye'); this.formNotes.set('');
    try { await firstValueFrom(this.http.post(`${this.baseUrl}/candidatures`, { idOffre: newC.idOffre, inclureLettreMotivation: true })); } catch {}
  }

  selectCandidature(c: Candidature) { this.selectedCandidature.set(c); }

  getBubbleColor(name: string): string {
    const colors = ['#0C1986','#1A91F0','#34A853','#F59B00','#D93025','#651FFF'];
    let sum = 0; for (let i = 0; i < name.length; i++) sum += name.charCodeAt(i);
    return colors[sum % colors.length];
  }

  getInitials(name: string): string {
    if (!name) return '?';
    return name.split(' ').map(w => w[0]).join('').toUpperCase().substring(0, 2);
  }

  getStatusColor(status: string): string {
    const map: Record<string, string> = { envoye: '#94A3B8', vu: '#1A91F0', entretien: '#F59B00', offre: '#34A853', refuse: '#D93025' };
    return map[status] || '#94A3B8';
  }
}
