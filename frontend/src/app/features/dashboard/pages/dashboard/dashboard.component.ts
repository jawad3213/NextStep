import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { AuthService } from '@core/auth/auth.service';
import { ProfileService } from '@features/profile/data-access/profile.service';
import { CandidatureDto } from '@features/applications/data-access/candidature.models';
import { CandidatureService } from '@features/applications/data-access/candidature.service';
import { OfferApiService } from '@features/offers/data-access/offer-api.service';
import { OfferHistoryItem } from '@features/offers/data-access/offers.models';
import { SourcedOffersApiService } from '@features/offers/data-access/sourced-offers-api.service';
import { SourcedOfferListItemDto } from '@features/offers/data-access/sourced-offers.models';
import {
  DashboardApplication,
  TodoKind,
  buildKpis,
  buildPipeline,
  buildSummaryLine,
  buildTodos,
  companyInitials,
  toDashboardApplications,
} from './dashboard-summary';

interface Shortcut {
  icon: string;
  label: string;
  route: string;
}

const RECENT_APPLICATIONS = 5;
const TODOS_SHOWN = 5;
/** One row of cards on desktop. */
const SOURCED_OFFERS_SHOWN = 3;

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent {
  private readonly authService = inject(AuthService);
  private readonly profileService = inject(ProfileService);
  private readonly candidatureService = inject(CandidatureService);
  private readonly offerApi = inject(OfferApiService);
  private readonly sourcedOffersApi = inject(SourcedOffersApiService);

  readonly loading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  /** Sources that failed on the last load (message + retry). */
  readonly failedSections = signal<string[]>([]);

  readonly currentDateLabel = signal(this.formatHeaderDate(new Date()));
  readonly applications = signal<DashboardApplication[]>([]);
  readonly sourcedOffers = signal<SourcedOfferListItemDto[]>([]);

  readonly userName = computed(() => {
    const profileName = this.profileService.profile()?.personal?.firstName?.trim();
    if (profileName) return profileName;
    const user = this.authService.user();
    return user?.firstName?.trim() || user?.email || '';
  });

  readonly kpis = computed(() => buildKpis(this.applications()));
  readonly pipeline = computed(() => buildPipeline(this.applications()));
  readonly todos = computed(() => buildTodos(this.applications()));
  readonly visibleTodos = computed(() => this.todos().slice(0, TODOS_SHOWN));
  readonly hiddenTodoCount = computed(() => Math.max(0, this.todos().length - TODOS_SHOWN));
  readonly recentApplications = computed(() => this.applications().slice(0, RECENT_APPLICATIONS));
  readonly summaryLine = computed(() => buildSummaryLine(this.applications(), this.todos().length));

  readonly shortcuts: Shortcut[] = [
    { icon: 'description', label: 'Generate a CV', route: '/cv' },
    { icon: 'travel_explore', label: 'Search jobs', route: '/offers-recent' },
    { icon: 'mail', label: 'Write an email', route: '/letters' },
    { icon: 'insights', label: 'Analyze a company', route: '/company-intel' },
  ];

  readonly todoStyle: Record<TodoKind, { icon: string; classes: string }> = {
    interview: { icon: 'event', classes: 'bg-purple-50 text-purple-600' },
    reply: { icon: 'mark_email_unread', classes: 'bg-green-50 text-green-600' },
    'follow-up': { icon: 'forward_to_inbox', classes: 'bg-orange-50 text-orange-600' },
    draft: { icon: 'edit_note', classes: 'bg-gray-100 text-gray-500' },
  };

  readonly companyInitials = companyInitials;

  constructor() {
    this.loadDashboard();
  }

  refresh(): void {
    this.loadDashboard();
  }

  private loadDashboard(): void {
    this.loading.set(true);
    this.errorMessage.set(null);
    this.failedSections.set([]);
    this.currentDateLabel.set(this.formatHeaderDate(new Date()));

    const failures: string[] = [];
    const markFailed = (section: string) => failures.push(section);

    forkJoin({
      candidatures: this.candidatureService.getMyCandidatures().pipe(catchError(() => { markFailed('candidatures'); return of([] as CandidatureDto[]); })),
      history: this.offerApi.getOffersHistory().pipe(catchError(() => { markFailed('offres analysées'); return of([] as OfferHistoryItem[]); })),
      sourced: this.sourcedOffersApi
        .getSourcedOffers({ limit: SOURCED_OFFERS_SHOWN, postedWindow: '24h', location: 'Casablanca' })
        .pipe(catchError(() => { markFailed('recent offers'); return of([] as SourcedOfferListItemDto[]); })),
    }).subscribe({
      next: ({ candidatures, history, sourced }) => {
        this.failedSections.set(failures);
        this.applications.set(toDashboardApplications(candidatures, history));
        this.sourcedOffers.set(sourced.slice(0, SOURCED_OFFERS_SHOWN));
        this.loading.set(false);
      },
      error: () => {
        this.errorMessage.set('Unable to load dashboard. Please retry in a moment.');
        this.failedSections.set(failures);
        this.loading.set(false);
      },
    });
  }

  formatShortDate(dateLike?: string): string {
    if (!dateLike) return '';
    const d = new Date(dateLike);
    if (Number.isNaN(d.getTime())) return '';
    return new Intl.DateTimeFormat('en-US', { day: 'numeric', month: 'short' }).format(d);
  }

  private formatHeaderDate(date: Date): string {
    return new Intl.DateTimeFormat('en-US', {
      weekday: 'long',
      day: 'numeric',
      month: 'long',
      year: 'numeric',
    }).format(date);
  }
}
