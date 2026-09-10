import { Component, computed, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { catchError, forkJoin, of, Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged, switchMap, finalize } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CandidatureService, CandidatureDto } from '../../services/candidature.service';
import { OfferService } from '../../services/offer.service';
import { ProfileService } from '../../features/profile/profile.service';
import { AutocompleteService, CompanySuggestion, JobTitleSuggestion } from '../../services/autocomplete.service';

interface CandidatureCard {
  id: string;
  idOffre: string;
  entreprise: string;
  role: string;
  type: string;
  statut: string;
  channel: string;
  applicationDate: string;
  dateCreation: string;
  hasResponse: boolean;
  responseStatus: string;
  lastResponseSnippet?: string;
  responseSummary?: string;
  recommendedAction?: string;
  lastResponseAtUtc?: string;
  followUpNeeded?: boolean;
  lastFollowUpAtUtc?: string;
  notes?: string;
}

@Component({
  selector: 'app-applications',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './applications.component.html',
  styleUrl: './applications.component.scss'
})
export class ApplicationsComponent implements OnInit {
  private readonly candidatureService = inject(CandidatureService);
  private readonly offerService = inject(OfferService);
  private readonly profileService = inject(ProfileService);
  private readonly autocompleteService = inject(AutocompleteService);
  private readonly router = inject(Router);

  filterStatut = signal('Tous');
  searchQuery = signal('');
  viewMode = signal<'kanban' | 'liste'>('kanban');
  showNewForm = signal(false);

  draggedCard = signal<CandidatureCard | null>(null);
  dragOverCol = signal<string | null>(null);

  activeStatusMenu = signal<string | null>(null);

  // ── Autocomplete intelligent (Entreprises & Postes) ────────────────────────
  companySuggestions = signal<CompanySuggestion[]>([]);
  jobSuggestions = signal<JobTitleSuggestion[]>([]);
  isSearchingCompany = signal<boolean>(false);
  activeCompanyIndex = signal<number>(-1);
  activeJobIndex = signal<number>(-1);
  selectedCompanyMeta = signal<{ domain?: string; logo?: string } | null>(null);

  entrepriseSuggestions = computed(() => this.companySuggestions().map((c) => c.name));
  posteSuggestions = computed(() => this.jobSuggestions().map((j) => j.title));

  private readonly companySearch$ = new Subject<string>();

  constructor() {
    this.companySearch$
      .pipe(
        debounceTime(220),
        distinctUntilChanged(),
        switchMap((query) => {
          const trimmed = (query || '').trim();
          if (trimmed.length < 2) {
            this.isSearchingCompany.set(false);
            return of([]);
          }
          this.isSearchingCompany.set(true);
          const history = this.cards().map((c) => c.entreprise);
          return this.autocompleteService.searchCompanies(trimmed, history).pipe(
            finalize(() => this.isSearchingCompany.set(false))
          );
        }),
        takeUntilDestroyed()
      )
      .subscribe((results) => {
        this.companySuggestions.set(results);
        this.activeCompanyIndex.set(-1);
      });
  }

  newForm = signal({
    entreprise: '',
    poste: '',
    type: 'Stage PFE',
    idOffre: '',
    channel: 'EMAIL',
    channelUrl: '',
    channelContact: '',
    applicationDate: new Date().toISOString().slice(0, 10),
    appliedManually: true,
    inclureLettreMotivation: false,
    language: 'AUTO',
    notes: '',
  });
  cards = signal<CandidatureCard[]>([]);
  loading = signal(true);
  loadingMore = signal(false);
  hasMore = signal(false);
  error = signal<string | null>(null);

  private offset = 0;
  private readonly pageSize = 10;

  ngOnInit(): void {
    this.load(true);
  }

  toggleStatusMenu(cardId: string, event: Event): void {
    event.stopPropagation();
    const current = this.activeStatusMenu();
    this.activeStatusMenu.set(current === cardId ? null : cardId);
  }

