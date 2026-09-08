import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { CandidatureService, CandidatureDto, CandidatureNoteDto, CandidatureStatusHistoryDto } from '../../../services/candidature.service';
import { OfferService } from '../../../services/offer.service';

@Component({
  selector: 'app-candidature-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './candidature-detail.component.html',
  styleUrl: './candidature-detail.component.scss'
})
export class CandidatureDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly candidatureService = inject(CandidatureService);
  private readonly offerService = inject(OfferService);

  candidature = signal<CandidatureDto | null>(null);
  offer = signal<any>(null);
  loading = signal(true);
  error = signal<string | null>(null);

  newNote = signal('');
  addingNote = signal(false);

  showStatusDropdown = signal(false);

  statutOptions = [
    { value: 'BROUILLON', label: 'Brouillon' },
    { value: 'ENVOYE', label: 'Envoye' },
    { value: 'ACCUSE_RECEPTION', label: 'Accuse de reception' },
    { value: 'EN_COURS_EXAMEN', label: 'En cours d\'examen' },
    { value: 'RELANCE_NECESSAIRE', label: 'Relance necessaire' },
    { value: 'RELANCE_ENVOYEE', label: 'Relance envoyee' },
    { value: 'REPONSE_RECUE', label: 'Reponse recue' },
    { value: 'TEST_TECHNIQUE', label: 'Test technique' },
    { value: 'ENTRETIEN_PROPOSE', label: 'Entretien propose' },
    { value: 'ENTRETIEN_EFFECTUE', label: 'Entretien effectue' },
    { value: 'OFFRE_RECUE', label: 'Offre recue' },
    { value: 'ACCEPTE', label: 'Accepte' },
    { value: 'REFUSE', label: 'Refuse' },
    { value: 'ABANDONNE', label: 'Abandonne' },
  ];

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('candidatureId');
    if (!id) {
      this.error.set('ID de candidature manquant.');
      this.loading.set(false);
      return;
    }
    this.loadCandidature(id);
  }

  loadCandidature(id: string): void {
    this.loading.set(true);
    this.candidatureService.getById(id).subscribe({
      next: (c) => {
        this.candidature.set(c);
        if (c.idOffre) {
          this.offerService.getOfferById(c.idOffre).subscribe({
            next: (o) => this.offer.set(o),
            error: () => this.offer.set(null),
          });
        }
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Candidature introuvable.');
        this.loading.set(false);
      },
    });
  }

  goBack(): void {
    this.router.navigate(['/applications']);
  }

  openEmailWorkspace(): void {
    const c = this.candidature();
    if (c) this.router.navigate(['/letters', c.idCandidature], { queryParams: { source: 'applications' } });
  }

  changeStatut(nouveauStatut: string): void {
    const c = this.candidature();
    if (!c || c.statut === nouveauStatut) return;

    this.candidatureService.updateStatut(c.idCandidature, { nouveauStatut }).subscribe({
      next: (updated) => {
        this.candidature.set(updated);
        this.showStatusDropdown.set(false);
      },
      error: () => {
        this.error.set('Impossible de changer le statut.');
      },
    });
  }

  addNote(): void {
    const c = this.candidature();
    const contenu = this.newNote().trim();
    if (!c || !contenu) return;

    this.addingNote.set(true);
    this.candidatureService.addNote(c.idCandidature, { contenu, auteur: 'user' }).subscribe({
      next: (note) => {
        this.candidature.update((prev) => prev ? {
          ...prev,
          candidatureNotes: [note, ...(prev.candidatureNotes || [])],
        } : prev);
        this.newNote.set('');
        this.addingNote.set(false);
      },
      error: () => {
        this.error.set('Impossible d\'ajouter la note.');
        this.addingNote.set(false);
      },
    });
  }

  deleteCandidature(): void {
    const c = this.candidature();
    if (!c) return;
    if (!confirm(`Supprimer la candidature chez ${this.getEntreprise()} ?`)) return;

    this.candidatureService.deleteCandidature(c.idCandidature).subscribe({
      next: () => this.router.navigate(['/applications']),
      error: () => this.error.set('Impossible de supprimer la candidature.'),
    });
  }

  getEntreprise(): string {
    const c = this.candidature();
    if (!c) return '';
    return this.offer()?.entreprise || c.notes?.split('\n')[0] || 'Candidature spontanee';
  }

  getPoste(): string {
    const c = this.candidature();
    if (!c) return '';
    return this.offer()?.titre || c.notes?.split('\n')[1] || 'Poste non precise';
  }

  getStatutLabel(statut: string): string {
    const opt = this.statutOptions.find((o) => o.value === statut);
    return opt?.label || statut;
  }

  getStatutColor(statut: string): string {
    switch (statut) {
      case 'ACCEPTE': case 'OFFRE_RECUE': return 'bg-green-50 text-green-700 border-green-200';
      case 'REFUSE': case 'ABANDONNE': return 'bg-red-50 text-red-600 border-red-200';
      case 'ENTRETIEN_PROPOSE': case 'ENTRETIEN_EFFECTUE': return 'bg-purple-50 text-purple-600 border-purple-200';
      case 'RELANCE_NECESSAIRE': case 'RELANCE_ENVOYEE': return 'bg-amber-50 text-amber-700 border-amber-200';
      case 'TEST_TECHNIQUE': return 'bg-gray-100 text-gray-600 border-gray-200';
      default: return 'bg-blue-50 text-blue-600 border-blue-200';
    }
  }

  getChannelLabel(channel: string): string {
    switch (channel) {
      case 'EMAIL': return 'Email';
      case 'LINKEDIN': return 'LinkedIn';
      case 'INDEED': return 'Indeed';
      case 'WHATSAPP': return 'WhatsApp';
      case 'WEBSITE': return 'Site carriere';
      case 'PHONE': return 'Telephone';
      default: return channel || 'Autre';
    }
  }

  getChannelColor(channel: string): string {
    switch (channel) {
      case 'EMAIL': return '#465fff';
      case 'LINKEDIN': return '#0A66C2';
      case 'INDEED': return '#2164F3';
      case 'WHATSAPP': return '#25D366';
      case 'WEBSITE': return '#7c3aed';
      case 'PHONE': return '#F59B00';
      default: return '#9CA3AF';
    }
  }

  getSourceColor(source: string): string {
    switch (source) {
      case 'ai': return 'bg-purple-50 text-purple-600';
      case 'system': return 'bg-gray-100 text-gray-500';
      case 'email_reply': return 'bg-blue-50 text-blue-600';
      default: return 'bg-brand-50 text-brand-600';
    }
  }

  formatDate(dateStr: string | undefined): string {
    if (!dateStr) return '';
    const d = new Date(dateStr);
    return d.toLocaleDateString('fr-FR', { day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' });
  }
}
