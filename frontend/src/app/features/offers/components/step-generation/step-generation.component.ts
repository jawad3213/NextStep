import { Component, inject, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PipelineStateService } from '../../../../services/pipeline-state.service';
import { SignalRService } from '../../../../services/signalr.service';
import { OfferApiService } from '../../services/offer-api.service';

@Component({
  selector: 'app-step-generation',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './step-generation.component.html',
  styleUrl: './step-generation.component.scss'
})
export class StepGenerationComponent {
  pipeline = inject(PipelineStateService);
  private signalR = inject(SignalRService);
  private api = inject(OfferApiService);

  constructor() {
    effect(() => {
      const isLoading = this.pipeline.isLoading();
      const result = this.pipeline.pipelineResult();
      const error = this.pipeline.pipelineError();

      if (!isLoading && (result || error)) {
        this.isGenerating = false;
        if (result && !error) {
          this.pipeline.markStepDone(3);
          this.pipeline.goToStep(5);
        }
      }
    });
  }

  // Use computed or local state driven by pipeline signal
  get currentProgress() { return this.pipeline.currentAgentProgress(); }
  get error() { return this.pipeline.pipelineError(); }

  isGenerating = false;

  get selectedTemplate(): string {
    return this.pipeline.selectedTemplateId();
  }

  startGeneration(): void {
    const offerId = this.pipeline.currentOfferId();
    if (!offerId) return;

    this.isGenerating = true;
    this.pipeline.pipelineError.set(null);

    // 1. Ensure SignalR is listening
    this.signalR.connect();
    this.signalR.joinOfferGroup(offerId);

    // 2. Call the resume endpoint
    const templateId = 1; // Logic for template mapping if needed
    this.api.resumePipeline(offerId, templateId).subscribe({
      next: () => {
        // The server will send progress via SignalR
        // We just stay in "isGenerating" state
      },
      error: (err) => {
        this.isGenerating = false;
        this.pipeline.pipelineError.set("Erreur lors du lancement de la génération.");
      }
    });

    // 3. Track completion is handled by the effect in constructor
  }

  goBack(): void {
    this.pipeline.goToStep(3);
  }
}
