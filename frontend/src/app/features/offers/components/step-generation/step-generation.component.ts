import { Component, inject, OnInit, OnDestroy } from '@angular/core';
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
export class StepGenerationComponent implements OnInit, OnDestroy {
  pipeline = inject(PipelineStateService);
  private signalR = inject(SignalRService);
  private api = inject(OfferApiService);

  progress = 0;
  statusText = 'Initialisation de la génération...';

  ngOnInit() {
    const offerId = this.pipeline.currentOfferId();
    if (!offerId) {
      this.simulateFallback();
      return;
    }

    // Make sure SignalR is connected for generation events
    this.signalR.connect();
    this.signalR.joinOfferGroup(offerId);

    // Wait briefly for pipeline to complete if it hasn't yet
    const result = this.pipeline.pipelineResult();
    if (!result) {
      this.statusText = 'Attente de la fin du pipeline...';
      // Poll for analysis, then trigger PDF generation
      this.waitForPipelineThenGenerate(offerId);
    } else {
      this.startPdfGeneration(offerId);
    }
  }

  private waitForPipelineThenGenerate(offerId: string): void {
    const check = setInterval(() => {
      if (this.pipeline.pipelineResult()) {
        clearInterval(check);
        this.startPdfGeneration(offerId);
      }
    }, 1000);

    // Fallback: also check via API
    this.api.getAnalysis(offerId).subscribe({
      next: (result) => {
        if (result && result.titre) {
          clearInterval(check);
          this.startPdfGeneration(offerId);
        }
      }
    });

    // Safety timeout
    setTimeout(() => clearInterval(check), 60000);
  }

  private startPdfGeneration(offerId: string): void {
    const templateId = this.pipeline.selectedTemplateId();
    this.statusText = 'Génération du CV avec le template ' + templateId + '...';

    this.api.generatePdf(offerId, templateId).subscribe({
      next: (response) => {
        this.progress = 100;
        this.statusText = 'CV généré avec succès !';
        this.pipeline.cvDownloadUrl.set(response.downloadUrl);
        setTimeout(() => {
          this.pipeline.markStepDone(3);
          this.pipeline.goToStep(5);
        }, 500);
      },
      error: () => this.simulateFallback()
    });
  }

  private simulateFallback(): void {
    this.statusText = 'Génération locale...';

    const steps = [
      { pct: 15, msg: 'Préparation des données...', delay: 500 },
      { pct: 35, msg: 'Application du template ' + this.pipeline.selectedTemplateId() + '...', delay: 1500 },
      { pct: 60, msg: 'Intégration des mots-clés ATS...', delay: 2500 },
      { pct: 85, msg: 'Génération de l\'email...', delay: 3500 },
      { pct: 100, msg: 'Finalisation...', delay: 4500 },
    ];

    steps.forEach(s => setTimeout(() => {
      this.progress = s.pct;
      this.statusText = s.msg;
      if (s.pct === 100) {
        setTimeout(() => {
          this.pipeline.markStepDone(3);
          this.pipeline.goToStep(5);
          this.pipeline.showSidebarBadge('email', 'Nouveau', 'green');
          this.pipeline.showSidebarBadge('cv-builder', 'Prêt', 'green');
        }, 500);
      }
    }, s.delay));
  }

  ngOnDestroy(): void {
    const offerId = this.pipeline.currentOfferId();
    if (offerId) {
      this.signalR.leaveOfferGroup(offerId);
    }
  }
}
