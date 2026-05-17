import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { firstValueFrom } from 'rxjs';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { PipelineStateService } from '../../../../services/pipeline-state.service';
import { CvHistoryItem, OfferApiService } from '../../services/offer-api.service';

@Component({
  selector: 'app-step-results',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './step-results.component.html',
  styleUrl: './step-results.component.scss'
})
export class StepResultsComponent implements OnInit, OnDestroy {
  pipeline = inject(PipelineStateService);
  private api = inject(OfferApiService);
  private sanitizer = inject(DomSanitizer);
  private previewBlobUrl: string | null = null;

  cvPreviewUrl: SafeResourceUrl | null = null;
  isLoadingPreview = false;
  previewError: string | null = null;

  get result() { return this.pipeline.pipelineResult(); }

  async ngOnInit(): Promise<void> {
    await this.loadFinalCvPreview();
  }

  ngOnDestroy(): void {
    if (this.previewBlobUrl) {
      window.URL.revokeObjectURL(this.previewBlobUrl);
    }
  }

  async downloadCv(): Promise<void> {
    const offerId = this.pipeline.currentOfferId();
    if (!offerId) {
      alert('Telechargement disponible dans la section CV Builder.');
      return;
    }

    try {
      let target = await this.findLatestCvForOffer(offerId);

      if (!target?.id) {
        const template = this.pipeline.selectedTemplateId() || 'modern';
        await firstValueFrom(this.api.generatePdf(offerId, template));
        target = await this.findLatestCvForOffer(offerId);
      }

      if (!target?.id) throw new Error('CV history not found');

      try {
        const fileBlob = await firstValueFrom(this.api.downloadCvHistoryFile(target.id));
        this.downloadBlob(fileBlob, offerId);
      } catch {
        const signed = await firstValueFrom(this.api.getCvDownloadUrl(target.id));
        if (!signed?.downloadUrl) throw new Error('Signed url missing');
        const fixedUrl = signed.downloadUrl
          .replace('http://minio:9000', 'http://localhost:9000')
          .replace('https://minio:9000', 'http://localhost:9000');
        window.location.href = fixedUrl;
      }
    } catch {
      alert('Telechargement indisponible pour le moment.');
    }
  }

  async loadFinalCvPreview(): Promise<void> {
    const offerId = this.pipeline.currentOfferId();
    if (!offerId) return;

    this.isLoadingPreview = true;
    this.previewError = null;
    try {
      const target = await this.findLatestCvForOffer(offerId);
      if (!target?.id) {
        this.previewError = 'PDF final pas encore sauvegarde.';
        return;
      }

      const fileBlob = await firstValueFrom(this.api.downloadCvHistoryFile(target.id));
      if (this.previewBlobUrl) {
        window.URL.revokeObjectURL(this.previewBlobUrl);
      }
      this.previewBlobUrl = window.URL.createObjectURL(fileBlob);
      this.cvPreviewUrl = this.sanitizer.bypassSecurityTrustResourceUrl(
        `${this.previewBlobUrl}#toolbar=0&navpanes=0&scrollbar=0`
      );
    } catch (err: any) {
      this.previewError = err?.message || 'Apercu PDF indisponible.';
    } finally {
      this.isLoadingPreview = false;
    }
  }

  private async findLatestCvForOffer(offerId: string): Promise<CvHistoryItem | undefined> {
    const history = await firstValueFrom(this.api.getCvHistory());
    return history
      .filter((h) => h.title === `CV_${offerId}`)
      .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())[0];
  }

  private downloadBlob(blob: Blob, offerId: string): void {
    const blobUrl = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = blobUrl;
    a.download = `CV_${offerId}.pdf`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    setTimeout(() => window.URL.revokeObjectURL(blobUrl), 30000);
  }

  finish(): void {
    this.pipeline.closeFlow();
  }
}
