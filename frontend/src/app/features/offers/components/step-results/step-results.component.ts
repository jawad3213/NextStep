import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PipelineStateService } from '../../../../services/pipeline-state.service';
import { OfferApiService } from '../../services/offer-api.service';

@Component({
  selector: 'app-step-results',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './step-results.component.html',
  styleUrl: './step-results.component.scss'
})
export class StepResultsComponent {
  pipeline = inject(PipelineStateService);
  private api = inject(OfferApiService);

  get result() { return this.pipeline.pipelineResult(); }
  get downloadUrl() { return this.pipeline.cvDownloadUrl(); }

  downloadCv(): void {
    const url = this.downloadUrl;
    if (url) {
      window.open(url, '_blank');
      return;
    }

    const offerId = this.pipeline.currentOfferId();
    if (offerId) {
      this.api.downloadCv(offerId).subscribe({
        next: (blob) => {
          const blobUrl = window.URL.createObjectURL(blob);
          const a = document.createElement('a');
          a.href = blobUrl;
          a.download = `CV_NextStep_${this.result?.companyName || 'candidature'}.pdf`;
          a.click();
          window.URL.revokeObjectURL(blobUrl);
        },
        error: () => alert('Téléchargement disponible dans la section CV Builder.')
      });
    } else {
      alert('Téléchargement disponible dans la section CV Builder.');
    }
  }

  finish(): void {
    this.pipeline.closeFlow();
  }
}
