import {
  Component, OnInit, OnDestroy, signal, computed,
  ViewChild, ElementRef, AfterViewChecked,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ArenaService } from '../../services/arena.service';
import {
  ArenaConfig, ChatMessage, QuestionItem,
  FeedbackResult, SalaryResult, ActiveTab, SessionConfig
} from '../../models/arena.models';

@Component({
  selector: 'app-arena-session',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './arena-session.component.html',
  styleUrls: ['./arena-session.component.scss'],
})
export class ArenaSessionComponent implements OnInit, OnDestroy, AfterViewChecked {

  @ViewChild('interviewScroll') interviewScroll!: ElementRef;
  @ViewChild('chatScroll') chatScroll!: ElementRef;
  @ViewChild('msgInput') msgInput!: ElementRef;

  config!: ArenaConfig;

  // ── Tabs ──────────────────────────────────────────────────
  activeTab = signal<ActiveTab>('questions');

  // ── Tab 1 ─────────────────────────────────────────────────
  questions = signal<QuestionItem[]>([]);
  loadingQs = signal(false);
  filterType = signal<'all' | 'behavioral' | 'technical' | 'situational'>('all');
  chatMessages = signal<ChatMessage[]>([]);
  chatInput = '';
  chatLoading = signal(false);
  threadId = crypto.randomUUID();
  expandedTip = signal<string | null>(null);

  filteredQs = computed(() => {
    const f = this.filterType(), q = this.questions();
    return f === 'all' ? q : q.filter(x => x.type === f);
  });

  // ── Tab 2 ─────────────────────────────────────────────────
  sessionId = signal('');
  sessionStarted = signal(false);
  interviewMsgs = signal<ChatMessage[]>([]);
  userInput = '';
  aiTyping = signal(false);
  isLoadingStart = signal(false);

  // Timer
  totalSecs = 0;
  remainingSecs = signal(0);
  timerRef?: ReturnType<typeof setInterval>;
  timerDisplay = computed(() => {
    const s = this.remainingSecs();
    return `${String(Math.floor(s / 60)).padStart(2, '0')}:${String(s % 60).padStart(2, '0')}`;
  });
  timerPct = computed(() => this.totalSecs ? (this.remainingSecs() / this.totalSecs) * 100 : 100);
  isWarning = computed(() => this.remainingSecs() < 120 && this.sessionStarted());

  // SVG circular timer: circumference of r=42 => 2π×42 ≈ 263.89
  private readonly CIRCUMFERENCE = 2 * Math.PI * 42;
  timerDashOffset = computed(() => {
    const pct = this.totalSecs ? this.remainingSecs() / this.totalSecs : 1;
    return this.CIRCUMFERENCE * (1 - pct);
  });

  // Results
  showResults = signal(false);
  isEvaluating = signal(false);
  feedback = signal<FeedbackResult | null>(null);
  finalScore = signal(0);

  // ── Tab 3 ─────────────────────────────────────────────────
  salary = signal<SalaryResult | null>(null);
  salaryLoading = signal(false);
  salaryLoaded = signal(false);
  activeStep = signal(0);

  private needsScrollInterview = false;
  private needsScrollChat = false;

  constructor(private svc: ArenaService, private router: Router) { }

  ngOnInit() {
    const state = history.state as { arenaConfig?: ArenaConfig };
    if (!state?.arenaConfig) { this.router.navigate(['/chatbot']); return; }
    this.config = state.arenaConfig;
    this.totalSecs = this.config.duration_minutes * 60;
    this.remainingSecs.set(this.totalSecs);
    this.loadQuestions();
  }

  ngOnDestroy() { this.stopTimer(); }

  ngAfterViewChecked() {
    if (this.needsScrollInterview) { this.scrollEl(this.interviewScroll); this.needsScrollInterview = false; }
    if (this.needsScrollChat) { this.scrollEl(this.chatScroll); this.needsScrollChat = false; }
  }

  // ── TABS ──────────────────────────────────────────────────

  setTab(t: ActiveTab) {
    this.activeTab.set(t);
    if (t === 'salary' && !this.salaryLoaded()) this.loadSalary();
  }

  // ── TAB 1 ─────────────────────────────────────────────────

  loadQuestions() {
    this.loadingQs.set(true);
    const fallbackQs: QuestionItem[] = [
      { id: '1', type: 'behavioral', question: "Parlez-moi d'une fois où vous avez dû gérer un conflit au sein de votre équipe. Quelle a été votre approche ?", tip: "Utilisez la méthode STAR. Focus sur la résolution et l'écoute active.", company_specific: true, source: 'glassdoor' },
      { id: '2', type: 'technical', question: "Comment optimiseriez-vous une requête SQL complexe qui met trop de temps à s'exécuter ?", tip: "Parlez d'indexation, d'analyse de plan d'exécution et de réduction des jointures inutiles.", company_specific: false, source: 'generated' },
      { id: '3', type: 'situational', question: "Si un client demande une fonctionnalité impossible à livrer dans les temps, que faites-vous ?", tip: "Proposez une solution alternative ou un MVP. Soyez transparent.", company_specific: true, source: 'glassdoor' },
      { id: '4', type: 'technical', question: "Quelle est la différence entre un index clustered et non-clustered ?", tip: "Le clustered détermine l'ordre physique des données. Un seul par table.", company_specific: false, source: 'generated' }
    ];

    this.svc.getQuestions(this.config).subscribe({
      next: r => { 
        this.questions.set(r.questions && r.questions.length > 0 ? r.questions : fallbackQs);
        this.sessionId.set(r.session_id); // On capture l'ID ici
        this.loadingQs.set(false); 
      },
      error: () => {
        this.questions.set(fallbackQs);
        this.loadingQs.set(false);
      },
    });
  }

