import { Component, computed, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { catchError, forkJoin, of, Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged, switchMap, finalize } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ProfileService } from '@features/profile/data-access/profile.service';
import { AutocompleteService, CompanySuggestion, JobTitleSuggestion } from '../../data-access/autocomplete.service';
import { ToastService } from '@core/notifications/toast.service';
import { CandidatureDto } from '../../data-access/candidature.models';
import { CandidatureService } from '../../data-access/candidature.service';
import { OfferApiService } from '@features/offers/data-access/offer-api.service';
import {
  CandidatureCard,
  KANBAN_COLUMNS,
  buildCandidaturesCsv,
  formatDateShort,
  formatRelativeDate,
  getChannelColor,
  getChannelIcon,
  getChannelLabel,
  getCompanyColor,
  getCompanyInitials,
  getDaysSince,
  getInitials,
  getJobCategoryBadgeClass,
  getTypeBadgeClass,
  isFollowUpSuggested,
  mapKanbanToStatus,
  mapStatusToKanban,
  resolveContractType,
} from './candidature-board';

@Component({
  selector: 'app-applications',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './applications.component.html',
  styleUrl: './applications.component.scss'
})
export class ApplicationsComponent implements OnInit {
  // Display rules used by the template (see candidature-board.ts)
  readonly columns = KANBAN_COLUMNS;
  readonly getDaysSince = getDaysSince;
  readonly getJobCategoryBadgeClass = getJobCategoryBadgeClass;
  readonly getCompanyInitials = getCompanyInitials;
  readonly formatRelativeDate = formatRelativeDate;
  readonly formatDateShort = formatDateShort;
  readonly isFollowUpSuggested = isFollowUpSuggested;
  readonly getChannelLabel = getChannelLabel;
  readonly getChannelIcon = getChannelIcon;
  readonly getChannelColor = getChannelColor;
  readonly getInitials = getInitials;
  readonly getTypeBadgeClass = getTypeBadgeClass;
  readonly getCompanyColor = getCompanyColor;

  private readonly candidatureService = inject(CandidatureService);
  private readonly offerApi = inject(OfferApiService);
  private readonly profileService = inject(ProfileService);
  private readonly autocompleteService = inject(AutocompleteService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  filterStatut = signal('All');
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

    const backendStatus = mapKanbanToStatus(newStatus);
    this.cards.update((list) => list.map((c) => c.id === card.id ? { ...c, statut: newStatus } : c));
    this.candidatureService.updateStatut(card.id, { nouveauStatut: backendStatus }).subscribe({
      next: () => this.toast.success('Statut mis à jour.'),
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
            ? this.offerApi.getAnalysis(c.idOffre).pipe(catchError(() => of(null)))
            : of(null)
        );

        forkJoin(requests).subscribe({
          next: (offers) => {
            const mappedCards: CandidatureCard[] = candidatures.map((c, i) => {
              const entreprise = offers[i]?.entreprise || c.notes?.split('\n')[0]?.trim() || 'Candidature spontanée';
              const role = offers[i]?.titre || c.notes?.split('\n')[1]?.trim() || 'Poste non précisé';
              const resolvedType = resolveContractType(offers[i]?.typeContrat, role, c.notes);

              return {
                id: c.idCandidature,
                idOffre: c.idOffre,
                entreprise,
                role,
                type: resolvedType,
                statut: mapStatusToKanban(c.statut),
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

  filteredCards = computed(() => {
    const all = this.cards();
    const filter = this.filterStatut();
    const query = this.searchQuery().toLowerCase();
    return all.filter((c) => {
      if (filter !== 'All' && c.statut !== filter) return false;
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
      const newStatus = mapKanbanToStatus(col);
      this.cards.update((list) => list.map((c2) => (c2.id === card.id ? { ...c2, statut: col } : c2)));
      this.candidatureService.updateStatut(card.id, { nouveauStatut: newStatus }).subscribe({
        next: () => this.toast.success('Statut mis à jour.'),
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
        const resolvedType = resolveContractType(f.type, f.poste, f.notes);
        const newCard: CandidatureCard = {
          id: created.idCandidature,
          idOffre: created.idOffre,
          entreprise: f.entreprise,
          role: f.poste,
          type: resolvedType,
          statut: mapStatusToKanban(created.statut),
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
        this.toast.success('Candidature créée avec succès.');
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
        this.toast.success('Candidature supprimée.');
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

    const csv = buildCandidaturesCsv(rows);

    const blob = new Blob(['\uFEFF' + csv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `candidatures_${new Date().toISOString().slice(0, 10)}.csv`;
    link.click();
    URL.revokeObjectURL(url);
  }

}
