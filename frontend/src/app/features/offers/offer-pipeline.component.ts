import { Component, computed, inject, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { PipelineStateService, PipelineStep } from '../../services/pipeline-state.service';
import { OfferStepId } from './offers.types';
import { OfferApiService } from './services/offer-api.service';
import { OffersStepperComponent } from './stepper/offers-stepper.component';
import { StepSubmitComponent } from './components/step-submit/step-submit.component';
import { StepAnalysisComponent } from './components/step-analysis/step-analysis.component';
import { StepTemplateComponent } from './components/step-template/step-template.component';
import { StepGenerationComponent } from './components/step-generation/step-generation.component';
import { StepResultsComponent } from './components/step-results/step-results.component';

@Component({
  selector: 'app-offer-pipeline',
  standalone: true,
  imports: [
    CommonModule,
    OffersStepperComponent,
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
    { id: 'generation', label: 'Edition CV', icon: 'edit_note' },
    { id: 'results', label: 'Résultats', icon: 'verified' },
  ];

  private readonly stepToPipeline: Record<OfferStepId, PipelineStep> = {
    submit: 1, analysis: 2, template: 3, generation: 4, results: 5,
  };

  private readonly pipelineToStep: Record<PipelineStep, OfferStepId> = {
    1: 'submit', 2: 'analysis', 3: 'template', 4: 'generation', 5: 'results',
  };

  readonly currentStepId = computed<OfferStepId>(() =>
    this.pipelineToStep[this.pipeline.currentStep()] || 'submit'
  );

  readonly stepStates = computed<Record<OfferStepId, 'idle' | 'active' | 'done' | 'error'>>(() => {
    const states: any = {};
    const pipelineSteps = this.pipeline.steps();
    for (const [offerStep, pipelineStep] of Object.entries(this.stepToPipeline)) {
      const ps = pipelineSteps[pipelineStep - 1];
      states[offerStep] = ps ? ps.status : 'idle';
    }
    return states;
  });

  ngOnInit(): void {
    const forceNew = this.route.snapshot.queryParamMap.get('new') === '1';
    if (forceNew) {
      this.pipeline.resetAll();
      this.pipeline.openFlow();
      return;
    }

    if (!this.pipeline.isFlowOpen()) {
      this.pipeline.openFlow();
    }

    const queryOfferId = this.route.snapshot.queryParamMap.get('offerId');
    const currentOfferId = this.pipeline.currentOfferId();
    const offerId = queryOfferId || currentOfferId;
    if (!offerId) return;

    // If user clicked "Continuer" on a different offer, clear stale in-memory data.
    if (queryOfferId && currentOfferId && queryOfferId !== currentOfferId) {
      this.pipeline.pipelineResult.set(null);
      this.pipeline.currentOfferId.set(queryOfferId);
      this.pipeline.goToStep(1);
    } else {
      this.pipeline.currentOfferId.set(offerId);
    }
    if (this.pipeline.pipelineResult() && !queryOfferId) return;

    this.pipeline.setLoading(true, 'Restauration de votre analyse...');
    this.offerApi.getAnalysis(offerId).subscribe({
      next: (analysis) => {
        this.pipeline.hydrateFromAnalysis(offerId, analysis);
        this.pipeline.setLoading(false);
      },
      error: () => {
        this.pipeline.setLoading(false);
      }
    });
  }

  ngOnDestroy(): void {
    this.pipeline.closeFlow();
  }

  goToStep(id: OfferStepId): void {
    if (this.pipeline.isLoading()) return;
    const pipelineStep = this.stepToPipeline[id];
    if (!pipelineStep) return;

    const targetIdx = pipelineStep - 1;
    const isDone = this.pipeline.steps()[targetIdx]?.status === 'done';

    if (pipelineStep <= this.pipeline.currentStep() || isDone) {
      this.pipeline.goToStep(pipelineStep);
    }
  }

  backToList(): void {
    this.router.navigate(['/offers']);
  }
}