  quickChangeStatut(card: CandidatureCard, newStatus: string, event: Event): void {
    event.stopPropagation();
    this.activeStatusMenu.set(null);
    if (card.statut === newStatus) return;

    const backendStatus = this.mapKanbanToStatus(newStatus);
    this.cards.update((list) => list.map((c) => c.id === card.id ? { ...c, statut: newStatus } : c));
    this.candidatureService.updateStatut(card.id, { nouveauStatut: backendStatus }).subscribe({
      error: () => {
        this.cards.update((list) => list.map((c) => c.id === card.id ? { ...c, statut: card.statut } : c));
      }
    });
  }

  onEntrepriseInput(value: string): void {
    this.updateForm('entreprise', value);
    if (!value || value.trim().length < 2) {
      this.companySuggestions.set([]);
      this.isSearchingCompany.set(false);
      this.selectedCompanyMeta.set(null);
      this.activeCompanyIndex.set(-1);
      return;
    }
    if (this.selectedCompanyMeta()) {
      this.selectedCompanyMeta.set(null);
    }
    this.companySearch$.next(value);
  }

  selectEntreprise(name: string): void {
    const found = this.companySuggestions().find((c) => c.name === name);
    if (found) {
      this.selectCompany(found);
    } else {
      this.updateForm('entreprise', name);
      this.companySuggestions.set([]);
      this.activeCompanyIndex.set(-1);
    }
  }

  selectCompany(comp: CompanySuggestion): void {
    this.updateForm('entreprise', comp.name);
    this.selectedCompanyMeta.set({ domain: comp.domain, logo: comp.logo });
    this.companySuggestions.set([]);
    this.activeCompanyIndex.set(-1);

    // Auto-remplissage intelligent du site web si vide
    if (comp.domain && !this.newForm().channelUrl) {
      this.updateForm('channelUrl', `https://${comp.domain}`);
    }
  }

  clearCompany(): void {
    this.updateForm('entreprise', '');
    this.selectedCompanyMeta.set(null);
    this.companySuggestions.set([]);
    this.activeCompanyIndex.set(-1);
  }

  onCompanyKeydown(event: KeyboardEvent): void {
    const list = this.companySuggestions();
    if (!list.length) return;

    if (event.key === 'ArrowDown') {
      event.preventDefault();
      this.activeCompanyIndex.update((i) => (i + 1 < list.length ? i + 1 : 0));
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      this.activeCompanyIndex.update((i) => (i - 1 >= 0 ? i - 1 : list.length - 1));
    } else if (event.key === 'Enter') {
      const idx = this.activeCompanyIndex();
      if (idx >= 0 && idx < list.length) {
        event.preventDefault();
        this.selectCompany(list[idx]);
      }
    } else if (event.key === 'Escape') {
      this.companySuggestions.set([]);
      this.activeCompanyIndex.set(-1);
    }
  }

  onPosteInput(value: string): void {
    this.updateForm('poste', value);
    if (!value || value.trim().length < 2) {
      this.jobSuggestions.set([]);
      this.activeJobIndex.set(-1);
      return;
    }
    const history = this.cards().map((c) => c.role);
    const results = this.autocompleteService.searchJobTitles(value, history);
    this.jobSuggestions.set(results);
    this.activeJobIndex.set(-1);
  }

  selectPoste(name: string): void {
    const found = this.jobSuggestions().find((j) => j.title === name);
    if (found) {
      this.selectJob(found);
    } else {
      this.updateForm('poste', name);
      this.jobSuggestions.set([]);
      this.activeJobIndex.set(-1);
    }
  }

  selectJob(job: JobTitleSuggestion): void {
    this.updateForm('poste', job.title);
    this.jobSuggestions.set([]);
    this.activeJobIndex.set(-1);
  }

  clearPoste(): void {
    this.updateForm('poste', '');
    this.jobSuggestions.set([]);
    this.activeJobIndex.set(-1);
  }