  setFilter(f: 'all' | 'behavioral' | 'technical' | 'situational') { this.filterType.set(f); }
  toggleTip(id: string) { this.expandedTip.set(this.expandedTip() === id ? null : id); }

  sendChat() {
    const v = this.chatInput.trim(); if (!v || this.chatLoading()) return;
    this.chatMessages.update(m => [...m, { role: 'user', content: v, timestamp: new Date() }]);
    this.chatInput = ''; this.chatLoading.set(true); this.needsScrollChat = true;
    this.svc.freeChat(v, this.threadId, this.chatMessages()).subscribe({
      next: r => {
        this.chatMessages.update(m => [...m, { role: 'ai', content: r.response, timestamp: new Date() }]);
        this.chatLoading.set(false); this.needsScrollChat = true;
      },
      error: () => this.chatLoading.set(false),
    });
  }

  onChatKey(e: KeyboardEvent) { if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); this.sendChat(); } }

  suggest(text: string) { this.chatInput = text; this.sendChat(); }

  // ── TAB 2 ─────────────────────────────────────────────────

  startInterview() {
    const sessionConfig: SessionConfig = {
      mode: 'arena',
      domain: this.config.domain,
      level: this.config.level,
      duration_minutes: this.config.duration_minutes,
      language: this.config.language,
      focus_areas: this.config.focus_areas,
      display_title: `${this.config.domain.charAt(0).toUpperCase() + this.config.domain.slice(1)} Arena`,
      display_emoji: '⚡',
      session_id: this.sessionId(),
      questions: this.questions()
    };
    
    this.router.navigate(['/chatbot/interview'], { state: { sessionConfig } });
  }

  sendMessage() {
    const v = this.userInput.trim();
    if (!v || this.aiTyping() || this.showResults()) return;
    this.interviewMsgs.update(m => [...m, { role: 'user', content: v, timestamp: new Date() }]);
    this.userInput = '';
    this.aiTyping.set(true);
    this.needsScrollInterview = true;

    this.svc.sendMessage(this.sessionId(), v, this.interviewMsgs(), this.config).subscribe({
      next: r => {
        this.interviewMsgs.update(m => [...m, { role: 'ai', content: r.ai_response, timestamp: new Date() }]);
        this.aiTyping.set(false);
        this.needsScrollInterview = true;
      },
      error: () => {
        // Graceful fallback — keep the session alive
        this.interviewMsgs.update(m => [...m, { role: 'ai', content: "Thank you for your response. Please elaborate further — provide a concrete example if possible.", timestamp: new Date() }]);
        this.aiTyping.set(false);
        this.needsScrollInterview = true;
      },
    });
  }

  onMsgKey(e: KeyboardEvent) { if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); this.sendMessage(); } }

  endEarly() { this.stopTimer(); this.evaluate(); }

  private startTimer() {
    this.timerRef = setInterval(() => {
      const v = this.remainingSecs() - 1;
      this.remainingSecs.set(v);
      if (v <= 0) { this.stopTimer(); this.evaluate(); }
    }, 1000);
  }

  private stopTimer() { if (this.timerRef) { clearInterval(this.timerRef); this.timerRef = undefined; } }

  private evaluate() {
    if (this.isEvaluating() || this.showResults()) return;
    this.isEvaluating.set(true);
    this.svc.endSession(this.sessionId(), this.interviewMsgs(), this.config).subscribe({
      next: r => {
        this.feedback.set(r.feedback); this.finalScore.set(r.score);
        this.isEvaluating.set(false); this.showResults.set(true);
      },
      error: () => this.isEvaluating.set(false),
    });
  }

  scoreColor() {
    const s = this.finalScore();
    return s >= 75 ? '#16A34A' : s >= 50 ? '#D97706' : '#DC2626';
  }

  scoreLabel() {
    const s = this.finalScore();
    return s >= 80 ? 'Excellent' : s >= 65 ? 'Good' : s >= 50 ? 'Fair' : 'Needs work';
  }

  // ── TAB 3 ─────────────────────────────────────────────────

  loadSalary() {
    this.salaryLoading.set(true);
    this.svc.getSalary(this.config).subscribe({
      next: r => { this.salary.set(r); this.salaryLoading.set(false); this.salaryLoaded.set(true); },
      error: () => this.salaryLoading.set(false),
    });
  }

  setScriptStep(i: number) { this.activeStep.set(i); }

  // ── HELPERS ───────────────────────────────────────────────

  back() { this.stopTimer(); this.router.navigate(['/chatbot']); }
  restart() { this.stopTimer(); this.router.navigate(['/chatbot']); }

  goToSalaryCoach() {
    this.stopTimer();
    this.router.navigate(['/chatbot/salary-coach'], { state: { arenaConfig: this.config } });
  }

  domainEmoji(): string {
    const map: Record<string, string> = {
      software: '💻', data: '📊', design: '🎨', product: '📈',
      finance: '💼', engineering: '🏗️', sales: '🤝', consulting: '🎓',
    };
    return map[this.config?.domain] ?? '⚡';
  }

  levelLabel() {
    return { junior: 'Junior', mid: 'Mid-level', senior: 'Senior' }[this.config?.level] ?? '';
  }

  langFlag() {
    const m: Record<string, string> = { en: '🇬🇧', fr: '🇫🇷', es: '🇪🇸', ar: '🇸🇦', de: '🇩🇪', zh: '🇨🇳' };
    return m[this.config?.language] ?? '🌐';
  }

  qCount(type: string) { return this.questions().filter(q => q.type === type).length; }

  private scrollEl(ref: ElementRef) {
    try { const el = ref?.nativeElement; if (el) el.scrollTop = el.scrollHeight; } catch { }
  }
}