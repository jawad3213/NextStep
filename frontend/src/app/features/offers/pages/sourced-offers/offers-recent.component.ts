import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { Subscription } from 'rxjs';
import { extractApiError } from '@core/http/extract-api-error';
import { ToastService } from '@core/notifications/toast.service';
import { NormalizedContractType, PostedWindow, ScrapeProvider, ScrapeSessionDto } from '../../data-access/sourced-offers.models';
import { SourcedOffersApiService } from '../../data-access/sourced-offers-api.service';
import { SourcedOfferListItemDto } from '../../data-access/sourced-offers.models';

type ProviderSummary = {
  key: ScrapeProvider;
  label: string;
  tone: string;
};

@Component({
  selector: 'app-offers-recent',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './offers-recent.component.html',
  styleUrl: '../offers/offers.component.scss',
})
export class OffersRecentComponent implements OnInit, OnDestroy {
  private readonly toast = inject(ToastService);
  private readonly sourcedOffersApi = inject(SourcedOffersApiService);
  private readonly router = inject(Router);

  readonly providers: ProviderSummary[] = [
    { key: 'linkedin', label: 'LinkedIn', tone: 'bg-sky-50 text-sky-700 border-sky-200' },
    { key: 'indeed', label: 'Indeed', tone: 'bg-violet-50 text-violet-700 border-violet-200' },
    { key: 'glassdoor', label: 'Glassdoor', tone: 'bg-emerald-50 text-emerald-700 border-emerald-200' },
  ];

  readonly contractOptions: { value: NormalizedContractType; label: string }[] = [
    { value: 'internship', label: 'Internship' },
    { value: 'cdi', label: 'CDI' },
    { value: 'cdd', label: 'CDD' },
    { value: 'freelance', label: 'Freelance' },
    { value: 'alternance', label: 'Alternance' },
    { value: 'part_time', label: 'Part-time' },
    { value: 'full_time', label: 'Full-time' },
    { value: 'temporary', label: 'Temporary' },
  ];

  readonly postedWindows: { value: PostedWindow; label: string }[] = [
    { value: '24h', label: 'Last 24h' },
    { value: '3d', label: 'Last 3 days' },
    { value: '7d', label: 'Last 7 days' },
    { value: '14d', label: 'Last 14 days' },
    { value: '30d', label: 'Last 30 days' },
    { value: 'any', label: 'Any time' },
  ];

  readonly workflowTabs = [
    { key: 'all', label: 'All' },
    { key: 'saved', label: 'Saved' },
    { key: 'shortlisted', label: 'Shortlisted' },
    { key: 'archived', label: 'Archived' },
  ] as const;

  // Autocomplete Suggestions
  readonly suggestedKeywords = [
    'Software Engineer', 'Frontend Developer', 'Backend Developer', 'Fullstack Developer', 
    'Data Scientist', 'Data Analyst', 'DevOps Engineer', 'Product Manager', 'UX/UI Designer',
    'Mobile Developer', 'iOS Developer', 'Android Developer', 'QA Engineer', 'System Administrator'
  ];

  readonly suggestedLocations = [
    'Maroc', 'Casablanca', 'Rabat', 'Marrakech', 'Tanger', 
    'France', 'Paris', 'Lyon', 'Remote', 'Worldwide', 'United States', 'United Kingdom'
  ];

  readonly suggestedCountries = [
    { code: 'ma', label: 'Maroc' },
    { code: 'fr', label: 'France' },
    { code: 'us', label: 'United States' },
    { code: 'gb', label: 'United Kingdom' },
    { code: 'ca', label: 'Canada' },
    { code: 'de', label: 'Germany' },
    { code: 'ae', label: 'UAE' },
    { code: 'sa', label: 'Saudi Arabia' }
  ];

  readonly selectedProviders = signal<ScrapeProvider[]>(['linkedin', 'indeed', 'glassdoor']);
  readonly selectedContractTypes = signal<NormalizedContractType[]>([]);
  readonly selectedPostedWindow = signal<PostedWindow>('24h');
  readonly workflowTab = signal<'all' | 'saved' | 'shortlisted' | 'archived'>('all');

