// ============================================================
// PipelineStateService — State partagé GLOBAL pour le pipeline
// Utilisé par le stepper Jobs + les badges sidebar
// ============================================================
import { Injectable, signal, computed } from '@angular/core';
import { GenerationProgress } from './signalr.service';

export type PipelineStep = 1 | 2 | 3 | 4 | 5;

export type PipelineStepStatus =
  | 'idle'       // pas encore démarré
  | 'active'     // en cours
  | 'done'       // terminé avec succès
  | 'error';     // erreur

export interface StepState {
  status: PipelineStepStatus;
  label: string;
}

export interface SidebarBadge {
  page: string;            // "cv-builder" | "email" | "company-intel" | "skill-gap" | "notifications"
  count?: number;
  label: string;
  variant: 'green' | 'blue' | 'red' | 'amber';
  visible: boolean;
}

export interface SkillDetail {
  name: string;
  category: 'technique' | 'soft' | 'langue' | 'certification';
  status: 'correspond' | 'partiel' | 'manquant';
}

export interface RecommendationPriorisee {
  text: string;
  priority: 'haute' | 'moyenne' | 'basse';
}

export interface KeywordPondere {
  word: string;
  weight: number;
}

export interface PipelineResult {
  // ─── Agent 1 : Analyse offre (OfferAnalyzerService) ───
  offerTitle: string;
  companyName: string;
  contractType: string;
  location?: string;
  requiredSkills: string[];
  preferredSkills: string[];
  experienceYears?: number;
  educationLevel?: string;

  // ─── Agent 2 : Récupération profil (ProfileRetrieverService) ───
  // (profil interne — pas de champs UI directs, alimente Agent 3)

  // ─── Agent 3 : Skill Gap (SkillGapService) ───
  matchScore: number;           // 0-100
  atsScore: number;             // 0-100
  matchBreakdown: {
    skills: number;
    experience: number;
    location: number;
  };
  keywordsPresent: string[];
  keywordsMissing: string[];
  matchingSkills: string[];
  missingSkills: string[];
  recommendations: string[];

  // ─── Données entreprise (Agent 4 — pas dans Step 1) ───
  companyCultureScore: number;
  companySalaryMin: number;
  companySalaryMax: number;
  companySize: string;
  companyNews: { title: string; date: string }[];

  // ─── Données avancées (Steps suivants) ───
  profileStrengths: string[];
  skillGaps: { skill: string; priority: string; weeks: number }[];
  cvPdfPath: string;
  atsImprovements: string[];
  emailSubject: string;
  emailBody: string;
  recruiterName: string;
  coverLetterContent: string;

  // ─── Enrichissements backend (v2) ───
  skillDetails?: SkillDetail[];
  recommendationsWithPriority?: RecommendationPriorisee[];
  keywordWeights?: KeywordPondere[];
  profileStrengthsList?: string[];
}

@Injectable({ providedIn: 'root' })
export class PipelineStateService {
  private readonly STORAGE_KEY = 'nextstep.offer.pipeline.v1';

  // ── State du stepper ────────────────────────────────────────────────────
  readonly currentStep = signal<PipelineStep>(1);
  readonly isFlowOpen  = signal<boolean>(false);
  readonly isLoading   = signal<boolean>(false);
  readonly loadingMessage = signal<string>('');

  readonly steps = signal<StepState[]>([
    { status: 'idle', label: 'Offre' },
    { status: 'idle', label: 'Skill Gap' },
    { status: 'idle', label: 'Template' },
    { status: 'idle', label: 'Génération' },
    { status: 'idle', label: 'Résultats' },
  ]);

  // ── Entrées utilisateur ─────────────────────────────────────────────────
  readonly selectedTemplateId = signal<string>('modern');
  readonly offerUrl  = signal<string>('');
  readonly offerText = signal<string>('');
  readonly currentOfferId = signal<string | null>(null);

  // ── Résultats pipeline ──────────────────────────────────────────────────
  readonly pipelineResult = signal<PipelineResult | null>(null);

  // ── SignalR progress ────────────────────────────────────────────────────
  readonly currentAgentProgress = signal<{
    step: string;
    agentName: string;
    label: string;
    status: 'running' | 'done' | 'todo';
    progressPercent: number;
  } | null>(null);

  readonly pipelineError = signal<string | null>(null);

  // ── PDF Generation ──────────────────────────────────────────────────────
  readonly generationProgress = signal<GenerationProgress | null>(null);
  readonly cvDownloadUrl = signal<string | null>(null);

  // ── Badges sidebar ──────────────────────────────────────────────────────
  readonly sidebarBadges = signal<SidebarBadge[]>([
    { page: 'cv-builder',    label: '',        variant: 'green', visible: false },
    { page: 'email',         label: 'Généré',  variant: 'amber', visible: false },
    { page: 'company-intel', label: 'Prêt',    variant: 'green', visible: false },
    { page: 'skill-gap',     label: '0 gaps',  variant: 'red',   visible: false },
    { page: 'notifications', label: '0',       variant: 'red',   visible: false },
  ]);

  // ── Computed helpers ────────────────────────────────────────────────────
  readonly canGoNext = computed(() => {
    const step = this.currentStep();
    const loading = this.isLoading();
    if (loading) return false;
    if (step === 1) return !!this.offerUrl() || !!this.offerText();
    return true;
  });

  readonly progressPercent = computed(() =>
    Math.round(((this.currentStep() - 1) / 4) * 100)
  );

  constructor() {
    this.restoreFromStorage();
  }

  // ── Actions ─────────────────────────────────────────────────────────────

