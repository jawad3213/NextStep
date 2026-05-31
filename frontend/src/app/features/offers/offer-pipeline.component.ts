import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { PipelineStateService, PipelineStep } from '../../services/pipeline-state.service';
import { StepAnalysisComponent } from './components/step-analysis/step-analysis.component';
import { StepGenerationComponent } from './components/step-generation/step-generation.component';
import { StepResultsComponent } from './components/step-results/step-results.component';
import { StepSubmitComponent } from './components/step-submit/step-submit.component';
import { StepTemplateComponent } from './components/step-template/step-template.component';
import { OfferApiService } from './services/offer-api.service';
import { OfferStepId } from './offers.types';
import { timeout } from 'rxjs';

@Component({
  selector: 'app-offer-pipeline',
  standalone: true,
  imports: [
    CommonModule,
    StepSubmitComponent,
    StepAnalysisComponent,
    StepTemplateComponent,
    StepGenerationComponent,
    StepResultsComponent,
  ],
  templateUrl: './offer-pipeline.component.html',
  styleUrl: './offer-pipeline.component.scss'
})
export class OfferPipelineComponent implements OnInit, OnDestroy {
  readonly pipeline = inject(PipelineStateService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly offerApi = inject(OfferApiService);

  readonly steps: { id: OfferStepId; label: string; icon: string }[] = [
    { id: 'submit', label: 'Offre', icon: 'description' },
    { id: 'analysis', label: 'Skill Gap', icon: 'analytics' },
    { id: 'template', label: 'Template', icon: 'palette' },
    { id: 'generation', label: 'CV Final', icon: 'edit_note' },
    { id: 'results', label: 'Email & Send', icon: 'send' },
  ];

  private readonly stepToPipeline: Record<OfferStepId, PipelineStep> = {
    submit: 1,
    analysis: 2,
    template: 3,
    generation: 4,
    results: 5,
  };

  private readonly pipelineToStep: Record<PipelineStep, OfferStepId> = {
    1: 'submit',
    2: 'analysis',
    3: 'template',
    4: 'generation',
    5: 'results',
  };

  readonly currentStepId = computed<OfferStepId>(() =>
    this.pipelineToStep[this.pipeline.currentStep()] || 'submit'
  );

  readonly stepStates = computed<Record<OfferStepId, 'idle' | 'active' | 'done' | 'error'>>(() => {
    const states: Record<OfferStepId, 'idle' | 'active' | 'done' | 'error'> = {
      submit: 'idle',
      analysis: 'idle',
      template: 'idle',
      generation: 'idle',
      results: 'idle',
    };

    const pipelineSteps = this.pipeline.steps();
    for (const [offerStep, pipelineStep] of Object.entries(this.stepToPipeline)) {
      const ps = pipelineSteps[pipelineStep - 1];
      states[offerStep as OfferStepId] = ps ? ps.status : 'idle';
    }

    return states;
  });

  ngOnInit(): void {
    const forceNew = this.route.snapshot.queryParamMap.get('new') === '1';
    const autoAnalyze = this.route.snapshot.queryParamMap.get('autoAnalyze') === '1';
    if (forceNew) {
      this.pipeline.resetAll();
      this.pipeline.openFlow();
      return;
    }

    if (!this.pipeline.isFlowOpen()) {
      this.pipeline.openFlow();
    }

    const queryOfferId = this.route.snapshot.queryParamMap.get('offerId');
    const requestedStep = this.route.snapshot.queryParamMap.get('step') as OfferStepId | null;
    const currentOfferId = this.pipeline.currentOfferId();
    const offerId = queryOfferId || currentOfferId;
    if (!offerId) {
      return;
    }

    if (queryOfferId && currentOfferId && queryOfferId !== currentOfferId) {
      this.pipeline.pipelineResult.set(null);
      this.pipeline.currentOfferId.set(queryOfferId);
      this.pipeline.goToStep(1);
    } else {
      this.pipeline.currentOfferId.set(offerId);
    }

    if (autoAnalyze) {
      this.runAutoAnalyze(offerId, requestedStep);
      return;
    }

    if (this.pipeline.pipelineResult() && !queryOfferId) {
      return;
    }

    this.restoreAnalysis(offerId, requestedStep);
  }

  ngOnDestroy(): void {
    this.pipeline.closeFlow();
  }

  goToStep(id: OfferStepId): void {
    if (this.pipeline.isLoading()) {
      return;
    }

    const pipelineStep = this.stepToPipeline[id];
    if (!pipelineStep) {
      return;
    }

    const targetIdx = pipelineStep - 1;
    const isDone = this.pipeline.steps()[targetIdx]?.status === 'done';

    if (pipelineStep <= this.pipeline.currentStep() || isDone) {
      this.pipeline.goToStep(pipelineStep);
    }
  }

  private applyRequestedStep(step: OfferStepId | null): void {
    if (!step || !(step in this.stepToPipeline)) {
      return;
    }

    const target = this.stepToPipeline[step];
    for (let index = 0; index < target - 1; index++) {
      this.pipeline.markStepDone(index);
    }
    this.pipeline.goToStep(target);
  }

  private runAutoAnalyze(offerId: string, requestedStep: OfferStepId | null): void {
    this.pipeline.pipelineResult.set(null);
    this.pipeline.pipelineError.set(null);
    this.pipeline.currentOfferId.set(offerId);
    this.pipeline.steps.set([
      { status: 'done', label: 'Offre' },
      { status: 'active', label: 'Skill Gap' },
      { status: 'idle', label: 'Template' },
      { status: 'idle', label: 'CV final' },
      { status: 'idle', label: 'Email & envoi' },
    ]);
    this.pipeline.goToStep(2);
    this.pipeline.setLoading(true, 'Analyse de l offre en cours...');
    const analysisStartTs = Date.now();
    const minAnalysisUxMs = 6500;
    const progressStages = [
      { agentName: 'offer_analyzer', label: 'Analyse de la description du poste...', progressPercent: 18 },
      { agentName: 'offer_analyzer', label: 'Extraction des competences et mots-cles...', progressPercent: 38 },
      { agentName: 'profile_retriever', label: 'Chargement de votre profil...', progressPercent: 62 },
      { agentName: 'skill_gap', label: 'Analyse du skill gap...', progressPercent: 84 },
      { agentName: 'skill_gap', label: 'Finalisation des resultats...', progressPercent: 95 },
    ] as const;
    const timers: Array<ReturnType<typeof setTimeout>> = [];

    const setStage = (stageIndex: number) => {
      const stage = progressStages[Math.max(0, Math.min(stageIndex, progressStages.length - 1))];
      this.pipeline.currentAgentProgress.set({
        step: 'analysis',
        agentName: stage.agentName,
        label: stage.label,
        status: 'running',
        progressPercent: stage.progressPercent,
      });
    };

    setStage(0);
    timers.push(setTimeout(() => setStage(1), 1200));
    timers.push(setTimeout(() => setStage(2), 2500));
    timers.push(setTimeout(() => setStage(3), 3900));
    timers.push(setTimeout(() => setStage(4), 5200));

    this.offerApi.analyzeSync(offerId, 1).subscribe({
      next: () => {
        const elapsedMs = Date.now() - analysisStartTs;
        const waitMs = Math.max(0, minAnalysisUxMs - elapsedMs);
        setTimeout(() => {
          timers.forEach((timer) => clearTimeout(timer));
          this.clearAutoAnalyzeQueryFlag();
          this.restoreAnalysis(offerId, requestedStep, true);
        }, waitMs);
      },
      error: (err) => {
        timers.forEach((timer) => clearTimeout(timer));
        const message = err?.error?.error || err?.message || 'Erreur lors de l analyse de l offre.';
        this.pipeline.pipelineError.set(message);
        this.clearAutoAnalyzeQueryFlag();
        this.pipeline.setLoading(false);
        this.pipeline.currentAgentProgress.set(null);
      },
    });
  }

  private restoreAnalysis(offerId: string, requestedStep: OfferStepId | null, keepLoader = false): void {
    this.pipeline.setLoading(true, keepLoader ? 'Restauration des resultats...' : 'Restauration de votre analyse...');
    this.offerApi.getAnalysis(offerId).pipe(
      timeout(15000)
    ).subscribe({
      next: (analysis) => {
        this.pipeline.hydrateFromAnalysis(offerId, analysis);
        this.applyRequestedStep(requestedStep);
        this.pipeline.setLoading(false);
        this.pipeline.currentAgentProgress.set(null);
      },
      error: () => {
        this.applyRequestedStep(requestedStep);
        this.pipeline.setLoading(false);
        this.pipeline.currentAgentProgress.set(null);
      }
    });
  }

  private clearAutoAnalyzeQueryFlag(): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { autoAnalyze: null },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }
}