  readonly keywords = signal('software engineer');
  readonly location = signal('Casablanca');
  readonly indeedCountryCode = signal('ma');

  /** Offers shown per page; the cached list endpoint returns at most MAX_CACHED. */
  readonly pageSize = 24;
  private readonly maxCached = 100;
  readonly limit = signal(this.pageSize);
  readonly isLoadingMore = signal(false);
  private listSub: Subscription | null = null;
  private refreshSub: Subscription | null = null;

  // Dropdown States
  readonly showKeywordSuggestions = signal(false);
  readonly showLocationSuggestions = signal(false);
  readonly showCountrySuggestions = signal(false);

  readonly filteredKeywords = computed(() => {
    const q = this.keywords().toLowerCase();
    return this.suggestedKeywords.filter(k => k.toLowerCase().includes(q) && k.toLowerCase() !== q);
  });

  readonly filteredLocations = computed(() => {
    const q = this.location().toLowerCase();
    return this.suggestedLocations.filter(l => l.toLowerCase().includes(q) && l.toLowerCase() !== q);
  });

  readonly filteredCountries = computed(() => {
    const q = this.indeedCountryCode().toLowerCase();
    return this.suggestedCountries.filter(c => 
      c.code.toLowerCase().includes(q) || c.label.toLowerCase().includes(q)
    );
  });

  readonly searchTerm = signal('');
  readonly sortBy = signal<'score' | 'recent' | 'company' | 'title'>('score');
  readonly isLoading = signal(false);
  readonly actionOfferId = signal<string | null>(null);
  readonly errorMessage = signal('');
  readonly warnings = signal<string[]>([]);
  readonly offers = signal<SourcedOfferListItemDto[]>([]);
  readonly lastSession = signal<ScrapeSessionDto | null>(null);

  readonly filteredOffers = computed(() => {
    const search = this.searchTerm().trim().toLowerCase();
    const sort = this.sortBy();
    const result = this.offers().filter((offer) => {
      if (!search) return true;
      return (
        offer.title.toLowerCase().includes(search) ||
        (offer.company ?? '').toLowerCase().includes(search) ||
        (offer.location ?? '').toLowerCase().includes(search) ||
        (offer.description ?? '').toLowerCase().includes(search) ||
        offer.matchedItTerms.some((tag) => tag.toLowerCase().includes(search))
      );
    });

    result.sort((a, b) => {
      switch (sort) {
        case 'score':
          return (b.aiScore ?? -1) - (a.aiScore ?? -1)
            || new Date(b.lastSeenAtUtc).getTime() - new Date(a.lastSeenAtUtc).getTime();
        case 'company':
          return (a.company ?? '').localeCompare(b.company ?? '');
        case 'title':
          return a.title.localeCompare(b.title);
        case 'recent':
        default:
          return new Date(b.lastSeenAtUtc).getTime() - new Date(a.lastSeenAtUtc).getTime();
      }
    });

    return result;
  });

  readonly providerCounts = computed(() => {
    const counts: Record<ScrapeProvider, number> = { linkedin: 0, indeed: 0, glassdoor: 0 };
    for (const offer of this.offers()) {
      if (offer.provider in counts) counts[offer.provider]++;
    }
    return counts;
  });

  /** A full page came back, so the cache may hold more. */
  readonly canLoadMore = computed(() =>
    !this.isLoading() && this.offers().length >= this.limit() && this.limit() < this.maxCached
  );

  readonly skeletonCards = [1, 2, 3, 4];

  ngOnInit(): void {
    this.loadCachedOffers();
  }

  ngOnDestroy(): void {
    this.listSub?.unsubscribe();
    this.refreshSub?.unsubscribe();
  }

  /** Reloads page 1 of the cache (after a filter change). */
  loadCachedOffers(): void {
    this.limit.set(this.pageSize);
    this.fetchCachedOffers();
  }

