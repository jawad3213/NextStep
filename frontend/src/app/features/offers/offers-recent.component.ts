import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { trigger, transition, style, animate, query, stagger } from '@angular/animations';

import {
  GlassdoorScrapedJob,
  IndeedScrapedJob,
  LinkedInScrapedJob,
  OfferApiService,
  ScrapeProvider,
} from './services/offer-api.service';

type ProviderSummary = {
  key: ScrapeProvider;
  label: string;
  tone: string;
  available: boolean;
  helper: string;
};

type ScrapedOfferCard = {
  id: string;
  provider: ScrapeProvider;
  providerLabel: string;
  title: string;
  company: string;
  location: string;
  postedAtText: string;
  tags: string[];
  description: string;
  employmentType: string;
  seniorityLevel: string;
  url: string;
  matchingScore?: number;
};

@Component({
  selector: 'app-offers-recent',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './offers-recent.component.html',
  styleUrl: './offers.component.scss',
  animations: [
    trigger('cardAnimation', [
      transition('* => *', [
        query(':enter', [
          style({ opacity: 0, transform: 'translateY(12px)' }),
          stagger(60, [
            animate('300ms ease-out', style({ opacity: 1, transform: 'translateY(0)' }))
          ])
        ], { optional: true })
      ])
    ])
  ]
})
export class OffersRecentComponent implements OnInit {
  private readonly offerApi = inject(OfferApiService);

  readonly providers: ProviderSummary[] = [
    {
      key: 'linkedin',
      label: 'LinkedIn',
      tone: 'bg-sky-50 text-sky-700 border-sky-200',
      available: true,
      helper: 'Live Scrapling agent',
    },
    {
      key: 'indeed',
      label: 'Indeed',
      tone: 'bg-violet-50 text-violet-700 border-violet-200',
      available: true,
      helper: 'Live Scrapling agent',
    },
    {
      key: 'glassdoor',
      label: 'Glassdoor',
      tone: 'bg-emerald-50 text-emerald-700 border-emerald-200',
      available: true,
      helper: 'Live Scrapling agent',
    },
  ];

  readonly selectedProvider = signal<ScrapeProvider>('linkedin');
  readonly keywords = signal('software engineer');
  readonly location = signal('Morocco');
  readonly postedSinceSeconds = signal(86400);
  readonly limit = signal(12);
  readonly indeedCountryCode = signal('ma');

  readonly searchTerm = signal('');
  readonly sortBy = signal<'recent' | 'company' | 'title'>('recent');
  readonly viewMode = signal<'list' | 'grid'>('grid');

  readonly isLoading = signal(false);
  readonly lastUpdated = signal<Date | null>(null);
  readonly errorMessage = signal('');
  readonly providerNotice = signal('');
  readonly offers = signal<ScrapedOfferCard[]>([]);

  readonly filteredOffers = computed(() => {
    const search = this.searchTerm().trim().toLowerCase();
    const sort = this.sortBy();
    const result = this.offers().filter((offer) => {
      if (!search) return true;
      return (
        offer.title.toLowerCase().includes(search) ||
        offer.company.toLowerCase().includes(search) ||
        offer.location.toLowerCase().includes(search) ||
        offer.tags.some((tag) => tag.toLowerCase().includes(search))
      );
    });

    result.sort((a, b) => {
      switch (sort) {
        case 'company':
          return a.company.localeCompare(b.company);
        case 'title':
          return a.title.localeCompare(b.title);
        case 'recent':
        default:
          return (this.extractHours(a.postedAtText) ?? Number.MAX_SAFE_INTEGER)
            - (this.extractHours(b.postedAtText) ?? Number.MAX_SAFE_INTEGER);
      }
    });
    return result;
  });

  readonly providerCount = computed(() => {
    const counts = new Map<ScrapeProvider, number>([
      ['linkedin', 0],
      ['indeed', 0],
      ['glassdoor', 0],
    ]);
    for (const offer of this.offers()) {
      counts.set(offer.provider, (counts.get(offer.provider) ?? 0) + 1);
    }
    return counts;
  });

  ngOnInit(): void {
    this.scrapeOffers();
  }

  selectProvider(provider: ProviderSummary): void {
    this.selectedProvider.set(provider.key);
    if (!provider.available) {
      this.providerNotice.set(`${provider.label} is not connected yet. Add its backend scraping endpoint, then this page is ready to consume it.`);
      this.offers.set([]);
      this.errorMessage.set('');
      return;
    }
    this.providerNotice.set('');
    this.scrapeOffers();
  }

