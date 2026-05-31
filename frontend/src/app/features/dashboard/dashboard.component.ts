import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { AuthService } from '../../core/auth/services/auth.service';
import { CandidatureDto, CandidatureService } from '../../services/candidature.service';
import { OfferDto, OfferHistoryItemDto, OfferService } from '../../services/offer.service';
import { OfferApiService, SourcedOfferListItemDto } from '../offers/services/offer-api.service';
import { OnboardingService } from '../../services/onboarding.service';
import { ProfileService } from '../profile/profile.service';

interface Application {
  candidatureId: string;
  company: string;
  logo: string;
  poste: string;
  date: string;
  status: string;
  statusColor: 'success' | 'warning' | 'primary' | 'slate';
}

interface Activity {
  icon: string;
  iconBg: string;
  iconColor: string;
  text: string;
  time: string;
}

interface Interview {
  candidatureId: string;
  dayLabel: string;
  date: string;
  title: string;
  time: string;
  location: string;
  isToday: boolean;
}

interface MissingSection {
  icon: string;
  label: string;
  description: string;
  route: string;
  queryParams: Record<string, string>;
  color: 'brand' | 'amber' | 'purple' | 'green';
}

interface QuickAction {
  icon: string;
  label: string;
  description: string;
  route: string;
  color: 'brand' | 'amber' | 'purple' | 'green';
}