  loadMore(): void {
    this.limit.update((n) => Math.min(n + this.pageSize, this.maxCached));
    this.isLoadingMore.set(true);
    this.fetchCachedOffers();
  }

  private fetchCachedOffers(): void {
    // Only the latest filter state counts: a slower, older response must not overwrite it.
    this.listSub?.unsubscribe();
    this.isLoading.set(true);
    this.errorMessage.set('');

    this.listSub = this.sourcedOffersApi.getSourcedOffers({
      keywords: this.keywords(),
      location: this.location(),
      providers: this.selectedProviders(),
      limit: this.limit(),
      postedWindow: this.selectedPostedWindow(),
      contractTypes: this.selectedContractTypes(),
      indeedCountryCode: this.indeedCountryCode(),
      workflowState: this.workflowTab() === 'all'
        ? null
        : (this.workflowTab() as 'saved' | 'shortlisted' | 'archived'),
    }).subscribe({
      next: (offers) => {
        this.offers.set(offers);
        this.isLoading.set(false);
        this.isLoadingMore.set(false);
      },
      error: (err) => {
        this.errorMessage.set(extractApiError(err).message || 'Unable to load sourced offers.');
        if (!this.isLoadingMore()) this.offers.set([]);
        this.isLoading.set(false);
        this.isLoadingMore.set(false);
      },
    });
  }

  // Autocomplete Selectors
  selectKeyword(kw: string): void {
    this.keywords.set(kw);
    this.showKeywordSuggestions.set(false);
  }

  selectLocation(loc: string): void {
    this.location.set(loc);
    this.showLocationSuggestions.set(false);
  }

  selectCountry(code: string): void {
    this.indeedCountryCode.set(code);
    this.showCountrySuggestions.set(false);
  }

  refreshOffers(): void {
    this.listSub?.unsubscribe();
    this.refreshSub?.unsubscribe();
    this.isLoading.set(true);
    this.isLoadingMore.set(false);
    this.errorMessage.set('');
    this.warnings.set([]);
    this.limit.set(this.pageSize);

    this.refreshSub = this.sourcedOffersApi.searchSourcedOffers({
      keywords: this.keywords(),
      location: this.location(),
      providers: this.selectedProviders(),
      limit: this.pageSize,
      postedWindow: this.selectedPostedWindow(),
      contractTypes: this.selectedContractTypes(),
      indeedCountryCode: this.indeedCountryCode(),
    }).subscribe({
      next: (response) => {
        this.offers.set(response.offers ?? []);
        this.lastSession.set(response.session ?? null);
        this.warnings.set(response.warnings ?? []);
        this.isLoading.set(false);
        const count = response.offers?.length ?? 0;
        if (count > 0) {
          this.toast.success(`${count} offer${count > 1 ? 's' : ''} refreshed.`);
        } else {
          this.toast.info('No new offers found for these filters.');
        }
      },
      error: (err) => {
        this.errorMessage.set(extractApiError(err).message || 'Unable to refresh sourced offers.');
        this.isLoading.set(false);
      },
    });
  }

  toggleProvider(provider: ScrapeProvider): void {
    const next = new Set(this.selectedProviders());
    if (next.has(provider)) next.delete(provider);
    else next.add(provider);
    this.selectedProviders.set([...next]);
    this.loadCachedOffers();
  }

  toggleContractType(contractType: NormalizedContractType): void {
    const next = new Set(this.selectedContractTypes());
    if (next.has(contractType)) next.delete(contractType);
    else next.add(contractType);
    this.selectedContractTypes.set([...next]);
    this.loadCachedOffers();
  }

  setWorkflowTab(tab: 'all' | 'saved' | 'shortlisted' | 'archived'): void {
    this.workflowTab.set(tab);
    this.loadCachedOffers();
  }

  saveOffer(offer: SourcedOfferListItemDto): void {
    this.patchOfferState(offer.id, { isSaved: !offer.isSaved });
  }

  shortlistOffer(offer: SourcedOfferListItemDto): void {
    this.patchOfferState(offer.id, { isShortlisted: !offer.isShortlisted, isSaved: true });
  }

