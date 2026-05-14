import { Component, inject, computed, OnInit, OnDestroy, effect } from '@angular/core';
import { Router, ActivatedRoute } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { OffersService, JobOffer } from './offers.service';
import { PipelineStateService, PipelineStep } from '../../services/pipeline-state.service';
import { OfferStepId } from './offers.types';
import { OffersStepperComponent } from './stepper/offers-stepper.component';
import { StepSubmitComponent } from './components/step-submit/step-submit.component';
import { StepAnalysisComponent } from './components/step-analysis/step-analysis.component';
import { StepTemplateComponent } from './components/step-template/step-template.component';
import { StepGenerationComponent } from './components/step-generation/step-generation.component';
import { StepResultsComponent } from './components/step-results/step-results.component';

@Component({
  selector: 'app-offers',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatIconModule,
    OffersStepperComponent,
    StepSubmitComponent,
    StepAnalysisComponent,
    StepTemplateComponent,
    StepGenerationComponent,
    StepResultsComponent
  ],
  templateUrl: './offers.component.html',
  styleUrls: ['./offers.component.scss']
})
export class OffersComponent implements OnInit, OnDestroy {
  readonly offersService = inject(OffersService);
  readonly pipeline = inject(PipelineStateService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private destroy$ = new Subject<void>();

  steps: { id: OfferStepId; label: string; icon: string }[] = [
    { id: 'submit', label: 'Offre', icon: 'description' },
    { id: 'analysis', label: 'Skill Gap', icon: 'analytics' },
    { id: 'template', label: 'Template', icon: 'palette' },
    { id: 'generation', label: 'Generation', icon: 'auto_awesome' },
    { id: 'results', label: 'Resultats', icon: 'verified' }
  ];

  stepToPipelineMap: Record<OfferStepId, PipelineStep> = {
    submit: 1,
    analysis: 2,
    template: 3,
    generation: 4,
    results: 5
  };

  pipelineToStepMap: Record<PipelineStep, OfferStepId> = {
    1: 'submit',
    2: 'analysis',
    3: 'template',
    4: 'generation',
    5: 'results'
  };

  currentOfferStepId = computed<OfferStepId>(() => {
    return this.pipelineToStepMap[this.pipeline.currentStep()] || 'submit';
  });

  offerStepStates = computed<Record<OfferStepId, 'idle' | 'active' | 'done' | 'error'>>(() => {
    const states: any = {};
    const pipelineSteps = this.pipeline.steps();
    for (const [offerStep, pipelineStep] of Object.entries(this.stepToPipelineMap)) {
      const ps = pipelineSteps[pipelineStep - 1];
      states[offerStep] = ps ? ps.status : 'idle';
    }
    return states;
  });

  // Search & filter panel states
  readonly searchTerm = this.offersService.searchTerm;
  readonly filterContract = this.offersService.filterContract;
  readonly filterStatus = this.offersService.filterStatus;
  readonly viewMode = this.offersService.viewMode;
  readonly isSyncing = this.offersService.isSyncing;

  // Reactive filtered offers selector
  readonly filteredOffers = computed(() => {
    const search = this.searchTerm().trim().toLowerCase();
    const contract = this.filterContract();
    const status = this.filterStatus();

    return this.offersService.offers().filter(offer => {
      const matchesSearch = !search || 
        offer.title.toLowerCase().includes(search) ||
        offer.company.toLowerCase().includes(search) ||
        offer.location.toLowerCase().includes(search) ||
        offer.tags.some(t => t.toLowerCase().includes(search));

      const matchesContract = !contract || offer.contractType === contract;
      const matchesStatus = !status || offer.status === status;

      return matchesSearch && matchesContract && matchesStatus;
    });
  });

  currentIndex = computed(() => this.steps.findIndex(s => s.id === this.currentOfferStepId()));

  nextStepName = computed(() => {
    const nextIdx = this.currentIndex() + 1;
    return nextIdx < this.steps.length ? this.steps[nextIdx].label : 'Terminer';
  });

  // Auto-clean URL when pipeline closes from anywhere (e.g., step-submit cancel)
  private pipelineWatcher = effect(() => {
    if (!this.pipeline.isFlowOpen()) {
      this.router.navigate([], {
        relativeTo: this.route,
        queryParams: { flow: null, step: null },
        queryParamsHandling: 'merge'
      });
    }
  });

  ngOnInit() {
    this.route.queryParamMap.pipe(takeUntil(this.destroy$)).subscribe(params => {
      const flow = params.get('flow');
      const stepParam = params.get('step');
      if (flow === 'pipeline') {
        if (!this.pipeline.isFlowOpen()) {
          this.pipeline.openFlow();
        }
        if (stepParam) {
          const pipelineStep = Number(stepParam) as PipelineStep;
          if (pipelineStep >= 1 && pipelineStep <= 5) {
            this.pipeline.goToStep(pipelineStep);
          }
        }
      }
    });
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  syncUrl() {
    const step = this.pipeline.currentStep();
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { flow: 'pipeline', step },
      queryParamsHandling: 'merge'
    });
  }

