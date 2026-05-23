import { Component, signal, computed, OnInit, inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { toSignal } from '@angular/core/rxjs-interop';
import { combineLatest, map } from 'rxjs';
import { ArenaService } from '../../services/arena.service';
import { AuthService } from '../../../../core/auth/services/auth.service';
import { SidebarService } from '../../../../shared/services/sidebar.service';
import {
  ArenaConfig, DOMAINS, LEVELS, DURATIONS, LANGUAGES,
  FOCUS_BY_DOMAIN, InterviewLevel, SessionSummary, SessionDetail
} from '../../models/arena.models';

type View = 'selector' | 'arena';
type ArenaStep = 'domain' | 'level' | 'duration' | 'language' | 'focus' | 'ready';

const STEPS: { key: ArenaStep; label: string }[] = [
  { key: 'domain', label: 'Domain' },
  { key: 'level', label: 'Level' },
  { key: 'duration', label: 'Duration' },
  { key: 'language', label: 'Language' },
  { key: 'focus', label: 'Focus Areas' },
  { key: 'ready', label: 'Ready' },
];

@Component({
  selector: 'app-mode-selector',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './mode-selector.component.html',
  styleUrls: ['./mode-selector.component.scss'],
})
export class ModeSelectorComponent implements OnInit {
  private http = inject(HttpClient);
  private router = inject(Router);
  private arenaService = inject(ArenaService);
  private authService = inject(AuthService);
  private sidebarService = inject(SidebarService);
  private platformId = inject(PLATFORM_ID);

  // Reactive left offset for the modal overlay:
  // When the sidebar is visible at xl, offset the fixed overlay
  // so flexbox centers the modal in the VISIBLE content area, not behind the sidebar.
  readonly modalOverlayLeft = toSignal(
    combineLatest([
      this.sidebarService.isExpanded$,
      this.sidebarService.isHovered$,
    ]).pipe(
      map(([expanded, hovered]) => {
        if (!isPlatformBrowser(this.platformId) || window.innerWidth < 1280) return '0px';
        return (expanded || hovered) ? '290px' : '90px';
      })
    ),
    { initialValue: '0px' }
  );

  // ── Views & steps
  view = signal<View>('selector');
  currentStep = signal<ArenaStep>('domain');
  isLaunching = signal(false);

  // ── Arena config
  selectedDomain = signal('');
  selectedLevel = signal<InterviewLevel>('junior');
  selectedDuration = signal(20);
  selectedLanguage = signal('en');
  selectedFocus = signal<string[]>([]);

  // ── Offer specifics
  selectedOfferId = signal<string | null>(null);
  selectedJobTitle = signal<string | null>(null);
  selectedCompany = signal<string | null>(null);

  // ── History Data
  pastSessions = signal<SessionSummary[]>([]);
  selectedSession = signal<SessionDetail | null>(null);
  showDetailModal = signal(false);
  loadingHistory = signal(false);
  showDeleteConfirm = signal(false);
  sessionToDelete = signal<SessionSummary | null>(null);

  // ── History Filter & Counts
  selectedHistoryFilter = signal<'all' | 'arena' | 'offer'>('all');
  historySearchQuery = signal<string>('');

  totalCount = computed(() => this.pastSessions().length);
  arenaCount = computed(() => this.pastSessions().filter(s => s.mode === 'arena').length);
  offerCount = computed(() => this.pastSessions().filter(s => s.mode === 'offer').length);

  filteredSessions = computed(() => {
    const filter = this.selectedHistoryFilter();
    const query = this.historySearchQuery().toLowerCase().trim();
    let sessions = this.pastSessions();

    if (filter !== 'all') {
      sessions = sessions.filter(s => s.mode === filter);
    }

    if (query) {
      sessions = sessions.filter(s => {
        const domainMatch = s.domain?.toLowerCase().includes(query);
        const jobMatch = s.jobTitle?.toLowerCase().includes(query);
        const companyMatch = s.company?.toLowerCase().includes(query);
        return domainMatch || jobMatch || companyMatch;
      });
    }

    return sessions;
  });

  // Notification Toast
  showToast = signal(false);
  toastMsg = signal('');

  // ── Data
  readonly domains = DOMAINS;
  readonly levels = LEVELS;
  readonly durations = DURATIONS;
  readonly languages = LANGUAGES;
  steps = computed(() => {
    if (this.selectedOfferId()) {
      return [
        { key: 'duration' as ArenaStep, label: 'Duration' },
        { key: 'language' as ArenaStep, label: 'Language' },
        { key: 'ready' as ArenaStep, label: 'Ready' },
      ];
    }
    return [
      { key: 'domain' as ArenaStep, label: 'Domain' },
      { key: 'level' as ArenaStep, label: 'Level' },
      { key: 'duration' as ArenaStep, label: 'Duration' },
      { key: 'language' as ArenaStep, label: 'Language' },
      { key: 'focus' as ArenaStep, label: 'Focus Areas' },
      { key: 'ready' as ArenaStep, label: 'Ready' },
    ];
  });

  focusPool = computed(() => FOCUS_BY_DOMAIN[this.selectedDomain()] ?? FOCUS_BY_DOMAIN['software']);
  stepIndex = computed(() => this.steps().findIndex(s => s.key === this.currentStep()));

  ngOnInit() {
    // Check if navigated from the Offers page with a pre-filled offer config
    const navState = history.state as { preselectedMode?: string; offerConfig?: ArenaConfig };
    if (navState?.preselectedMode === 'offer' && navState?.offerConfig) {
      const config = navState.offerConfig;
      this.selectedOfferId.set(config.offer_id ?? null);
      this.selectedJobTitle.set(config.job_title ?? null);
      this.selectedCompany.set(config.company ?? null);
      this.selectedDomain.set(config.domain || 'software');
      this.selectedLevel.set(config.level || 'senior');
      this.selectedDuration.set(config.duration_minutes || 20);
      this.selectedLanguage.set(config.language || 'en');
      this.selectedFocus.set(config.focus_areas || []);

      // Go to stepper configuration starting at the first dynamic step (duration) for offer mode!
      this.view.set('arena');
      this.currentStep.set('duration');
      return;
    }

    // Normal load — fetch session history
    setTimeout(() => {
      if (this.authService.isAuthenticated()) {
        this.loadHistory();
      } else {
        this.pastSessions.set([]);
      }
    }, 500);
  }

  private loadHistory() {
    // Récupère le token JWT depuis Keycloak
    const token = this.authService.getToken();
    if (!token) {
      this.pastSessions.set([]);
      return;
    }

    // Décode le token pour extraire le userId (claim "sub")
    const userId = this.extractUserIdFromToken(token);
    if (!userId) {
      this.pastSessions.set([]);
      return;
    }

    this.loadingHistory.set(true);

    this.arenaService.getSessions(userId).subscribe({
      next: (sessions) => {
        this.pastSessions.set(sessions);
        this.loadingHistory.set(false);
      },
      error: () => {
        this.pastSessions.set([]);
        this.loadingHistory.set(false);
      }
    });
  }

  private extractUserIdFromToken(token: string): string | null {
    try {
      // Décode le JWT: format = header.payload.signature
      const parts = token.split('.');
      if (parts.length !== 3) return null;

      const payload = JSON.parse(atob(parts[1]));

      // Le userId se trouve dans le claim "sub"
      return payload.sub || payload.userId || null;
    } catch {
      return null;
    }
  }

  viewDetails(session: SessionSummary) {
    this.loadingHistory.set(true);
    this.arenaService.getSessionDetail(session.sessionId).subscribe({
      next: (detail) => {
        this.selectedSession.set(detail);
        this.showDetailModal.set(true);
        this.loadingHistory.set(false);
      },
      error: () => {
        this.loadingHistory.set(false);
        // On pourrait ajouter un toast d'erreur ici
      }
    });
  }

  deleteSession(event: Event, session: SessionSummary) {
    event.stopPropagation();
    this.sessionToDelete.set(session);
    this.showDeleteConfirm.set(true);
  }

  cancelDelete() {
    this.showDeleteConfirm.set(false);
    this.sessionToDelete.set(null);
  }

  confirmDeleteSession() {
    const session = this.sessionToDelete();
    if (!session) return;

    this.arenaService.deleteSession(session.sessionId).subscribe({
      next: () => {
        this.pastSessions.update(list => list.filter(s => s.sessionId !== session.sessionId));
        this.triggerToast('Session deleted successfully!');
        this.cancelDelete();
      },
      error: () => {
        // Silently fail — toast could be added here if needed
      }
    });
  }

  triggerToast(msg: string) {
    this.toastMsg.set(msg);
    this.showToast.set(true);
    setTimeout(() => this.showToast.set(false), 3000);
  }

  closeModal() {
    this.showDetailModal.set(false);
    this.selectedSession.set(null);
  }

  setHistoryFilter(filter: 'all' | 'arena' | 'offer') {
    this.selectedHistoryFilter.set(filter);
  }

  onSearchInput(event: Event) {
    const input = event.target as HTMLInputElement;
    this.historySearchQuery.set(input.value);
  }

  onFilterChange(event: Event) {
    const select = event.target as HTMLSelectElement;
    this.selectedHistoryFilter.set(select.value as 'all' | 'arena' | 'offer');
  }

  getModeInitials(s: SessionSummary): string {
    if (s.mode === 'offer' && s.company) {
      return s.company.slice(0, 2).toUpperCase();
    }
    const domainObj = this.domains.find(d => d.key === s.domain);
    if (domainObj) {
      return domainObj.name.split(' ').map(w => w[0]).join('').toUpperCase().slice(0, 2);
    }
    return 'AI';
  }

  getModeGradient(s: SessionSummary): string {
    if (s.mode === 'offer') {
      return 'linear-gradient(135deg, #465FFF 0%, #0C1986 100%)';
    } else {
      const hash = s.domain ? s.domain.charCodeAt(0) : 0;
      if (hash % 2 === 0) {
        return 'linear-gradient(135deg, #EC9F05 0%, #FF4E00 100%)';
      } else {
        return 'linear-gradient(135deg, #7F00FF 0%, #E100FF 100%)';
      }
    }
  }

  getLanguageLabel(langKey: string | null | undefined): string {
    if (!langKey) return '🌐 EN';
    const lang = this.languages.find(l => l.key === langKey.toLowerCase());
    return lang ? `${lang.flag} ${langKey.toUpperCase()}` : '🌐 ' + langKey.toUpperCase();
  }



  stepState(key: ArenaStep): 'done' | 'current' | 'locked' {
    const idx = this.steps().findIndex(s => s.key === key);
    const curr = this.stepIndex();
    if (idx < 0) return 'locked';
    if (idx < curr) return 'done';
    if (idx === curr) return 'current';
    return 'locked';
  }

  stepSummary(key: ArenaStep): string {
    switch (key) {
      case 'domain': return this.domains.find(d => d.key === this.selectedDomain())?.name ?? '';
      case 'level': return this.levels.find(l => l.key === this.selectedLevel())?.label ?? '';
      case 'duration': return this.selectedDuration() + ' min';
      case 'language': return this.languages.find(l => l.key === this.selectedLanguage())?.name ?? '';
      case 'focus': return this.selectedFocus().length ? this.selectedFocus().slice(0, 2).join(', ') : 'Auto';
      default: return '';
    }
  }

  canGoTo(key: ArenaStep): boolean {
    const idx = this.steps().findIndex(s => s.key === key);
    if (idx < 0) return false;
    return this.stepIndex() >= idx;
  }
  canContinue(): boolean {
    switch (this.currentStep()) {
      case 'domain': return !!this.selectedDomain();
      case 'level': return !!this.selectedLevel();
      case 'duration': return !!this.selectedDuration();
      case 'language': return !!this.selectedLanguage();
      default: return true;
    }
  }

  summaryDomain() { return this.domains.find(d => d.key === this.selectedDomain()); }
  summaryLevel() { return this.levels.find(l => l.key === this.selectedLevel()); }
  summaryLanguage() { return this.languages.find(l => l.key === this.selectedLanguage()); }

  selectMode(mode: 'offer' | 'arena') { if (mode === 'arena') this.view.set('arena'); else this.router.navigate(['/offers']); }
  backToSelector() {
    this.view.set('selector');
    this.selectedOfferId.set(null);
    this.selectedJobTitle.set(null);
    this.selectedCompany.set(null);
    this.currentStep.set('domain');
    this.isLaunching.set(false);
  }
  goToStep(key: ArenaStep) { if (this.canGoTo(key)) this.currentStep.set(key); }
  next() { const idx = this.stepIndex(); if (idx < this.steps().length - 1) this.currentStep.set(this.steps()[idx + 1].key); }
  back() { const idx = this.stepIndex(); if (idx > 0) this.currentStep.set(this.steps()[idx - 1].key); }

  pickDomain(key: string) { this.selectedDomain.set(key); this.selectedFocus.set([]); setTimeout(() => this.next(), 220); }
  pickLevel(key: string) { this.selectedLevel.set(key as InterviewLevel); setTimeout(() => this.next(), 220); }
  pickDuration(val: number) { this.selectedDuration.set(val); setTimeout(() => this.next(), 220); }
  pickLanguage(key: string) { this.selectedLanguage.set(key); setTimeout(() => this.next(), 220); }
  toggleFocus(f: string) {
    const curr = this.selectedFocus();
    if (curr.includes(f)) this.selectedFocus.set(curr.filter(x => x !== f));
    else this.selectedFocus.set([...curr, f]);
  }

  launch() {
    this.isLaunching.set(true);
    const config: ArenaConfig = {
      domain: this.selectedDomain(),
      level: this.selectedLevel(),
      duration_minutes: this.selectedDuration(),
      language: this.selectedLanguage(),
      focus_areas: this.selectedFocus(),
      offer_id: this.selectedOfferId() ?? undefined,
      job_title: this.selectedJobTitle() ?? undefined,
      company: this.selectedCompany() ?? undefined
    };
    setTimeout(() => { this.router.navigate(['/chatbot/arena'], { state: { arenaConfig: config } }); }, 1800);
  }
}