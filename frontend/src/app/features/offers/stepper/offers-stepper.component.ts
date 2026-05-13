import { Component, input, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { OfferStepId } from '../offers.types';
import { PipelineStateService } from '../../../services/pipeline-state.service';

@Component({
  selector: 'app-offers-stepper',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './offers-stepper.component.html',
  styleUrl: './offers-stepper.component.scss'
})
export class OffersStepperComponent {
  readonly steps = input.required<{ id: OfferStepId; label: string; icon: string }[]>();
  readonly currentStep = input.required<OfferStepId>();
  readonly stepStates = input.required<Record<OfferStepId, 'idle' | 'active' | 'done' | 'error'>>();

  readonly pipeline = inject(PipelineStateService);

  currentIndex = computed(() => this.steps().findIndex(s => s.id === this.currentStep()));
  isPipelineOpen = input(false);

  stepClick = input<(id: OfferStepId) => void>();

  onStepClick(id: OfferStepId) {
    if (!this.isStepClickable(id)) return;
    const handler = this.stepClick();
    if (handler) handler(id);
  }

  isStepComplete(id: OfferStepId): boolean {
    return this.stepStates()[id] === 'done';
  }

  isStepActive(id: OfferStepId): boolean {
    return this.currentStep() === id;
  }

  isStepClickable(id: OfferStepId): boolean {
    if (this.pipeline.isLoading()) {
      return false;
    }
    const stepsList = this.steps();
    const targetIdx = stepsList.findIndex(s => s.id === id);
    const currentIdx = stepsList.findIndex(s => s.id === this.currentStep());
    const isDone = this.stepStates()[id] === 'done';
    
    return targetIdx <= currentIdx || isDone;
  }
}