  // SETTERS & STATE TRANSITIONS
  setViewMode(mode: 'list' | 'grid') {
    this.offersService.viewMode.set(mode);
  }

  // SYNC ACTION ANIMATION
  triggerGlobalSync() {
    this.offersService.isSyncing.set(true);
    setTimeout(() => {
      this.offersService.isSyncing.set(false);
    }, 1500);
  }

  // STEP NAVIGATION
  goToOfferStep(id: OfferStepId) {
    if (this.pipeline.isLoading()) {
      return;
    }

    const pipelineStep = this.stepToPipelineMap[id];
    if (!pipelineStep) return;

    const currentStepNumber = this.pipeline.currentStep();
    const targetStepIndex = pipelineStep - 1;
    const isStepDone = this.pipeline.steps()[targetStepIndex]?.status === 'done';

    // Allow clicking if it is a previous/current step, or if the step is already marked as done
    if (pipelineStep <= currentStepNumber || isStepDone) {
      this.pipeline.goToStep(pipelineStep);
      this.syncUrl();
    }
  }

  next() {
    const nextIdx = this.currentIndex() + 1;
    if (nextIdx < this.steps.length) {
      this.goToOfferStep(this.steps[nextIdx].id);
    }
  }

  prev() {
    const prevIdx = this.currentIndex() - 1;
    if (prevIdx >= 0) {
      this.goToOfferStep(this.steps[prevIdx].id);
    }
  }

  // PIPELINE LAUNCH
  openPipeline() {
    this.pipeline.openFlow();
    this.syncUrl();
  }

  closePipeline() {
    this.pipeline.closeFlow();
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { flow: null, step: null },
      queryParamsHandling: 'merge'
    });
  }

  // AVATAR BUBBLE BG GRADIENTS
  getBubbleGradient(company: string): string {
    const colors = [
      'linear-gradient(135deg, #1A91F0 0%, #0C1986 100%)',
      'linear-gradient(135deg, #00B0FF 0%, #00B0FF 100%)',
      'linear-gradient(135deg, #00E676 0%, #00A250 100%)',
      'linear-gradient(135deg, #FF9100 0%, #FF6D00 100%)',
      'linear-gradient(135deg, #651FFF 0%, #4615B2 100%)',
      'linear-gradient(135deg, #D500F9 0%, #9C00AF 100%)',
      'linear-gradient(135deg, #37474F 0%, #212121 100%)'
    ];
    let sum = 0;
    for (let i = 0; i < company.length; i++) sum += company.charCodeAt(i);
    return colors[sum % colors.length];
  }

  getInitials(company: string): string {
    if (!company) return 'CO';
    const parts = company.trim().split(' ');
    if (parts.length > 1) {
      return (parts[0][0] + parts[1][0]).toUpperCase();
    }
    return company.substring(0, 2).toUpperCase();
  }
}
