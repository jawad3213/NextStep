import { Component, signal, computed, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import {
  ArenaConfig, DOMAINS, LEVELS, DURATIONS, LANGUAGES,
  FOCUS_BY_DOMAIN, InterviewLevel, PastSessionDto, SessionCoachingDetailsDto
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

  // ── History Data
  pastSessions = signal<PastSessionDto[]>([]);
  selectedSession = signal<SessionCoachingDetailsDto | null>(null);
  showDetails = signal(false);

  // ── Data
  readonly domains = DOMAINS;
  readonly levels = LEVELS;
  readonly durations = DURATIONS;
  readonly languages = LANGUAGES;
  readonly steps = STEPS;

  focusPool = computed(() => FOCUS_BY_DOMAIN[this.selectedDomain()] ?? FOCUS_BY_DOMAIN['software']);
  stepIndex = computed(() => STEPS.findIndex(s => s.key === this.currentStep()));

  ngOnInit() {
    this.loadHistory();
  }

  loadHistory() {
    this.http.get<any[]>('/api/arena/sessions').subscribe({
      next: (data) => {
        const mapped = data.map(s => ({
          ...s,
          id: s.sessionId,
          id_session: s.sessionId,
          scoreEntretien: s.score,
          durationMinutes: s.durationMinutes,
          dateSession: s.dateSession
        }));
        this.pastSessions.set(mapped);
      },
      error: (err) => console.error('Failed to load history', err)
    });
  }

  viewDetails(session: PastSessionDto) {
    this.http.get<any>(`/api/arena/sessions/${session.id}/details`).subscribe({
      next: (res) => {
        const details: SessionCoachingDetailsDto = {
          ...res,
          global_score: res.globalScore,
          coaching_tips: res.coachingTips,
          domain: session.domain,
          level: session.level,
          mode: session.mode,
          date: session.dateSession
        };
        this.selectedSession.set(details);
        this.showDetails.set(true);
      },
      error: (err) => console.error('Failed to load details', err)
    });
  }

  closeDetails() { this.showDetails.set(false); setTimeout(() => this.selectedSession.set(null), 300); }

  stepState(key: ArenaStep): 'done' | 'current' | 'locked' {
    const idx = STEPS.findIndex(s => s.key === key);
    const curr = this.stepIndex();
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

  canGoTo(key: ArenaStep): boolean { return this.stepIndex() >= STEPS.findIndex(s => s.key === key); }
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
  backToSelector() { this.view.set('selector'); this.currentStep.set('domain'); this.isLaunching.set(false); }
  goToStep(key: ArenaStep) { if (this.canGoTo(key)) this.currentStep.set(key); }
  next() { const idx = this.stepIndex(); if (idx < STEPS.length - 1) this.currentStep.set(STEPS[idx + 1].key); }
  back() { const idx = this.stepIndex(); if (idx > 0) this.currentStep.set(STEPS[idx - 1].key); }

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
    };
    setTimeout(() => { this.router.navigate(['/chatbot/arena'], { state: { arenaConfig: config } }); }, 1800);
  }
}