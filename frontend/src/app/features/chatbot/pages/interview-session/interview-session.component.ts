import {
  Component, OnInit, OnDestroy, signal, computed,
  ViewChild, ElementRef, AfterViewChecked,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ArenaService } from '../../services/arena.service';
import {
  SessionConfig, ArenaConfig, ChatMessage, FeedbackResult,
} from '../../models/arena.models';

@Component({
  selector: 'app-interview-session',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './interview-session.component.html',
  styleUrls: ['./interview-session.component.scss'],
})
export class InterviewSessionComponent implements OnInit, OnDestroy, AfterViewChecked {

  @ViewChild('chatScroll') chatScroll!: ElementRef;
  @ViewChild('msgInput')   msgInput!:   ElementRef;

  // ── Config (shared between Arena & Offer) ───────────────────
  config!: SessionConfig;

  // ── Session state ────────────────────────────────────────────
  phase = signal<'connecting' | 'live' | 'evaluating' | 'results'>('connecting');
  sessionId = signal('');
  messages  = signal<ChatMessage[]>([]);
  userInput = '';
  aiTyping  = signal(false);

  // ── Timer ────────────────────────────────────────────────────
  totalSecs     = 0;
  remaining     = signal(0);
  private timerRef?: ReturnType<typeof setInterval>;

  readonly CIRCUMFERENCE = 2 * Math.PI * 45; // Match r=45 from HTML

  // ── Evaluation results ───────────────────────────────────────
  feedback   = signal<FeedbackResult | null>(null);
  finalScore = signal(0);

  private needsScroll = false;

  constructor(private svc: ArenaService, private router: Router) {}

  // ── Lifecycle ────────────────────────────────────────────────

  ngOnInit() {
    const state = history.state as { sessionConfig?: SessionConfig; arenaConfig?: ArenaConfig };

    // Support both SessionConfig (new) and legacy ArenaConfig (arena-session redirect)
    if (state?.sessionConfig) {
      this.config = state.sessionConfig;
    } else if (state?.arenaConfig) {
      const c = state.arenaConfig as any;
      this.config = {
        mode: 'arena',
        domain: c.domain, level: c.level,
        duration_minutes: c.duration_minutes,
        language: c.language, focus_areas: c.focus_areas,
        display_title: c.display_title || `${this.titlecase(c.domain)} Arena`,
        display_emoji: c.display_emoji || this.domainEmojiFor(c.domain),
        session_id: c.session_id // On récupère l'ID passé
      };
    } else {
      this.router.navigate(['/chatbot']);
      return;
    }

    this.totalSecs = this.config.duration_minutes * 60;
    this.remaining.set(this.totalSecs);
    this.startSession();
  }

  ngOnDestroy() { this.stopTimer(); }

  ngAfterViewChecked() {
    if (this.needsScroll) {
      try {
        const el = this.chatScroll?.nativeElement;
        if (el) el.scrollTop = el.scrollHeight;
      } catch {}
      this.needsScroll = false;
    }
  }

  // ── Session start (optimistic) ───────────────────────────────

  startSession() {
    this.phase.set('live');
    this.aiTyping.set(true);
    this.startTimer();
    this.needsScroll = true;
    setTimeout(() => this.msgInput?.nativeElement?.focus(), 800);

    const arenaConfig = this.toArenaConfig();
    this.svc.startSession(arenaConfig, this.config.session_id, this.config.questions).subscribe({
      next: r => {
        this.sessionId.set(r.session_id);
        this.messages.set([{ role: 'ai', content: r.opening_message, timestamp: new Date() }]);
        this.aiTyping.set(false);
        this.needsScroll = true;
      },
      error: () => {
        const title = this.config.display_title ?? `${this.titlecase(this.config.domain)} Interview`;
        const level = this.levelLabel();
        const msg = `Bonjour ! Je suis votre recruteur IA pour cette session "${title}" — niveau ${level}.\n\nCommençons ! Présentez-vous en 2-3 phrases, en mettant en avant votre expérience clé.`;
        this.messages.set([{ role: 'ai', content: msg, timestamp: new Date() }]);
        this.aiTyping.set(false);
        this.needsScroll = true;
      },
    });
  }

  // ── Messaging ────────────────────────────────────────────────

  send() {
    const v = this.userInput.trim();
    if (!v || this.aiTyping() || this.phase() !== 'live') return;
    this.messages.update(m => [...m, { role: 'user', content: v, timestamp: new Date() }]);
    this.userInput = '';
    this.aiTyping.set(true);
    this.needsScroll = true;

    const arenaConfig = this.toArenaConfig();
    this.svc.sendMessage(this.sessionId(), v, this.messages(), arenaConfig).subscribe({
      next: r => {
        this.messages.update(m => [...m, { role: 'ai', content: r.ai_response, timestamp: new Date() }]);
        this.aiTyping.set(false);
        this.needsScroll = true;
      },
      error: () => {
        this.messages.update(m => [...m, {
          role: 'ai',
          content: "Good answer! Elaborate further by giving a concrete example with measurable figures or results.",
          timestamp: new Date(),
        }]);
        this.aiTyping.set(false);
        this.needsScroll = true;
      },
    });
  }