  scrapeOffers(): void {
    if (!['linkedin', 'indeed', 'glassdoor'].includes(this.selectedProvider())) {
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set('');
    this.providerNotice.set('');

    if (this.selectedProvider() === 'linkedin') {
      this.offerApi.searchLinkedInJobs({
        keywords: this.keywords(),
        location: this.location(),
        limit: this.limit(),
        posted_since_seconds: this.postedSinceSeconds(),
        fetch_details: true,
        it_only: true,
      }).subscribe({
        next: (response) => {
          this.offers.set((response.jobs ?? []).map((job) => this.mapLinkedInJob(job)));
          this.lastUpdated.set(new Date());
          if (response.errors?.length) {
            this.providerNotice.set(response.errors.join(' | '));
          }
          this.isLoading.set(false);
        },
        error: (err) => {
          this.errorMessage.set(err?.error?.detail || 'Unable to fetch scraped offers right now.');
          this.offers.set([]);
          this.isLoading.set(false);
        }
      });
      return;
    }

    if (this.selectedProvider() === 'indeed') {
      this.offerApi.searchIndeedJobs({
        keywords: this.keywords(),
        location: this.location(),
        limit: this.limit(),
        fetch_details: true,
        it_only: true,
        country_code: this.indeedCountryCode(),
      }).subscribe({
        next: (response) => {
          this.offers.set((response.jobs ?? []).map((job) => this.mapIndeedJob(job)));
          this.lastUpdated.set(new Date());
          if (response.errors?.length) {
            this.providerNotice.set(response.errors.join(' | '));
          }
          this.isLoading.set(false);
        },
        error: (err) => {
          this.errorMessage.set(err?.error?.detail || 'Unable to fetch scraped offers right now.');
          this.offers.set([]);
          this.isLoading.set(false);
        }
      });
      return;
    }

    this.offerApi.searchGlassdoorJobs({
      keywords: this.keywords(),
      location: this.location(),
      limit: this.limit(),
      fetch_details: false,
      it_only: true,
    }).subscribe({
      next: (response) => {
        this.offers.set((response.jobs ?? []).map((job) => this.mapGlassdoorJob(job)));
        this.lastUpdated.set(new Date());
        if (response.errors?.length) {
          this.providerNotice.set(response.errors.join(' | '));
        }
        this.isLoading.set(false);
      },
      error: (err) => {
        this.errorMessage.set(err?.error?.detail || 'Unable to fetch scraped offers right now.');
        this.offers.set([]);
        this.isLoading.set(false);
      }
    });
  }

  onSearchInput(value: string): void {
    this.searchTerm.set(value);
  }

  onSortChange(value: string): void {
    this.sortBy.set(value as 'recent' | 'company' | 'title');
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

  formatLastUpdated(): string {
    const value = this.lastUpdated();
    if (!value) return 'No sync yet';
    return value.toLocaleString();
  }

  private mapLinkedInJob(job: LinkedInScrapedJob): ScrapedOfferCard {
    return {
      id: job.job_id || job.url || `${job.title}-${job.company}`,
      provider: 'linkedin',
      providerLabel: 'LinkedIn',
      title: job.title,
      company: job.company || 'Company',
      location: job.location || 'Location not specified',
      postedAtText: job.posted_at_text || 'Unknown',
      tags: (job.matched_it_terms || []).slice(0, 5),
      description: job.description || 'No description returned by the scraper.',
      employmentType: job.employment_type || 'Not specified',
      seniorityLevel: job.seniority_level || 'Not specified',
      url: job.url || '#',
      matchingScore: job.is_it_offer ? 100 : undefined,
    };
  }

  private mapIndeedJob(job: IndeedScrapedJob): ScrapedOfferCard {
    return {
      id: job.job_id || job.url || `${job.title}-${job.company}`,
      provider: 'indeed',
      providerLabel: 'Indeed',
      title: job.title,
      company: job.company || 'Company',
      location: job.location || 'Location not specified',
      postedAtText: job.posted_at_text || 'Unknown',
      tags: (job.matched_it_terms || []).slice(0, 5),
      description: job.description || 'No description returned by the scraper.',
      employmentType: job.employment_type || 'Not specified',
      seniorityLevel: job.seniority_level || 'Not specified',
      url: job.url || '#',
      matchingScore: job.is_it_offer ? 100 : undefined,
    };
  }

  private mapGlassdoorJob(job: GlassdoorScrapedJob): ScrapedOfferCard {
    return {
      id: job.job_id || job.url || `${job.title}-${job.company}`,
      provider: 'glassdoor',
      providerLabel: 'Glassdoor',
      title: job.title,
      company: job.company || 'Company',
      location: job.location || 'Location not specified',
      postedAtText: job.posted_at_text || 'Unknown',
      tags: (job.matched_it_terms || []).slice(0, 5),
      description: job.description || 'No description returned by the scraper.',
      employmentType: job.employment_type || 'Not specified',
      seniorityLevel: job.seniority_level || 'Not specified',
      url: job.url || '#',
      matchingScore: job.is_it_offer ? 100 : undefined,
    };
  }

  private extractHours(value: string): number | null {
    const lower = (value || '').toLowerCase();
    const hourMatch = lower.match(/(\d+)\s*hour/);
    if (hourMatch) return Number(hourMatch[1]);
    const dayMatch = lower.match(/(\d+)\s*day/);
    if (dayMatch) return Number(dayMatch[1]) * 24;
    return null;
  }
}
