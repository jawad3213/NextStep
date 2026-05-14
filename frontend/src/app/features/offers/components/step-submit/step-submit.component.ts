import { Component, inject, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PipelineStateService } from '../../../../services/pipeline-state.service';
import { OfferApiService } from '../../services/offer-api.service';
import { SignalRService } from '../../../../services/signalr.service';

@Component({
  selector: 'app-step-submit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './step-submit.component.html',
  styleUrl: './step-submit.component.scss'
})
export class StepSubmitComponent implements OnDestroy {
  pipeline = inject(PipelineStateService);
  private offerApiService = inject(OfferApiService);
  private signalR = inject(SignalRService);

  mode: 'url' | 'text' = 'url';
  urlValue  = '';
  textValue = '';
  error     = '';

  get canSubmit(): boolean {
    if (this.pipeline.isLoading()) return false;
    if (this.mode === 'url')  return this.urlValue.trim().length > 10;
    if (this.mode === 'text') return this.textValue.trim().length > 50;
    return false;
  }

  get wordCount(): number {
    const trimmed = this.textValue.trim();
    if (!trimmed) return 0;
    return trimmed.split(/\s+/).length;
  }

  submit(): void {
    this.error = '';
    this.pipeline.pipelineError.set(null);
    this.pipeline.setLoading(true, 'Soumission en cours...');
    this.pipeline.offerUrl.set(this.urlValue);
    this.pipeline.offerText.set(this.textValue);

    const payload = {
      rawText: this.mode === 'text' ? this.textValue : this.urlValue,
      templateId: 1
    };

    this.offerApiService.submitOffer(payload).subscribe({
      next: (response) => {
        this.signalR.connect();
        this.signalR.joinOfferGroup(response.offerId);

        this.pipeline.setLoading(true, 'Pipeline IA en cours...');
        this.pipeline.currentOfferId.set(response.offerId);
        this.pipeline.markStepDone(0);
        this.pipeline.goToStep(2);
      },
      error: (err) => {
        this.pipeline.setLoading(false);
        this.pipeline.pipelineError.set(err.message || 'Erreur de soumission');
        this.error = `Erreur : ${err.message || 'Service indisponible'}`;
      }
    });
  }

  ngOnDestroy(): void {
    this.pipeline.pipelineError.set(null);
  }
}
