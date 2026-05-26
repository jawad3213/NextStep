import { Injectable, computed, signal } from '@angular/core';
import { GenerationProgress } from './signalr.service';

export type PipelineStep = 1 | 2 | 3 | 4 | 5;

export type PipelineStepStatus = 'idle' | 'active' | 'done' | 'error';

export interface StepState {
  status: PipelineStepStatus;
  label: string;
}

export interface SidebarBadge {
  page: string;
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
  offerTitle: string;
  companyName: string;
  contractType: string;
  location?: string;
  requiredSkills: string[];
  preferredSkills: string[];
  experienceYears?: number;
  educationLevel?: string;
  modeTravail?: string;
  descriptionPoste?: string;
  originalRawText?: string;
  matchScore: number;
  atsScore: number;
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
  companyCultureScore: number;
  companySalaryMin: number;
  companySalaryMax: number;
  companySize: string;
  companyNews: { title: string; date: string }[];
  profileStrengths: string[];
  skillGaps: { skill: string; priority: string; weeks: number }[];
  cvPdfPath: string;
  atsImprovements: string[];
  emailSubject: string;
  emailBody: string;
  recruiterName: string;
  coverLetterContent: string;
  skillDetails?: SkillDetail[];
  recommendationsWithPriority?: RecommendationPriorisee[];
  keywordWeights?: KeywordPondere[];
  profileStrengthsList?: string[];
  profileData?: any;
  cvGeneratedContent?: any;
}

@Injectable({ providedIn: 'root' })
export class PipelineStateService {
  private readonly STORAGE_KEY = 'nextstep.offer.pipeline.v1';

  readonly currentStep = signal<PipelineStep>(1);
  readonly isFlowOpen = signal<boolean>(false);
  readonly isLoading = signal<boolean>(false);
  readonly loadingMessage = signal<string>('');

  readonly steps = signal<StepState[]>([
    { status: 'idle', label: 'Offre' },
    { status: 'idle', label: 'Skill Gap' },
    { status: 'idle', label: 'Template' },
    { status: 'idle', label: 'CV final' },
    { status: 'idle', label: 'Email & envoi' },
  ]);

  readonly selectedTemplateId = signal<string>('modern');
  readonly offerUrl = signal<string>('');
  readonly offerText = signal<string>('');
  readonly currentOfferId = signal<string | null>(null);

  readonly pipelineResult = signal<PipelineResult | null>(null);

  readonly currentAgentProgress = signal<{
    step: string;
    agentName: string;
    label: string;
    status: 'running' | 'done' | 'todo';
    progressPercent: number;
  } | null>(null);

  readonly pipelineError = signal<string | null>(null);
  readonly generationProgress = signal<GenerationProgress | null>(null);
  readonly cvDownloadUrl = signal<string | null>(null);
  readonly finalCvHistoryId = signal<string | null>(null);
  readonly finalCvTitle = signal<string | null>(null);

  readonly sidebarBadges = signal<SidebarBadge[]>([
    { page: 'cv-builder', label: '', variant: 'green', visible: false },
    { page: 'email', label: 'Genere', variant: 'amber', visible: false },
    { page: 'company-intel', label: 'Pret', variant: 'green', visible: false },
    { page: 'skill-gap', label: '0 gaps', variant: 'red', visible: false },
    { page: 'notifications', label: '0', variant: 'red', visible: false },
  ]);

  readonly canGoNext = computed(() => {
    if (this.isLoading()) {
      return false;
    }

    return this.currentStep() !== 1 || !!this.offerUrl() || !!this.offerText();
  });

  readonly progressPercent = computed(() =>
    Math.round(((this.currentStep() - 1) / 4) * 100)
  );

  constructor() {
    this.restoreFromStorage();
  }

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

  showSidebarBadge(page: string, label: string, variant: 'green' | 'blue' | 'red' | 'amber'): void {
    this.sidebarBadges.update((badges) =>
      badges.map((badge) =>
        badge.page === page ? { ...badge, label, variant, visible: true } : badge
      )
    );
  }

  hideSidebarBadge(page: string): void {
    this.sidebarBadges.update((badges) =>
      badges.map((badge) => badge.page === page ? { ...badge, visible: false } : badge)
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
    this.finalCvHistoryId.set(null);
    this.finalCvTitle.set(null);
    this.pipelineError.set(null);
    this.resetSteps();
    this.sidebarBadges.update((badges) => badges.map((badge) => ({ ...badge, visible: false })));
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
      modeTravail: dto.modeTravail ?? undefined,
      descriptionPoste: dto.descriptionPoste ?? '',
      originalRawText: dto.texteBrut ?? '',
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
      profileData: dto.profileData ?? dto.profile_data ?? undefined,
      cvGeneratedContent: dto.cvGeneratedContent ?? dto.cv_data ?? dto.cvData ?? undefined,
    });

    if (dto.texteBrut) {
      this.offerText.set(dto.texteBrut);
    } else if (dto.descriptionPoste) {
      this.offerText.set(dto.descriptionPoste);
    }

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
        finalCvHistoryId: this.finalCvHistoryId(),
        finalCvTitle: this.finalCvTitle(),
      }));
    } catch {}
  }

  private restoreFromStorage(): void {
    try {
      const raw = localStorage.getItem(this.STORAGE_KEY);
      if (!raw) {
        return;
      }

      const saved = JSON.parse(raw);
      if (saved.currentStep) this.currentStep.set(saved.currentStep);
      if (Array.isArray(saved.steps)) this.steps.set(saved.steps);
      if (typeof saved.selectedTemplateId === 'string') this.selectedTemplateId.set(saved.selectedTemplateId);
      if (typeof saved.offerUrl === 'string') this.offerUrl.set(saved.offerUrl);
      if (typeof saved.offerText === 'string') this.offerText.set(saved.offerText);
      if (typeof saved.currentOfferId === 'string') this.currentOfferId.set(saved.currentOfferId);
      if (saved.pipelineResult) this.pipelineResult.set(saved.pipelineResult);
      if (typeof saved.cvDownloadUrl === 'string') this.cvDownloadUrl.set(saved.cvDownloadUrl);
      if (typeof saved.finalCvHistoryId === 'string') this.finalCvHistoryId.set(saved.finalCvHistoryId);
      if (typeof saved.finalCvTitle === 'string') this.finalCvTitle.set(saved.finalCvTitle);
      if (typeof saved.isFlowOpen === 'boolean') this.isFlowOpen.set(saved.isFlowOpen);
    } catch {}
  }

  private updateStepStatus(index: number, status: PipelineStepStatus): void {
    this.steps.update((steps) =>
      steps.map((step, i) => i === index ? { ...step, status } : step)
    );
  }

  private resetSteps(): void {
    this.steps.update((steps) => steps.map((step) => ({ ...step, status: 'idle' })));
  }
}