  onJobKeydown(event: KeyboardEvent): void {
    const list = this.jobSuggestions();
    if (!list.length) return;

    if (event.key === 'ArrowDown') {
      event.preventDefault();
      this.activeJobIndex.update((i) => (i + 1 < list.length ? i + 1 : 0));
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      this.activeJobIndex.update((i) => (i - 1 >= 0 ? i - 1 : list.length - 1));
    } else if (event.key === 'Enter') {
      const idx = this.activeJobIndex();
      if (idx >= 0 && idx < list.length) {
        event.preventDefault();
        this.selectJob(list[idx]);
      }
    } else if (event.key === 'Escape') {
      this.jobSuggestions.set([]);
      this.activeJobIndex.set(-1);
    }
  }

  getJobCategoryBadgeClass(category: string): string {
    switch (category) {
      case 'Ingénierie & Dev':
        return 'bg-blue-50 text-blue-700 border border-blue-200';
      case 'Data & IA':
        return 'bg-purple-50 text-purple-700 border border-purple-200';
      case 'Cloud & DevOps':
        return 'bg-cyan-50 text-cyan-700 border border-cyan-200';
      case 'Produit & Design':
        return 'bg-rose-50 text-rose-700 border border-rose-200';
      case 'Cybersécurité':
        return 'bg-emerald-50 text-emerald-700 border border-emerald-200';
      case 'Management':
        return 'bg-amber-50 text-amber-700 border border-amber-200';
      case 'QA & Test':
        return 'bg-orange-50 text-orange-700 border border-orange-200';
      default:
        return 'bg-gray-100 text-gray-700 border border-gray-200';
    }
  }

  getCompanyInitials(name: string): string {
    if (!name) return 'CO';
    const parts = name.trim().split(/\s+/);
    if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
    return (parts[0][0] + parts[1][0]).toUpperCase();
  }

  load(reset = false): void {
    if (reset) {
      this.offset = 0;
      this.cards.set([]);
      this.loading.set(true);
    } else {
      this.loadingMore.set(true);
    }
    this.error.set(null);

    this.candidatureService.getMyCandidaturesPaged(this.offset, this.pageSize).subscribe({
      next: (page) => {
        const candidatures = page.items;
        if (candidatures.length === 0 && reset) {
          this.hasMore.set(false);
          this.loading.set(false);
          this.loadingMore.set(false);
          return;
        }

        const requests = candidatures.map((c) =>
          c.idOffre
            ? this.offerService.getOfferById(c.idOffre).pipe(catchError(() => of(null)))
            : of(null)
        );

        forkJoin(requests).subscribe({
          next: (offers) => {
            const mappedCards: CandidatureCard[] = candidatures.map((c, i) => {
              const entreprise = offers[i]?.entreprise || c.notes?.split('\n')[0]?.trim() || 'Candidature spontanée';
              const role = offers[i]?.titre || c.notes?.split('\n')[1]?.trim() || 'Poste non précisé';
              const resolvedType = this.resolveContractType(offers[i]?.typeContrat, role, c.notes);

              return {
                id: c.idCandidature,
                idOffre: c.idOffre,
                entreprise,
                role,
                type: resolvedType,
                statut: this.mapStatusToKanban(c.statut),
                channel: c.channel,
                applicationDate: c.applicationDate,
                dateCreation: c.dateCreation,
                hasResponse: c.hasResponse,
                responseStatus: c.responseStatus,
                lastResponseSnippet: c.lastResponseSnippet,
                responseSummary: c.responseSummary,
                recommendedAction: c.recommendedAction,
                lastResponseAtUtc: c.lastResponseAtUtc,
                followUpNeeded: c.followUpNeeded,
                lastFollowUpAtUtc: c.lastFollowUpAtUtc,
                notes: c.notes,
              };
            });

            this.cards.update((existing) => (reset ? mappedCards : [...existing, ...mappedCards]));
            this.offset += mappedCards.length;
            this.hasMore.set(page.hasMore);
            this.loading.set(false);
            this.loadingMore.set(false);
          },
          error: () => {
            this.error.set('Impossible de charger les details des offres.');
            this.loading.set(false);
            this.loadingMore.set(false);
          },
        });
      },
      error: () => {
        this.error.set('Impossible de charger vos candidatures.');
        this.loading.set(false);
        this.loadingMore.set(false);
      },
    });
  }

