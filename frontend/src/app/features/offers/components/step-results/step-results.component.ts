import { CommonModule } from '@angular/common';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { PipelineStateService } from '../../../../services/pipeline-state.service';
import {
  CvHistoryItem,
  EmailDraftResponse,
  OfferApiService,
  SendApplicationEmailRequest
} from '../../services/offer-api.service';
import { CandidatureService } from '../../../../services/candidature.service';
import { EmailService } from '../../../../services/email.service';
import { Router } from '@angular/router';

@Component({
  selector: 'app-step-results',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './step-results.component.html',
  styleUrl: './step-results.component.scss'
})
export class StepResultsComponent implements OnInit, OnDestroy {
  readonly pipeline = inject(PipelineStateService);
  private readonly api = inject(OfferApiService);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly candidatureService = inject(CandidatureService);
  private readonly emailService = inject(EmailService);
  private readonly router = inject(Router);
  private previewBlobUrl: string | null = null;

  cvPreviewUrl: SafeResourceUrl | null = null;
  isLoadingPreview = false;
  previewError: string | null = null;

  recipientEmail = '';
  emailSubject = '';
  emailBody = '';
  isSendingEmail = false;
  isLoadingEmailDraft = false;
  isGeneratingDraft = false;
  sendError: string | null = null;
  sendSuccessMessage: string | null = null;
  lastSentDraft: EmailDraftResponse | null = null;

  get result() {
    return this.pipeline.pipelineResult();
  }

  get attachmentName(): string {
    return `${this.pipeline.finalCvTitle() ?? `CV_${this.pipeline.currentOfferId()}`}.pdf`;
  }

  get canSend(): boolean {
    return !this.isSendingEmail
      && !!this.pipeline.currentOfferId()
      && !!this.recipientEmail.trim()
      && !!this.emailSubject.trim()
      && !!this.emailBody.trim();
  }

  async ngOnInit(): Promise<void> {
    this.hydrateEmailFields();
    await this.loadFinalCvPreview();
  }

  ngOnDestroy(): void {
    if (this.previewBlobUrl) {
      window.URL.revokeObjectURL(this.previewBlobUrl);
    }
  }

  async downloadCv(): Promise<void> {
    const historyId = this.pipeline.finalCvHistoryId();
    const offerId = this.pipeline.currentOfferId();

    if (!offerId) {
      this.previewError = 'Aucune offre active pour telecharger le CV.';
      return;
    }

    try {
      const target = historyId
        ? { id: historyId }
        : await this.findLatestCvForOffer(offerId);

      if (!target?.id) {
        throw new Error('Le CV final n a pas encore ete sauvegarde.');
      }

      const fileBlob = await firstValueFrom(this.api.downloadCvHistoryFile(target.id));
      this.downloadBlob(fileBlob, offerId);
    } catch (err: any) {
      this.previewError = err?.message || 'Telechargement indisponible pour le moment.';
    }
  }

  async sendEmail(): Promise<void> {
    const offerId = this.pipeline.currentOfferId();
    if (!offerId || !this.canSend) {
      return;
    }

    this.isSendingEmail = true;
    this.sendError = null;
    this.sendSuccessMessage = null;

    const payload: SendApplicationEmailRequest = {
      offerId,
      cvHistoryId: this.pipeline.finalCvHistoryId(),
      recipientEmail: this.recipientEmail.trim(),
      subject: this.emailSubject.trim(),
      body: this.emailBody.trim(),
      emailType: 'application',
      language: 'fr',
    };

    try {
      const sent = await firstValueFrom(this.api.sendApplicationEmail(payload));
      this.lastSentDraft = sent;
      this.sendSuccessMessage = `Email envoye a ${sent.recipientEmail} avec le CV en piece jointe.`;
      this.pipeline.markStepDone(4);
      this.pipeline.showSidebarBadge('email', 'Envoye', 'green');

      const result = this.result;
      if (result) {
        this.pipeline.setResult({
          ...result,
          emailSubject: this.emailSubject,
          emailBody: this.emailBody,
        });
      }
    } catch (err: any) {
      this.sendError = err?.error?.error ?? err?.error?.message ?? err?.message ?? 'Envoi de l email impossible.';
    } finally {
      this.isSendingEmail = false;
    }
  }

  copyEmailBody(): void {
    void navigator.clipboard.writeText(this.emailBody || '');
  }

  backToEditor(): void {
    this.pipeline.goToStep(4);
  }

  finish(): void {
    this.pipeline.closeFlow();
  }

