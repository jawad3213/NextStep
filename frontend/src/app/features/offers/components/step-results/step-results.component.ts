import { CommonModule } from '@angular/common';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom, timeout } from 'rxjs';
import { PipelineStateService } from '../../../../services/pipeline-state.service';
import { CandidatureService } from '../../../../services/candidature.service';
import { EmailService } from '../../../../services/email.service';
import {
  CvHistoryItem,
  EmailDraftResponse,
  OfferApiService,
  SendApplicationEmailRequest
} from '../../services/offer-api.service';
import { CandidatureService } from '../../../../services/candidature.service';
import { EmailService } from '../../../../services/email.service';

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
  private readonly candidatureService = inject(CandidatureService);
  private readonly emailService = inject(EmailService);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly candidatureService = inject(CandidatureService);
  private readonly emailService = inject(EmailService);
  private previewBlobUrl: string | null = null;

  cvPreviewUrl: SafeResourceUrl | null = null;
  isLoadingPreview = false;
  previewError: string | null = null;

  recipientEmail = '';
  emailSubject = '';
  emailBody = '';
  isSendingEmail = false;
  isLoadingEmailDraft = false;
  sendError: string | null = null;
  sendSuccessMessage: string | null = null;
  lastSentDraft: EmailDraftResponse | null = null;
  isGeneratingDraft = false;

  get result() {
    return this.pipeline.pipelineResult();
  }

  get attachmentName(): string {
    return `${this.pipeline.finalCvTitle() ?? `CV_${this.pipeline.currentOfferId()}`}.pdf`;
  }

  get canSend(): boolean {
    return !this.isSendingEmail
      && !this.isGeneratingDraft
      && !!this.pipeline.currentOfferId()
      && !!this.recipientEmail.trim()
      && !!this.emailSubject.trim()
      && !!this.emailBody.trim();
  }

  async ngOnInit(): Promise<void> {
    this.hydrateEmailFields();
    await Promise.all([
      this.loadFinalCvPreview(),
      this.hydrateEmailFromBackend()
    ]);
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

      const fileBlob = await firstValueFrom(
        this.api.downloadCvHistoryFile(target.id).pipe(timeout(30000))
      );
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
                  this.isLoadingEmailDraft = false;
                },
                error: () => {
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

  private buildDefaultBody(): string {
    const result = this.result;
    const recruiter = result?.recruiterName?.trim() || 'Bonjour';
    const company = result?.companyName?.trim() || 'votre entreprise';
    const role = result?.offerTitle?.trim() || 'le poste';

    return `${recruiter},\n\nJe vous contacte pour vous transmettre ma candidature pour ${role} chez ${company}. Vous trouverez mon CV en piece jointe.\n\nJe reste a votre disposition pour un echange.\n\nCordialement,`;
  }

  private async hydrateEmailFromBackend(): Promise<void> {
    const offerId = this.pipeline.currentOfferId();
    if (!offerId) {
      return;
    }

    this.isGeneratingDraft = true;
    try {
      const candidatures = await firstValueFrom(
        this.candidatureService.getMyCandidatures().pipe(timeout(15000))
      );
      let linkedCandidature = candidatures.find(
        (c) => c.idOffre?.toLowerCase() === offerId.toLowerCase()
      );
      if (!linkedCandidature) {
        try {
          linkedCandidature = await firstValueFrom(
            this.candidatureService.create({ idOffre: offerId, inclureLettreMotivation: true }).pipe(timeout(10000))
          );
        } catch (createErr) {
          console.warn('[CV-PIPELINE] Failed to create missing candidature', createErr);
          return;
        }
      }

      if (!linkedCandidature) {
        return;
      }

      const generatedDraft = await firstValueFrom(
        this.emailService.generateDraft({
          candidatureId: linkedCandidature.idCandidature,
          emailType: 'application',
          language: 'fr',
          tone: 'professionnel',
        }).pipe(timeout(30000))
      );

      this.recipientEmail = (generatedDraft.recipientEmail ?? this.recipientEmail).trim();
      this.emailSubject = generatedDraft.subject?.trim() || this.emailSubject;
      this.emailBody = generatedDraft.body?.trim() || this.emailBody;

      const result = this.result;
      if (result) {
        this.pipeline.setResult({
          ...result,
          emailSubject: this.emailSubject,
          emailBody: this.emailBody,
        });
      }
    } catch (err) {
      console.warn('[CV-PIPELINE] Email generation from backend failed. Keeping local fallback.', err);
    } finally {
      this.isGeneratingDraft = false;
    }
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

      const fileBlob = await firstValueFrom(
        this.api.downloadCvHistoryFile(target.id).pipe(timeout(30000))
      );
      if (this.previewBlobUrl) {
        window.URL.revokeObjectURL(this.previewBlobUrl);
      }
      this.previewBlobUrl = window.URL.createObjectURL(fileBlob);
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
    const history = await firstValueFrom(this.api.getCvHistory().pipe(timeout(15000)));
    const expectedTitle = this.pipeline.finalCvTitle() ?? `CV_${offerId}`;
    return history
      .filter((item) => item.title === expectedTitle)
      .sort((left, right) => new Date(right.createdAt).getTime() - new Date(left.createdAt).getTime())[0];
  }

  private downloadBlob(blob: Blob, offerId: string): void {
    const blobUrl = window.URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = blobUrl;
    anchor.download = `CV_${offerId}.pdf`;
    document.body.appendChild(anchor);
    anchor.click();
    document.body.removeChild(anchor);
    setTimeout(() => window.URL.revokeObjectURL(blobUrl), 30000);
  }
}
