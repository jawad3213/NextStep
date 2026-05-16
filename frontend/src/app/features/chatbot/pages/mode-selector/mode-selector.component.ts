import { Component, signal, computed, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { ArenaService } from '../../services/arena.service';
import { AuthService } from '../../../../core/auth/services/auth.service';
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
  pastSessions = signal<SessionSummary[]>([]);
  selectedSession = signal<SessionDetail | null>(null);
  showDetailModal = signal(false);
  loadingHistory = signal(false);
  showDeleteConfirm = signal(false);
  sessionToDelete = signal<SessionSummary | null>(null);
  
  // Notification Toast
  showToast = signal(false);
  toastMsg = signal('');

  // ── Data
  readonly domains = DOMAINS;
  readonly levels = LEVELS;
  readonly durations = DURATIONS;
  readonly languages = LANGUAGES;
  readonly steps = STEPS;

  focusPool = computed(() => FOCUS_BY_DOMAIN[this.selectedDomain()] ?? FOCUS_BY_DOMAIN['software']);
  stepIndex = computed(() => STEPS.findIndex(s => s.key === this.currentStep()));

  ngOnInit() {
    // Attendez que Keycloak soit prêt avec un petit délai
    // Keycloak s'initialise de manière asynchrone
    setTimeout(() => {
      if (this.authService.isAuthenticated()) {
        console.log('✅ Utilisateur authentifié - chargement de l\'historique');
        this.loadHistory();
      } else {
        console.warn('⏳ Utilisateur non authentifié - historique non chargé');
        this.pastSessions.set([]);
      }
    }, 500); // Donne 500ms à Keycloak pour initialiser
  }

  private loadHistory() {
    // Récupère le token JWT depuis Keycloak
    const token = this.authService.getToken();
    if (!token) {
      console.warn('❌ Token Keycloak manquant!');
      this.pastSessions.set([]);
      return;
    }

    // Décode le token pour extraire le userId (claim "sub")
    const userId = this.extractUserIdFromToken(token);
    if (!userId) {
      console.warn('❌ userId manquant dans le token!');
      this.pastSessions.set([]);
      return;
    }

    console.log('✅ userId trouvé:', userId);
    this.loadingHistory.set(true);

    this.arenaService.getSessions(userId).subscribe({
      next: (sessions) => {
        console.log('✅ Sessions chargées:', sessions.length);
        console.log('📊 Réponse API complète:', sessions);
        this.pastSessions.set(sessions);
        this.loadingHistory.set(false);
      },
      error: (err) => {
        console.error('❌ Erreur chargement historique:', err);
        console.error('📡 Détails erreur:', err.status, err.message);
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
      console.log('🔍 JWT Payload:', payload);
      
      // Le userId se trouve dans le claim "sub"
      const userId = payload.sub || payload.userId;
      if (userId) {
        console.log('✅ userId extrait du token:', userId);
      }
      return userId || null;
    } catch (error) {
      console.error('❌ Erreur décodage token:', error);
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
    console.log('🗑️ Tentative de suppression de la session:', this.sessionToDelete()?.sessionId);
    const session = this.sessionToDelete();
    if (!session) {
      console.warn('⚠️ Aucune session à supprimer (sessionToDelete est null)');
      return;
    }

    this.arenaService.deleteSession(session.sessionId).subscribe({
      next: () => {
        console.log('✅ Session supprimée avec succès');
        this.pastSessions.update(list => list.filter(s => s.sessionId !== session.sessionId));
        this.triggerToast('Session deleted successfully!');
        this.cancelDelete();
      },
      error: (err) => {
        console.error('❌ Erreur suppression:', err);
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