  private hydrateEmailFields(): void {
    const result = this.result;
    this.recipientEmail = this.lastSentDraft?.recipientEmail ?? '';
    this.emailSubject = `Application - ${result?.offerTitle || 'Poste'}`;
    this.emailBody = this.buildDefaultBody();
    this.sendError = null;

    const offerId = this.pipeline.currentOfferId();
    if (!offerId) return;

    this.isLoadingEmailDraft = true;
    this.candidatureService.getMyCandidatures().subscribe({
      next: (candidatures) => {
        const candidature = candidatures.find(c => c.idOffre === offerId);
        if (!candidature) {
          this.isLoadingEmailDraft = false;
          return;
        }

        this.emailService.getDraftsByCandidature(candidature.idCandidature).subscribe({
          next: (drafts) => {
            const appDraft = drafts.find(d => d.emailType === 'application');
            if (appDraft) {
              this.recipientEmail = appDraft.recipientEmail ?? '';
              this.emailSubject = appDraft.subject ?? '';
              this.emailBody = appDraft.body ?? '';
              this.isLoadingEmailDraft = false;
            } else {
              this.isGeneratingDraft = true;
              this.emailService.generateDraft({
                candidatureId: candidature.idCandidature,
                emailType: 'application',
                language: 'fr',
                cvHistoryId: this.pipeline.finalCvHistoryId()
              }).subscribe({
                next: (newDraft) => {
                  this.recipientEmail = newDraft.recipientEmail ?? '';
                  this.emailSubject = newDraft.subject ?? '';
                  this.emailBody = newDraft.body ?? '';
                  this.isGeneratingDraft = false;
                  this.isLoadingEmailDraft = false;
                },
                error: (err) => {
                  this.sendError = this.extractApiErrorMessage(err, 'Generation IA de l email indisponible.');
                  this.isGeneratingDraft = false;
                  this.isLoadingEmailDraft = false;
                }
              });
            }
          },
          error: () => {
            this.isLoadingEmailDraft = false;
          }
        });
      },
      error: () => {
        this.isLoadingEmailDraft = false;
      }
    });
  }

  private extractApiErrorMessage(err: any, fallback: string): string {
    const payload = err?.error;
    if (typeof payload === 'string' && payload.trim().length > 0) {
      return payload;
    }
    return payload?.error ?? payload?.message ?? err?.message ?? fallback;
  }

  private buildDefaultBody(): string {
    const result = this.result;
    const recruiter = result?.recruiterName?.trim() || 'Bonjour';
    const company = result?.companyName?.trim() || 'votre entreprise';
    const role = result?.offerTitle?.trim() || 'le poste';

    return `${recruiter},\n\nJe vous contacte pour vous transmettre ma candidature pour ${role} chez ${company}. Vous trouverez mon CV en piece jointe.\n\nJe reste a votre disposition pour un echange.\n\nCordialement,`;
  }

  private async loadFinalCvPreview(): Promise<void> {
    const offerId = this.pipeline.currentOfferId();
    if (!offerId) {
      return;
    }

    this.isLoadingPreview = true;
    this.previewError = null;
    try {
      const targetId = this.pipeline.finalCvHistoryId();
      const target = targetId ? { id: targetId } : await this.findLatestCvForOffer(offerId);
      if (!target?.id) {
        this.previewError = 'PDF final pas encore sauvegarde.';
        return;
      }

      const fileBlob = await firstValueFrom(this.api.downloadCvHistoryFile(target.id));
      if (this.previewBlobUrl) {
        globalThis.URL.revokeObjectURL(this.previewBlobUrl);
      }
      this.previewBlobUrl = globalThis.URL.createObjectURL(fileBlob);
      this.cvPreviewUrl = this.sanitizer.bypassSecurityTrustResourceUrl(
        `${this.previewBlobUrl}#toolbar=0&navpanes=0&scrollbar=0&view=FitH`
      );
    } catch (err: any) {
      this.previewError = err?.message || 'Apercu PDF indisponible.';
    } finally {
      this.isLoadingPreview = false;
    }
  }

  private async findLatestCvForOffer(offerId: string): Promise<CvHistoryItem | undefined> {
    const history = await firstValueFrom(this.api.getCvHistory());
    const expectedTitle = this.pipeline.finalCvTitle() ?? `CV_${offerId}`;
    return history
      .filter((item) => item.title === expectedTitle)
      .sort((left, right) => new Date(right.createdAt).getTime() - new Date(left.createdAt).getTime())[0];
  }

  private downloadBlob(blob: Blob, offerId: string): void {
    const blobUrl = globalThis.URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = blobUrl;
    anchor.download = `CV_${offerId}.pdf`;
    document.body.appendChild(anchor);
    anchor.click();
    document.body.removeChild(anchor);
    setTimeout(() => globalThis.URL.revokeObjectURL(blobUrl), 30000);
  }

  startInterview(): void {
    const offerId = this.pipeline.currentOfferId();
    const result = this.result;
    if (!offerId || !result) return;
    const config = {
      offer_id: offerId,
      job_title: result.offerTitle || 'Offre',
      company: result.companyName || 'Entreprise',
      domain: 'software',
      level: 'senior',
      duration_minutes: 20,
      language: 'fr',
      focus_areas: result.requiredSkills || []
    };
    this.pipeline.closeFlow();
    this.router.navigate(['/chatbot'], {
      state: {
        preselectedMode: 'offer',
        offerConfig: config
      }
    });
  }
}