  archiveOffer(offer: SourcedOfferListItemDto): void {
    this.patchOfferState(offer.id, { isArchived: !offer.isArchived });
  }

  openOfferDetail(offerId: string): void {
    this.router.navigate(['/offers-recent', offerId]);
  }

  analyzeOffer(offer: SourcedOfferListItemDto): void {
    this.actionOfferId.set(offer.id);
    this.errorMessage.set('');

    this.sourcedOffersApi.promoteSourcedOffer(offer.id).subscribe({
      next: (promotion) => {
        this.actionOfferId.set(null);
        this.router.navigate(['/offers/analyze'], {
          queryParams: {
            offerId: promotion.offerId,
            autoAnalyze: 1,
          },
        });
        this.toast.success('Analysis started.');
      },
      error: (err) => {
        this.actionOfferId.set(null);
        this.errorMessage.set(extractApiError(err).message || 'Unable to promote sourced offer.');
      },
    });
  }

  formatLastUpdated(): string {
    const value = this.lastSession()?.createdAtUtc;
    if (!value) return 'cached results';
    return new Date(value).toLocaleString();
  }

  getProviderChip(provider: ScrapeProvider): ProviderSummary {
    return this.providers.find((item) => item.key === provider) ?? this.providers[0];
  }

  getBubbleGradient(company: string): string {
    const colors = [
      'linear-gradient(135deg, #465FFF 0%, #252dae 100%)',
      'linear-gradient(135deg, #00B0FF 0%, #0091EA 100%)',
      'linear-gradient(135deg, #00E676 0%, #00A250 100%)',
      'linear-gradient(135deg, #FF9100 0%, #FF6D00 100%)',
      'linear-gradient(135deg, #651FFF 0%, #4615B2 100%)',
      'linear-gradient(135deg, #D500F9 0%, #9C00AF 100%)',
    ];
    let sum = 0;
    for (let i = 0; i < company.length; i++) sum += company.charCodeAt(i);
    return colors[sum % colors.length];
  }

  getInitials(value: string): string {
    const parts = value.split(' ').filter(Boolean);
    if (parts.length === 0) return 'NS';
    if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
    return `${parts[0][0]}${parts[1][0]}`.toUpperCase();
  }

  private patchOfferState(id: string, payload: { isSaved?: boolean; isShortlisted?: boolean; isArchived?: boolean }): void {
    this.actionOfferId.set(id);
    this.sourcedOffersApi.updateSourcedOffer(id, payload).subscribe({
      next: (updated) => {
        this.offers.update((current) => {
          const next = current.map((offer) => offer.id === id ? { ...offer, ...updated } : offer);
          // In a Saved/Shortlisted/Archived tab, an offer that no longer matches it leaves the list.
          const tab = this.workflowTab();
          if (tab === 'all') return next.filter((offer) => offer.id !== id || !offer.isArchived);
          return next.filter((offer) => offer.id !== id || this.matchesTab(offer, tab));
        });
        this.actionOfferId.set(null);
        this.toast.success(this.stateToast(payload));
      },
      error: (err) => {
        this.actionOfferId.set(null);
        this.errorMessage.set(extractApiError(err).message || 'Unable to update sourced offer.');
      },
    });
  }

  private matchesTab(offer: SourcedOfferListItemDto, tab: 'saved' | 'shortlisted' | 'archived'): boolean {
    switch (tab) {
      case 'saved': return offer.isSaved && !offer.isArchived;
      case 'shortlisted': return offer.isShortlisted && !offer.isArchived;
      case 'archived': return offer.isArchived;
    }
  }

  private stateToast(payload: { isSaved?: boolean; isShortlisted?: boolean; isArchived?: boolean }): string {
    if (payload.isArchived !== undefined) return payload.isArchived ? 'Offer archived.' : 'Offer restored.';
    if (payload.isShortlisted !== undefined) return payload.isShortlisted ? 'Added to shortlist.' : 'Removed from shortlist.';
    return payload.isSaved ? 'Offer saved.' : 'Offer unsaved.';
  }
}