  onKey(e: KeyboardEvent) {
    if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); this.send(); }
  }

  // ── Timer ────────────────────────────────────────────────────

  private startTimer() {
    this.timerRef = setInterval(() => {
      const v = this.remaining() - 1;
      this.remaining.set(v);
      if (v <= 0) { this.stopTimer(); this.endSession(); }
    }, 1000);
  }

  private stopTimer() {
    if (this.timerRef) { clearInterval(this.timerRef); this.timerRef = undefined; }
  }

  // ── End & Evaluate ───────────────────────────────────────────

  endEarly() { this.stopTimer(); this.endSession(); }

  private endSession() {
    if (this.phase() === 'evaluating' || this.phase() === 'results') return;
    this.phase.set('evaluating');

    const arenaConfig = this.toArenaConfig();
    this.svc.endSession(this.sessionId(), this.messages(), arenaConfig).subscribe({
      next: r => {
        this.feedback.set(r.feedback);
        this.finalScore.set(r.score);
        this.phase.set('results');
      },
      error: (err) => {
        console.error('Evaluation Error:', err);
        // Au lieu d'un faux 65, on affiche une erreur ou on reste en live
        alert("Evaluation failed. The AI is taking too long to respond. Please try again.");
        this.phase.set('live'); 
      },
    });
  }

  // ── Helpers ──────────────────────────────────────────────────

  scoreColor() {
    const s = this.finalScore();
    return s >= 75 ? '#16A34A' : s >= 50 ? '#D97706' : '#DC2626';
  }

  scoreLabel() {
    const s = this.finalScore();
    return s >= 80 ? 'Excellent 🏆' : s >= 65 ? 'Bon niveau' : s >= 50 ? 'Correct' : 'À améliorer';
  }

  scoreBg() {
    const s = this.finalScore();
    return s >= 75 ? 'var(--green-bg)' : s >= 50 ? 'var(--gold-bg)' : 'var(--red-bg)';
  }

  levelLabel() {
    return { junior: 'Junior', mid: 'Mid-level', senior: 'Senior' }[this.config?.level] ?? '';
  }

  langFlag() {
    const m: Record<string, string> = { en: '🇬🇧', fr: '🇫🇷', es: '🇪🇸', ar: '🇸🇦', de: '🇩🇪', zh: '🇨🇳' };
    return m[this.config?.language] ?? '🌐';
  }

  displayTitle(): string {
    if (!this.config) return 'Interview Session';
    if (this.config?.display_title) return this.config.display_title;
    if (this.config?.mode === 'offer' && this.config?.job_title && this.config?.company) {
      return `${this.config.job_title} @ ${this.config.company}`;
    }
    return `${this.titlecase(this.config?.domain || '')} Arena`;
  }

  displayEmoji(): string {
    if (!this.config) return '🎯';
    return this.config.display_emoji ?? this.domainEmojiFor(this.config.domain);
  }

  msgCount() { return Math.floor(this.messages().filter(m => m.role === 'user').length); }

  isWarning() { return this.remaining() < 60; }

  timerDisplay(): string {
    const s = this.remaining();
    const mm = Math.floor(s / 60);
    const ss = s % 60;
    return `${mm}:${ss < 10 ? '0' : ''}${ss}`;
  }

  timerDashOffset(): number {
    const total = this.totalSecs || 1;
    const rem = this.remaining();
    const ratio = rem / total;
    const circ = 2 * Math.PI * 45; 
    return circ * (1 - ratio);
  }

  back() {
    this.stopTimer();
    this.router.navigate(['/chatbot/arena'], {
      state: { arenaConfig: this.toArenaConfig() }
    });
  }

  restart() {
    this.stopTimer();
    // Réinitialisation de l'état
    this.messages.set([]);
    this.phase.set('connecting');
    this.remaining.set(this.totalSecs);
    this.feedback.set(null);
    this.finalScore.set(0);
    this.sessionId.set('');
    
    // Relancer la session
    this.startSession();
  }

  private toArenaConfig(): ArenaConfig {
    return {
      domain:           this.config.domain,
      level:            this.config.level,
      duration_minutes: this.config.duration_minutes,
      language:         this.config.language,
      focus_areas:      this.config.focus_areas,
    };
  }

  private domainEmojiFor(domain: string): string {
    const map: Record<string, string> = {
      software: '💻', data: '📊', design: '🎨', product: '📈',
      finance: '💼', engineering: '🏗️', sales: '🤝', consulting: '🎓',
    };
    return map[domain] ?? '⚡';
  }

  private titlecase(s: string) {
    return s ? s.charAt(0).toUpperCase() + s.slice(1) : '';
  }
}
