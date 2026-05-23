import { Component, input, output, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { OfferStepId } from '../offers.types';
import { PipelineStateService } from '../../../services/pipeline-state.service';

@Component({
  selector: 'app-offers-stepper',
  standalone: true,
  imports: [CommonModule, MatIconModule],
  templateUrl: './offers-stepper.component.html',
  styleUrl: './offers-stepper.component.scss'
})
export class OffersStepperComponent {
  readonly steps = input.required<{ id: OfferStepId; label: string; icon: string }[]>();
  readonly currentStep = input.required<OfferStepId>();
  readonly stepStates = input.required<Record<OfferStepId, 'idle' | 'active' | 'done' | 'error'>>();

  readonly pipeline = inject(PipelineStateService);

  readonly currentIndex = computed(() => this.steps().findIndex(s => s.id === this.currentStep()));
  readonly stepClick = output<OfferStepId>();

  onStepClick(id: OfferStepId) {
    if (!this.isStepClickable(id)) return;
    this.stepClick.emit(id);
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