  loadMore(): void {
    if (this.loadingMore() || !this.hasMore()) return;
    this.load(false);
  }

  updateForm(key: string, value: string | boolean): void {
    this.newForm.update((prev) => ({ ...prev, [key]: value }));
  }

  mapStatusToKanban(backendStatus: string): string {
    switch (backendStatus) {
      case 'BROUILLON': return 'brouillon';
      case 'ENVOYE': return 'envoye';
      case 'ACCUSE_RECEPTION': return 'envoye';
      case 'EN_COURS_EXAMEN': return 'en-attente';
      case 'RELANCE_NECESSAIRE': return 'relance';
      case 'RELANCE_ENVOYEE': return 'relance';
      case 'REPONSE_RECUE': return 'en-attente';
      case 'TEST_TECHNIQUE': return 'test-tech';
      case 'ENTRETIEN_PROPOSE': return 'entretien';
      case 'ENTRETIEN_EFFECTUE': return 'entretien';
      case 'OFFRE_RECUE': return 'accepte';
      case 'ACCEPTE': return 'accepte';
      case 'REFUSE': return 'refuse';
      case 'ABANDONNE': return 'refuse';
      default: return 'envoye';
    }
  }

  mapKanbanToStatus(col: string): string {
    switch (col) {
      case 'brouillon': return 'BROUILLON';
      case 'envoye': return 'ENVOYE';
      case 'en-attente': return 'EN_COURS_EXAMEN';
      case 'relance': return 'RELANCE_NECESSAIRE';
      case 'entretien': return 'ENTRETIEN_PROPOSE';
      case 'test-tech': return 'TEST_TECHNIQUE';
      case 'accepte': return 'ACCEPTE';
      case 'refuse': return 'REFUSE';
      default: return 'ENVOYE';
    }
  }

  columns = [
    { key: 'brouillon', label: 'Brouillon', color: '#9CA3AF' },
    { key: 'envoye', label: 'Envoye', color: '#465fff' },
    { key: 'en-attente', label: 'En attente', color: '#F59B00' },
    { key: 'relance', label: 'Relance', color: '#F97316' },
    { key: 'entretien', label: 'Entretien', color: '#7c3aed' },
    { key: 'test-tech', label: 'Test Tech', color: '#757575' },
    { key: 'accepte', label: 'Accepte', color: '#34A853' },
    { key: 'refuse', label: 'Refuse', color: '#D93025' },
  ];

  filteredCards = computed(() => {
    const all = this.cards();
    const filter = this.filterStatut();
    const query = this.searchQuery().toLowerCase();
    return all.filter((c) => {
      if (filter !== 'Tous' && c.statut !== filter) return false;
      if (query && !c.entreprise.toLowerCase().includes(query) && !c.role.toLowerCase().includes(query)) return false;
      return true;
    });
  });

  stats = computed(() => {
    const all = this.cards();
    return {
      total: all.length,
      envoyees: all.filter((c) => c.statut === 'envoye').length,
      enAttente: all.filter((c) => c.statut === 'en-attente').length,
      acceptees: all.filter((c) => c.statut === 'accepte').length,
      avecReponse: all.filter((c) => c.hasResponse).length,
      tauxReponse: all.length > 0 ? Math.round((all.filter((c) => c.hasResponse).length / all.length) * 100) : 0,
    };
  });

  getColumnCards(key: string): CandidatureCard[] {
    return this.filteredCards().filter((c) => c.statut === key);
  }

  setFilter(s: string): void { this.filterStatut.set(s); }
  setView(v: 'kanban' | 'liste'): void { this.viewMode.set(v); }

  openEmailWorkspace(candidatureId: string): void {
    this.router.navigate(['/letters', candidatureId], { queryParams: { source: 'applications' } });
  }