interface OfferOverview {
  company: string;
  poste: string;
  matchScore: number;
  atsScore: number;
  date: string;
  skills: string[];
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent {
  private readonly authService = inject(AuthService);
  private readonly onboardingService = inject(OnboardingService);
  private readonly candidatureService = inject(CandidatureService);
  private readonly offerService = inject(OfferService);
  private readonly offerApi = inject(OfferApiService);
  private readonly profileService = inject(ProfileService);

  readonly loading = signal(false);
  readonly refreshing = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly currentDateLabel = signal(this.formatHeaderDate(new Date()));
  readonly profileCompletion = this.profileService.completionPercentage;
  readonly sourcedOffers = signal<SourcedOfferListItemDto[]>([]);

  readonly userName = computed(() => {
    const profile = this.profileService.profile();
    const profileName = `${profile?.personal?.firstName ?? ''} ${profile?.personal?.lastName ?? ''}`.trim();
    if (profileName) return profileName;

    const user = this.authService.user();
    const full = `${user?.firstName ?? ''} ${user?.lastName ?? ''}`.trim();
    return full || user?.email || 'there';
  });

  private readonly candidaturesData = signal<CandidatureDto[]>([]);
  private readonly offerHistoryData = signal<OfferHistoryItemDto[]>([]);
  private readonly offerMapSignal = signal<Map<string, OfferHistoryItemDto>>(new Map());

  readonly recentApplications = signal<Application[]>([]);
  readonly activities = signal<Activity[]>([]);
  readonly interviews = signal<Interview[]>([]);
  readonly loadingMoreInterviews = signal(false);
  readonly hasMoreInterviews = signal(false);
  private interviewOffset = 0;
  private readonly interviewPageSize = 10;
  readonly applications = signal<Application[]>([]);
  readonly offers = signal<OfferOverview[]>([]);

  readonly missingSections: MissingSection[] = [
    { icon: 'work_history', label: 'Expériences', description: 'Ajoutez votre parcours professionnel', route: '/profile', queryParams: { step: 'experience' }, color: 'brand' },
    { icon: 'school', label: 'Formations', description: 'Complétez votre niveau d\'études', route: '/profile', queryParams: { step: 'formation' }, color: 'amber' },
    { icon: 'psychology', label: 'Compétences', description: 'Listez vos savoir-faire techniques', route: '/profile', queryParams: { step: 'competences' }, color: 'purple' },
    { icon: 'verified', label: 'Certifications', description: 'Ajoutez vos certifications', route: '/profile', queryParams: { step: 'certifications' }, color: 'green' },
  ];

  readonly quickActions: QuickAction[] = [
    { icon: 'description', label: 'Générer un CV', description: 'Créez un CV personnalisé', route: '/cv', color: 'brand' },
    { icon: 'travel_explore', label: 'Scraper des Offres', description: 'Trouvez de nouvelles offres', route: '/offers-recent', color: 'purple' },
    { icon: 'mail', label: 'Rédiger Email', description: 'Préparez vos candidatures', route: '/letters', color: 'amber' },
    { icon: 'insights', label: 'Analyse Entreprise', description: 'Étudiez vos employeurs', route: '/company-intel', color: 'green' },
  ];

  readonly totalApplications = computed(() => this.candidaturesData().length);
  readonly totalAccepted = computed(() => this.candidaturesData().filter(c => this.isAccepted(c)).length);
  readonly totalPending = computed(() => this.candidaturesData().filter(c => this.isPending(c)).length);
  readonly totalSent = computed(() =>
    this.candidaturesData().filter(c => !this.isAccepted(c) && !this.isPending(c)).length
  );
  readonly totalCvs = computed(() =>
    this.offerHistoryData().filter(o => Number(o.currentStep ?? 0) >= 4).length
  );

  readonly successRate = computed(() => {
    const total = this.totalApplications();
    if (total === 0) return 0;
    return Math.round((this.totalAccepted() / total) * 100);
  });
  readonly successTrend = computed(() => `${this.totalAccepted()} valides`);
  readonly avgTime = computed(() => {
    const withResponses = this.candidaturesData().filter(c => !!c.lastResponseAtUtc);
    if (!withResponses.length) return '--';

    const days = withResponses
      .map(c => {
        const start = new Date(c.dateCreation).getTime();
        const end = c.lastResponseAtUtc ? new Date(c.lastResponseAtUtc).getTime() : start;
        return Math.max(0, (end - start) / (1000 * 60 * 60 * 24));
      })
      .filter(v => Number.isFinite(v));

    if (!days.length) return '--';
    const avg = Math.round(days.reduce((a, b) => a + b, 0) / days.length);
    return `${avg} j.`;
  });

  readonly sentPct = computed(() => this.toPercent(this.totalSent(), this.totalApplications()));
  readonly pendingPct = computed(() => this.toPercent(this.totalPending(), this.totalApplications()));
  readonly acceptedPct = computed(() => this.toPercent(this.totalAccepted(), this.totalApplications()));

  readonly sentOffset = computed(() => 251.2 * (1 - this.sentPct() / 100));
  readonly pendingOffset = computed(() => 251.2 * (1 - this.pendingPct() / 100));
  readonly acceptedOffset = computed(() => 251.2 * (1 - this.acceptedPct() / 100));

  readonly sentAngle = 0;
  readonly pendingAngle = computed(() => (this.sentPct() / 100) * 360);
  readonly acceptedAngle = computed(() => ((this.sentPct() + this.pendingPct()) / 100) * 360);

  readonly responsesCount = computed(() => this.candidaturesData().filter(c => !!c.hasResponse).length);
  readonly noResponseCount = computed(() => Math.max(0, this.totalApplications() - this.responsesCount()));
  readonly responsesPct = computed(() => this.toPercent(this.responsesCount(), this.totalApplications()));

  constructor() {
    this.loadDashboard();
  }

  get statusClassMap(): Record<string, string> {
    return {
      success: 'bg-green-50 text-green-700',
      warning: 'bg-amber-50 text-amber-700',
      primary: 'bg-brand-50 text-brand-600',
      slate: 'bg-slate-100 text-slate-500',
    };
  }

  get missingColorMap(): Record<string, string> {
    return {
      brand: 'border-brand-200 bg-brand-50 hover:bg-brand-100 text-brand-700',
      amber: 'border-amber-200 bg-amber-50 hover:bg-amber-100 text-amber-700',
      purple: 'border-purple-200 bg-purple-50 hover:bg-purple-100 text-purple-700',
      green: 'border-green-200 bg-green-50 hover:bg-green-100 text-green-700',
    };
  }

  get missingIconColorMap(): Record<string, string> {
    return {
      brand: 'text-brand-500',
      amber: 'text-amber-500',
      purple: 'text-purple-500',
      green: 'text-green-600',
    };
  }

  getScoreColor(score: number): string {
    if (score >= 70) return 'text-green-600';
    if (score >= 50) return 'text-amber-500';
    return 'text-red-500';
  }

  getScoreBg(score: number): string {
    if (score >= 70) return 'bg-green-50';
    if (score >= 50) return 'bg-amber-50';
    return 'bg-red-50';
  }

  refresh(): void {
    this.loadDashboard(true);
  }

  private loadDashboard(manualRefresh = false): void {
    this.loading.set(true);
    this.errorMessage.set(null);
    if (manualRefresh) this.refreshing.set(true);
    this.currentDateLabel.set(this.formatHeaderDate(new Date()));

    forkJoin({
      onboarding: this.onboardingService.getStatus().pipe(catchError(() => of({ onboardingCompleted: true, profileScore: 0 }))),
      candidatures: this.candidatureService.getMyCandidatures().pipe(catchError(() => of([] as CandidatureDto[]))),
      history: this.offerService.getMyOfferHistory().pipe(catchError(() => of([] as OfferHistoryItemDto[]))),
      scrapedOffers: this.offerApi.getSourcedOffers({ limit: 6, postedWindow: '24h', location: 'Casablanca' }).pipe(catchError(() => of([] as SourcedOfferListItemDto[]))),
    }).subscribe({
      next: ({ onboarding, candidatures, history, scrapedOffers }) => {
        this.candidaturesData.set(candidatures);
        this.offerHistoryData.set(history);
        this.sourcedOffers.set(scrapedOffers);

        const offerMap = new Map(history.map(h => [h.offerId, h]));
        this.offerMapSignal.set(offerMap);
        const sortedCandidatures = [...candidatures].sort((a, b) =>
          this.toTimestamp(b.dateCreation) - this.toTimestamp(a.dateCreation)
        );

        const recent = sortedCandidatures.slice(0, 8).map(c => this.toApplication(c, offerMap));
        this.recentApplications.set(recent);
        this.applications.set(recent.slice(0, 3));
        this.interviews.set(this.buildInterviews(sortedCandidatures, offerMap));
        this.loadMoreInterviews(true);
        this.activities.set(this.buildActivities(sortedCandidatures, history, offerMap));

        const topOffers = [...history]
          .sort((a, b) => this.toTimestamp(b.dateCreation) - this.toTimestamp(a.dateCreation))
          .slice(0, 3);

        if (topOffers.length === 0) {
          this.offers.set([]);
          this.finishLoading();
          return;
        }

        forkJoin(
          topOffers.map(h =>
            this.offerService.getOfferById(h.offerId).pipe(catchError(() => of(null)))
          )
        ).subscribe({
          next: (details) => {
            const overview = topOffers.map((h, i) => this.toOfferOverview(h, details[i]));
            this.offers.set(overview);
            this.finishLoading();
          },
          error: () => {
            this.offers.set(topOffers.map(h => this.toOfferOverview(h, null)));
            this.finishLoading();
          }
        });
      },
      error: () => {
        this.errorMessage.set('Impossible de charger le dashboard depuis le backend.');
        this.finishLoading();
      }
    });
  }

  private finishLoading(): void {
    this.loading.set(false);
    this.refreshing.set(false);
  }

  private toOfferOverview(history: OfferHistoryItemDto, detail: OfferDto | null): OfferOverview {
    return {
      company: detail?.entreprise || history.entreprise || 'Entreprise',
      poste: detail?.titre || history.titre || 'Poste',
      matchScore: Number(detail?.scoreMatching ?? history.scoreMatching ?? 0),
      atsScore: Number(detail?.scoreAts ?? 0),
      date: this.formatShortDate(history.dateCreation),
      skills: (detail?.competencesRequises ?? []).slice(0, 3),
    };
  }

  private toApplication(candidature: CandidatureDto, offerMap: Map<string, OfferHistoryItemDto>): Application {
    const offer = offerMap.get(candidature.idOffre);
    const meta = this.getStatusMeta(candidature);
    return {
      candidatureId: candidature.idCandidature,
      company: offer?.entreprise || 'Entreprise',
      logo: 'business',
      poste: offer?.titre || 'Poste',
      date: this.formatLongDate(candidature.dateCreation),
      status: meta.label,
      statusColor: meta.color,
    };
  }

  private buildInterviews(
    candidatures: CandidatureDto[],
    offerMap: Map<string, OfferHistoryItemDto>
  ): Interview[] {
    return candidatures
      .filter(c => this.statusString(c).includes('ENTRETIEN'))
      .sort((a, b) => this.toTimestamp(b.lastResponseAtUtc ?? b.dateCreation) - this.toTimestamp(a.lastResponseAtUtc ?? a.dateCreation))
      .slice(0, 2)
      .map(c => {
        const baseDate = new Date(c.lastResponseAtUtc ?? c.dateCreation);
        const offer = offerMap.get(c.idOffre);
        return {
          candidatureId: c.idCandidature,
          dayLabel: new Intl.DateTimeFormat('fr-FR', { weekday: 'short' }).format(baseDate).replace('.', '').toUpperCase(),
          date: new Intl.DateTimeFormat('fr-FR', { day: '2-digit' }).format(baseDate),
          title: `Entretien - ${offer?.entreprise || 'Entreprise'}`,
          time: new Intl.DateTimeFormat('fr-FR', { hour: '2-digit', minute: '2-digit' }).format(baseDate),
          location: 'A confirmer',
          isToday: this.isSameDay(baseDate, new Date()),
        };
      });
  }

  loadMoreInterviews(reset = false): void {
    if (!reset && (this.loadingMoreInterviews() || !this.hasMoreInterviews())) return;
    if (reset) {
      this.interviewOffset = 0;
      this.interviews.set([]);
    }

    this.loadingMoreInterviews.set(true);
    this.candidatureService
      .getMyCandidaturesPaged(this.interviewOffset, this.interviewPageSize, true)
      .subscribe({
        next: (page) => {
          const offerMap = this.offerMapSignal();
          const mapped = page.items.map((c) => {
            const baseDate = new Date(c.lastResponseAtUtc ?? c.dateCreation);
            const offer = offerMap.get(c.idOffre);
            return {
              candidatureId: c.idCandidature,
              dayLabel: new Intl.DateTimeFormat('fr-FR', { weekday: 'short' }).format(baseDate).replace('.', '').toUpperCase(),
              date: new Intl.DateTimeFormat('fr-FR', { day: '2-digit' }).format(baseDate),
              title: `Entretien - ${offer?.entreprise || 'Entreprise'}`,
              time: new Intl.DateTimeFormat('fr-FR', { hour: '2-digit', minute: '2-digit' }).format(baseDate),
              location: 'A confirmer',
              isToday: this.isSameDay(baseDate, new Date()),
            } as Interview;
          });

          this.interviews.update((existing) => {
            const map = new Map(existing.map((i) => [i.candidatureId, i]));
            for (const m of mapped) map.set(m.candidatureId, m);
            return Array.from(map.values());
          });

          this.interviewOffset += mapped.length;
          this.hasMoreInterviews.set(page.hasMore);
          this.loadingMoreInterviews.set(false);
        },
        error: () => {
          this.loadingMoreInterviews.set(false);
          this.hasMoreInterviews.set(false);
        }
      });
  }

  private buildActivities(
    candidatures: CandidatureDto[],
    offers: OfferHistoryItemDto[],
    offerMap: Map<string, OfferHistoryItemDto>
  ): Activity[] {
    const activityFromCandidatures: Activity[] = candidatures.slice(0, 3).map(c => {
      const meta = this.getStatusMeta(c);
      const company = offerMap.get(c.idOffre)?.entreprise || 'Entreprise';
      return {
        icon: meta.color === 'success' ? 'check_circle' : meta.color === 'warning' ? 'schedule' : 'send',
        iconBg: meta.color === 'success' ? 'bg-green-50' : meta.color === 'warning' ? 'bg-amber-50' : 'bg-brand-50',
        iconColor: meta.color === 'success' ? 'text-green-600' : meta.color === 'warning' ? 'text-amber-600' : 'text-brand-500',
        text: `Candidature ${meta.label.toLowerCase()} - ${company}`,
        time: this.timeAgo(c.dateCreation),
      };
    });

    const activityFromOffers: Activity[] = offers.slice(0, 2).map(o => ({
      icon: 'work',
      iconBg: 'bg-purple-50',
      iconColor: 'text-purple-600',
      text: `Offre analysee - ${o.titre || 'Offre'}`,
      time: this.timeAgo(o.dateCreation),
    }));

    return [...activityFromCandidatures, ...activityFromOffers].slice(0, 5);
  }

  private getStatusMeta(candidature: CandidatureDto): { label: string; color: 'success' | 'warning' | 'primary' | 'slate' } {
    const normalized = this.statusString(candidature);
    if (normalized.includes('ACCEPTE')) return { label: 'Acceptee', color: 'success' };
    if (normalized.includes('ENTRETIEN')) return { label: 'Entretien', color: 'success' };
    if (normalized.includes('REFUSE')) return { label: 'Refusee', color: 'slate' };
    if (normalized.includes('ATTENTE') || normalized.includes('RELANCE')) return { label: 'En attente', color: 'warning' };
    return { label: 'Envoyee', color: 'primary' };
  }

  private statusString(candidature: CandidatureDto): string {
    return `${candidature.responseStatus ?? ''} ${candidature.statut ?? ''}`.toUpperCase();
  }

  private isAccepted(candidature: CandidatureDto): boolean {
    return this.statusString(candidature).includes('ACCEPTE');
  }

  private isPending(candidature: CandidatureDto): boolean {
    const status = this.statusString(candidature);
    return (
      !candidature.hasResponse ||
      status.includes('ATTENTE') ||
      status.includes('RELANCE') ||
      status.includes('REPONSE_GENERALE') ||
      status.includes('REPONSE_AUTOMATIQUE')
    );
  }

  private toPercent(value: number, total: number): number {
    if (total <= 0) return 0;
    return Math.round((value / total) * 100);
  }

  private toTimestamp(dateLike?: string): number {
    if (!dateLike) return 0;
    const ts = new Date(dateLike).getTime();
    return Number.isFinite(ts) ? ts : 0;
  }

  private formatHeaderDate(date: Date): string {
    return new Intl.DateTimeFormat('fr-FR', {
      weekday: 'long',
      day: '2-digit',
      month: 'long',
      year: 'numeric',
    }).format(date).toUpperCase();
  }

  private formatLongDate(dateLike?: string): string {
    if (!dateLike) return '--';
    const d = new Date(dateLike);
    if (Number.isNaN(d.getTime())) return '--';
    return new Intl.DateTimeFormat('fr-FR', {
      day: '2-digit',
      month: 'long',
      year: 'numeric',
    }).format(d);
  }

  private formatShortDate(dateLike?: string): string {
    if (!dateLike) return '--';
    const d = new Date(dateLike);
    if (Number.isNaN(d.getTime())) return '--';
    return new Intl.DateTimeFormat('fr-FR', {
      day: '2-digit',
      month: 'short',
    }).format(d);
  }

  private timeAgo(dateLike?: string): string {
    if (!dateLike) return '--';
    const now = Date.now();
    const ts = this.toTimestamp(dateLike);
    if (ts <= 0) return '--';
    const diffMs = now - ts;
    const diffHours = Math.max(0, Math.floor(diffMs / (1000 * 60 * 60)));
    if (diffHours < 1) return 'Il y a quelques minutes';
    if (diffHours < 24) return `Il y a ${diffHours}h`;
    const diffDays = Math.floor(diffHours / 24);
    return `Il y a ${diffDays}j`;
  }

  private isSameDay(a: Date, b: Date): boolean {
    return a.getFullYear() === b.getFullYear()
      && a.getMonth() === b.getMonth()
      && a.getDate() === b.getDate();
  }
}