  openFlow(): void {
    this.isFlowOpen.set(true);
    if (this.currentStep() < 1) {
      this.currentStep.set(1);
    }
    if (this.steps()[0]?.status === 'idle') {
      this.updateStepStatus(0, 'active');
    }
    this.persistToStorage();
  }

  closeFlow(): void {
    this.isFlowOpen.set(false);
    this.persistToStorage();
  }

  goToStep(step: PipelineStep): void {
    this.currentStep.set(step);
    this.persistToStorage();
  }

  markStepDone(stepIndex: number): void {
    this.updateStepStatus(stepIndex, 'done');
    this.persistToStorage();
  }

  setLoading(loading: boolean, message = ''): void {
    this.isLoading.set(loading);
    this.loadingMessage.set(message);
    this.persistToStorage();
  }

  setResult(result: PipelineResult): void {
    this.pipelineResult.set(result);
    this.persistToStorage();
  }

  showSidebarBadge(
    page: string,
    label: string,
    variant: 'green' | 'blue' | 'red' | 'amber'
  ): void {
    this.sidebarBadges.update(badges =>
      badges.map(b =>
        b.page === page ? { ...b, label, variant, visible: true } : b
      )
    );
  }

  hideSidebarBadge(page: string): void {
    this.sidebarBadges.update(badges =>
      badges.map(b => b.page === page ? { ...b, visible: false } : b)
    );
  }

  resetAll(): void {
    this.isFlowOpen.set(false);
    this.currentStep.set(1);
    this.isLoading.set(false);
    this.pipelineResult.set(null);
    this.offerUrl.set('');
    this.offerText.set('');
    this.currentOfferId.set(null);
    this.currentAgentProgress.set(null);
    this.generationProgress.set(null);
    this.cvDownloadUrl.set(null);
    this.pipelineError.set(null);
    this.resetSteps();
    this.sidebarBadges.update(badges => badges.map(b => ({ ...b, visible: false })));
    this.persistToStorage();
  }

  hydrateFromAnalysis(offerId: string, dto: any): void {
    this.currentOfferId.set(offerId);
    this.setResult({
      offerTitle: dto.titre ?? '',
      companyName: dto.entreprise ?? '',
      contractType: dto.typeContrat ?? '',
      location: dto.localisation ?? undefined,
      requiredSkills: dto.competencesRequises ?? [],
      preferredSkills: dto.competencesSouhaitees ?? [],
      experienceYears: dto.anneesExperience ?? undefined,
      educationLevel: dto.niveauEtudes ?? undefined,
      matchScore: dto.scoreMatching ?? 0,
      atsScore: dto.scoreAts ?? 0,
      matchBreakdown: { skills: 0, experience: 0, location: 0 },
      keywordsPresent: dto.keywordsPresents ?? [],
      keywordsMissing: dto.keywordsManquants ?? [],
      matchingSkills: dto.competencesMatching ?? [],
      missingSkills: dto.competencesManquantes ?? [],
      recommendations: dto.recommandations ?? [],
      companyCultureScore: dto.companyCultureScore ?? 0,
      companySalaryMin: dto.companySalaryMin ?? 0,
      companySalaryMax: dto.companySalaryMax ?? 0,
      companySize: dto.companySize ?? '',
      companyNews: dto.companyNews ?? [],
      profileStrengths: [],
      skillGaps: [],
      cvPdfPath: '',
      atsImprovements: [],
      emailSubject: '',
      emailBody: '',
      recruiterName: '',
      coverLetterContent: '',
    });
    this.markStepDone(0);
    this.markStepDone(1);
    if (this.currentStep() < 2) {
      this.goToStep(2);
    }
    this.isFlowOpen.set(true);
    this.persistToStorage();
  }

  private persistToStorage(): void {
    try {
      localStorage.setItem(this.STORAGE_KEY, JSON.stringify({
        currentStep: this.currentStep(),
        isFlowOpen: this.isFlowOpen(),
        steps: this.steps(),
        selectedTemplateId: this.selectedTemplateId(),
        offerUrl: this.offerUrl(),
        offerText: this.offerText(),
        currentOfferId: this.currentOfferId(),
        pipelineResult: this.pipelineResult(),
        cvDownloadUrl: this.cvDownloadUrl(),
      }));
    } catch {}
  }

  private restoreFromStorage(): void {
    try {
      const raw = localStorage.getItem(this.STORAGE_KEY);
      if (!raw) return;
      const s = JSON.parse(raw);
      if (s.currentStep) this.currentStep.set(s.currentStep);
      if (Array.isArray(s.steps)) this.steps.set(s.steps);
      if (typeof s.selectedTemplateId === 'string') this.selectedTemplateId.set(s.selectedTemplateId);
      if (typeof s.offerUrl === 'string') this.offerUrl.set(s.offerUrl);
      if (typeof s.offerText === 'string') this.offerText.set(s.offerText);
      if (typeof s.currentOfferId === 'string') this.currentOfferId.set(s.currentOfferId);
      if (s.pipelineResult) this.pipelineResult.set(s.pipelineResult);
      if (typeof s.cvDownloadUrl === 'string') this.cvDownloadUrl.set(s.cvDownloadUrl);
      if (typeof s.isFlowOpen === 'boolean') this.isFlowOpen.set(s.isFlowOpen);
    } catch {}
  }

  private updateStepStatus(index: number, status: PipelineStepStatus): void {
    this.steps.update(steps =>
      steps.map((s, i) => i === index ? { ...s, status } : s)
    );
  }

  private resetSteps(): void {
    this.steps.update(steps => steps.map(s => ({ ...s, status: 'idle' })));
  }
}