  openDetails(candidatureId: string): void {
    this.router.navigate(['/applications', candidatureId]);
  }

  prepareForInterview(card: CandidatureCard): void {
    const sessionConfig = {
      mode: 'offer',
      domain: 'software',
      level: 'mid',
      duration_minutes: 15,
      language: 'fr',
      focus_areas: ['Technique', 'Motivation'],
      offer_id: card.idOffre || '',
      job_title: card.role,
      company: card.entreprise,
      display_title: `Entretien ${card.role} @ ${card.entreprise}`,
      display_emoji: '🎯'
    };
    this.router.navigate(['/chatbot/interview'], { state: { sessionConfig } });
  }

  onDragStart(c: CandidatureCard): void { this.draggedCard.set(c); }
  onDragOver(e: DragEvent, col: string): void { e.preventDefault(); this.dragOverCol.set(col); }
  onDragLeave(): void { this.dragOverCol.set(null); }
  onDrop(e: DragEvent, col: string): void {
    e.preventDefault();
    this.dragOverCol.set(null);
    const card = this.draggedCard();
    if (card && card.statut !== col) {
      const newStatus = this.mapKanbanToStatus(col);
      this.cards.update((list) => list.map((c2) => (c2.id === card.id ? { ...c2, statut: col } : c2)));
      this.candidatureService.updateStatut(card.id, { nouveauStatut: newStatus }).subscribe({
        error: () => {
          this.cards.update((list) => list.map((c2) => (c2.id === card.id ? { ...c2, statut: card.statut } : c2)));
        }
      });
    }
    this.draggedCard.set(null);
  }

  createCandidature(): void {
    const f = this.newForm();
    if (!f.entreprise || !f.poste) return;

    const payload = {
      channel: f.channel,
      channelUrl: f.channelUrl || undefined,
      channelContact: f.channelContact || undefined,
      applicationDate: f.applicationDate,
      appliedManually: f.appliedManually,
      inclureLettreMotivation: f.inclureLettreMotivation,
      language: f.language,
      notes: `${f.entreprise}\n${f.poste}\n${f.type}${f.notes ? '\n' + f.notes : ''}`,
    };

    this.candidatureService.create(payload).subscribe({
      next: (created) => {
        const resolvedType = this.resolveContractType(f.type, f.poste, f.notes);
        const newCard: CandidatureCard = {
          id: created.idCandidature,
          idOffre: created.idOffre,
          entreprise: f.entreprise,
          role: f.poste,
          type: resolvedType,
          statut: this.mapStatusToKanban(created.statut),
          channel: created.channel,
          applicationDate: created.applicationDate,
          dateCreation: created.dateCreation,
          hasResponse: created.hasResponse,
          responseStatus: created.responseStatus,
          notes: created.notes,
        };
        this.cards.update((list) => [newCard, ...list]);
        this.showNewForm.set(false);
        this.resetForm();
      },
      error: () => {
        this.error.set('Impossible de créer la candidature.');
      }
    });
  }

  deleteCandidature(card: CandidatureCard): void {
    if (!confirm(`Supprimer la candidature chez ${card.entreprise} ?`)) return;
    this.candidatureService.deleteCandidature(card.id).subscribe({
      next: () => {
        this.cards.update((list) => list.filter((c) => c.id !== card.id));
      },
      error: () => {
        this.error.set('Impossible de supprimer la candidature.');
      }
    });
  }

  openNewApplicationModal(): void {
    this.resetForm();
    this.showNewForm.set(true);
  }

  closeNewApplicationModal(): void {
    this.showNewForm.set(false);
    this.companySuggestions.set([]);
    this.jobSuggestions.set([]);
    this.selectedCompanyMeta.set(null);
    this.activeCompanyIndex.set(-1);
    this.activeJobIndex.set(-1);
  }

