import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import {
  NormalizedContractType,
  OfferApiService,
  PostedWindow,
  ScrapeProvider,
  ScrapeSessionDto,
  SourcedOfferListItemDto,
} from './services/offer-api.service';

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
  styleUrl: './offers.component.scss',
})
export class OffersRecentComponent implements OnInit {
  private readonly offerApi = inject(OfferApiService);
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
  readonly limit = signal(24);

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

  ngOnInit(): void {
    this.loadCachedOffers();
  }

  loadCachedOffers(): void {
    this.isLoading.set(true);
    this.errorMessage.set('');

    this.offerApi.getSourcedOffers({
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
      },
      error: (err) => {
        this.errorMessage.set(err?.error?.error || err?.error?.detail || 'Unable to load sourced offers.');
        this.offers.set([]);
        this.isLoading.set(false);
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
    this.isLoading.set(true);
    this.errorMessage.set('');
    this.warnings.set([]);

    this.offerApi.searchSourcedOffers({
      keywords: this.keywords(),
      location: this.location(),
      providers: this.selectedProviders(),
      limit: this.limit(),
      postedWindow: this.selectedPostedWindow(),
      contractTypes: this.selectedContractTypes(),
      indeedCountryCode: this.indeedCountryCode(),
    }).subscribe({
      next: (response) => {
        this.offers.set(response.offers ?? []);
        this.lastSession.set(response.session ?? null);
        this.warnings.set(response.warnings ?? []);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.errorMessage.set(err?.error?.error || err?.error?.detail || 'Unable to refresh sourced offers.');
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

    this.offerApi.promoteSourcedOffer(offer.id).subscribe({
      next: (promotion) => {
        this.actionOfferId.set(null);
        this.router.navigate(['/offers/analyze'], {
          queryParams: {
            offerId: promotion.offerId,
            autoAnalyze: 1,
          },
        });
      },
      error: (err) => {
        this.actionOfferId.set(null);
        this.errorMessage.set(err?.error?.error || err?.error?.detail || 'Unable to promote sourced offer.');
      },
    });
  }

  formatLastUpdated(): string {
    const value = this.lastSession()?.createdAtUtc;
    if (!value) return 'Cached results';
    return new Date(value).toLocaleString();
  }

  getProviderChip(provider: ScrapeProvider): ProviderSummary {
    return this.providers.find((item) => item.key === provider) ?? this.providers[0];
  }

  getProviderCount(provider: ScrapeProvider): number {
    return this.offers().filter((offer) => offer.provider === provider).length;
  }

  getRankingTags(offer: SourcedOfferListItemDto): string[] {
    return offer.aiMatchedSkills?.length ? offer.aiMatchedSkills : offer.matchedItTerms;
  }

  getAiReasons(offer: SourcedOfferListItemDto): string[] {
    return offer.aiReasons ?? [];
  }

  shouldShowEmploymentType(offer: SourcedOfferListItemDto): boolean {
    const employment = (offer.employmentType ?? '').trim().toLowerCase();
    if (!employment || employment === 'not applicable') return false;
    const contract = (offer.normalizedContractType ?? '').trim().toLowerCase();
    if (contract && contract !== 'other' && ['full-time', 'full time', 'part-time', 'part time'].includes(employment)) {
      return false;
    }
    return true;
  }

  shouldShowSeniority(offer: SourcedOfferListItemDto): boolean {
    const seniority = (offer.seniorityLevel ?? '').trim().toLowerCase();
    return !!seniority && seniority !== 'not applicable';
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
    this.offerApi.updateSourcedOffer(id, payload).subscribe({
      next: (updated) => {
        this.offers.update((current) => current.map((offer) => offer.id === id ? { ...offer, ...updated } : offer));
        this.actionOfferId.set(null);
      },
      error: (err) => {
        this.actionOfferId.set(null);
        this.errorMessage.set(err?.error?.error || err?.error?.detail || 'Unable to update sourced offer.');
      },
    });
  }
}
