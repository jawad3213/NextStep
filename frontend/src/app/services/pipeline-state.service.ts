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
}

@Injectable({ providedIn: 'root' })
export class PipelineStateService {

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

  // ── Actions ─────────────────────────────────────────────────────────────

  openFlow(): void {
    this.isFlowOpen.set(true);
    this.currentStep.set(1);
    this.updateStepStatus(0, 'active');
  }

  closeFlow(): void {
    this.isFlowOpen.set(false);
    this.resetSteps();
  }

  goToStep(step: PipelineStep): void {
    this.currentStep.set(step);
  }

  markStepDone(stepIndex: number): void {
    this.updateStepStatus(stepIndex, 'done');
  }

  setLoading(loading: boolean, message = ''): void {
    this.isLoading.set(loading);
    this.loadingMessage.set(message);
  }

  setResult(result: PipelineResult): void {
    this.pipelineResult.set(result);
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