  private resetForm(): void {
    this.companySuggestions.set([]);
    this.jobSuggestions.set([]);
    this.selectedCompanyMeta.set(null);
    this.activeCompanyIndex.set(-1);
    this.activeJobIndex.set(-1);
    this.newForm.set({
      entreprise: '',
      poste: '',
      type: 'Stage PFE',
      idOffre: '',
      channel: 'EMAIL',
      channelUrl: '',
      channelContact: '',
      applicationDate: new Date().toISOString().slice(0, 10),
      appliedManually: true,
      inclureLettreMotivation: false,
      language: 'AUTO',
      notes: '',
    });
  }

  exportCsv(): void {
    const rows = this.filteredCards();
    if (rows.length === 0) return;

    const headers = ['Entreprise', 'Poste', 'Canal', 'Statut', 'Date', 'Reponse', 'Relance'];
    const csvRows = [headers.join(',')];

    for (const c of rows) {
      const row = [
        `"${(c.entreprise || '').replace(/"/g, '""')}"`,
        `"${(c.role || '').replace(/"/g, '""')}"`,
        `"${c.channel}"`,
        `"${c.statut}"`,
        `"${c.applicationDate}"`,
        `"${c.hasResponse ? 'Oui' : 'Non'}"`,
        `"${c.followUpNeeded ? 'Oui' : 'Non'}"`,
      ];
      csvRows.push(row.join(','));
    }

    const blob = new Blob(['\uFEFF' + csvRows.join('\n')], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `candidatures_${new Date().toISOString().slice(0, 10)}.csv`;
    link.click();
    URL.revokeObjectURL(url);
  }

  resolveContractType(rawType?: string | null, role?: string | null, notes?: string | null): string {
    const typeStr = (rawType || '').trim();
    if (typeStr && typeStr !== 'CDI' && typeStr !== 'Non spécifié') {
      if (/pfe/i.test(typeStr)) return 'Stage PFE';
      if (/pfa/i.test(typeStr)) return 'Stage PFA';
      if (/stage|intern/i.test(typeStr)) return 'Stage';
      if (/alternan/i.test(typeStr)) return 'Alternance';
      if (/freelance/i.test(typeStr)) return 'Freelance';
      if (/cdd/i.test(typeStr)) return 'CDD';
      return typeStr;
    }

    if (notes) {
      const lines = notes.split('\n').map((l) => l.trim()).filter(Boolean);
      if (lines.length >= 3) {
        const candidate = lines[2];
        if (/pfe/i.test(candidate)) return 'Stage PFE';
        if (/pfa/i.test(candidate)) return 'Stage PFA';
        if (/stage|intern/i.test(candidate)) return 'Stage';
        if (/alternan/i.test(candidate)) return 'Alternance';
        if (/freelance/i.test(candidate)) return 'Freelance';
        if (/cdd/i.test(candidate)) return 'CDD';
        if (/cdi/i.test(candidate)) return 'CDI';
        if (candidate.length > 2 && candidate.length < 30) return candidate;
      }
    }

    const combined = `${role || ''} ${notes || ''}`.toLowerCase();
    if (combined.includes('pfe') || combined.includes("fin d'études") || combined.includes('fin d’études')) {
      return 'Stage PFE';
    }
    if (combined.includes('pfa')) {
      return 'Stage PFA';
    }
    if (combined.includes('intern') || combined.includes('stagiaire') || combined.includes('stage')) {
      return 'Stage';
    }
    if (combined.includes('alternan') || combined.includes('apprenti') || combined.includes('contrat pro')) {
      return 'Alternance';
    }
    if (combined.includes('freelance') || combined.includes('independant') || combined.includes('consultant')) {
      return 'Freelance';
    }
    if (combined.includes('cdd')) {
      return 'CDD';
    }
    if (combined.includes('cdi')) {
      return 'CDI';
    }

    return rawType && rawType !== 'CDI' ? rawType : 'Stage PFE';
  }

  getDaysSince(dateStr?: string | null): number {
    if (!dateStr) return 0;
    const then = new Date(dateStr).getTime();
    if (isNaN(then)) return 0;
    const now = new Date().getTime();
    const diffDays = Math.floor((now - then) / (1000 * 60 * 60 * 24));
    return diffDays >= 0 ? diffDays : 0;
  }

  formatRelativeDate(dateStr?: string | null): string {
    if (!dateStr) return '';
    const days = this.getDaysSince(dateStr);
    if (days === 0) return "Aujourd'hui";
    if (days === 1) return 'Hier';
    return `J+${days}`;
  }

  formatDateShort(dateStr?: string | null): string {
    if (!dateStr) return 'Date non précisée';
    const d = new Date(dateStr);
    if (isNaN(d.getTime())) return 'Date invalide';
    return d.toLocaleDateString('fr-FR', { day: '2-digit', month: 'short', year: 'numeric' });
  }

  isFollowUpSuggested(card: CandidatureCard): boolean {
    if (card.statut !== 'en-attente' && card.statut !== 'envoye') return false;
    if (card.hasResponse) return false;
    return this.getDaysSince(card.applicationDate) >= 5;
  }

  getChannelLabel(channel: string): string {
    switch (channel) {
      case 'EMAIL': return 'Email';
      case 'LINKEDIN': return 'LinkedIn';
      case 'INDEED': return 'Indeed';
      case 'WHATSAPP': return 'WhatsApp';
      case 'WEBSITE': return 'Site web';
      case 'PHONE': return 'Téléphone';
      default: return channel || 'Direct';
    }
  }

  getChannelIcon(channel: string): string {
    switch (channel) {
      case 'EMAIL': return 'mail';
      case 'LINKEDIN': return 'in';
      case 'INDEED': return 'i';
      case 'WHATSAPP': return 'chat';
      case 'WEBSITE': return 'language';
      case 'PHONE': return 'call';
      default: return 'send';
    }
  }

  getChannelColor(channel: string): string {
    switch (channel) {
      case 'EMAIL': return '#465fff';
      case 'LINKEDIN': return '#0A66C2';
      case 'INDEED': return '#2164F3';
      case 'WHATSAPP': return '#128C7E';
      case 'WEBSITE': return '#6366F1';
      case 'PHONE': return '#F59B00';
      default: return '#6B7280';
    }
  }

  getInitials(name: string): string {
    return name.split(' ').map((w) => w[0]).join('').toUpperCase().slice(0, 2) || '?';
  }

  getTypeBadgeClass(type: string): string {
    const map: Record<string, string> = {
      'Stage PFE': 'bg-orange-50 text-orange-700 border border-orange-200/80',
      'Stage PFA': 'bg-amber-50 text-amber-700 border border-amber-200/80',
      Stage: 'bg-indigo-50 text-indigo-700 border border-indigo-200/80',
      Alternance: 'bg-sky-50 text-sky-700 border border-sky-200/80',
      CDI: 'bg-emerald-50 text-emerald-700 border border-emerald-200/80',
      CDD: 'bg-purple-50 text-purple-700 border border-purple-200/80',
      Freelance: 'bg-teal-50 text-teal-700 border border-teal-200/80',
    };
    return map[type] || 'bg-gray-100 text-gray-700 border border-gray-200';
  }

  getCompanyColor(name: string): string {
    const colors = ['#465fff', '#8f4900', '#34A853', '#F59B00', '#D93025', '#7d5700', '#1A91F0', '#b35e00'];
    let sum = 0;
    for (let i = 0; i < name.length; i += 1) sum += name.charCodeAt(i);
    return colors[sum % colors.length];
  }

  historyEntries = [
    { initial: 'S', company: 'Startup IA', role: 'Freelance DevOps', date: '12 Oct. 2023', issue: 'Refuse', issueClass: 'error' },
    { initial: 'M', company: 'MedTech Hub', role: 'Backend Dev', date: '05 Oct. 2023', issue: 'Retire', issueClass: 'neutral' },
    { initial: 'A', company: 'Alten Maroc', role: 'Apprenti QA', date: '22 Sep. 2023', issue: 'Accepte', issueClass: 'success' },
  ];
}
