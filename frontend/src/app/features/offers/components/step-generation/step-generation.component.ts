import { Component, OnDestroy, OnInit, effect, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { Subscription, firstValueFrom } from 'rxjs';
import { PipelineStateService } from '../../../../services/pipeline-state.service';
import { ResumeEditorComponent } from '../resume-editor/resume-editor.component';
import {
  CvDesignConfig,
  CvRenderResponse,
  CvSaveResponse,
  OfferApiService
} from '../../services/offer-api.service';
import { environment } from '../../../../../environments/environment';
import { SignalRService } from '../../../../services/signalr.service';
import { ProfileService } from '../../../profile/profile.service';

interface RealCvTemplate {
  slug: string;
  label: string;
  tone: string;
}

interface CvEditorDraft {
  cvData: any;
  designConfig: CvDesignConfig;
  htmlSnapshot?: string | null;
}

@Component({
  selector: 'app-step-generation',
  standalone: true,
  imports: [CommonModule, ResumeEditorComponent],
  templateUrl: './step-generation.component.html',
  styleUrls: ['./step-generation.component.scss']
})
export class StepGenerationComponent implements OnInit, OnDestroy {
  pipeline = inject(PipelineStateService);
  private readonly offerApi = inject(OfferApiService);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly signalR = inject(SignalRService);
  private readonly profileService = inject(ProfileService);
  private previewDebounceId: number | null = null;
  private autosaveDebounceId: number | null = null;
  private previewRequestSub: Subscription | null = null;
  private autosaveSub: Subscription | null = null;
  private loadDraftSub: Subscription | null = null;
  private lastDraftHash = '';
  private signedProfilePhotoUrl: string | null = null;
  private readonly thumbnailUrlCache = new Map<string, string>();
  private readonly thumbnailNonce = Date.now();
  private readonly draftKey = 'nextstep_cv_draft';
  private readonly draftOfferKey = 'nextstep_cv_draft_offer_id';
  private hasUserEditedDraft = false;

  editorInitialData = signal<any | null>(null);
  private editorDraft = signal<any | null>(null);
  private readonly designConfigState = signal<CvDesignConfig>(this.defaultDesignConfig('modern'));
  renderedHtml: SafeHtml | null = null;
  private renderedHtmlSnapshot: string | null = null;
  isRenderingPreview = false;
  previewError: string | null = null;
  draftStatus: 'idle' | 'local' | 'saving' | 'saved' | 'error' = 'idle';
  draftSavedAt: string | null = null;
  draftVersion: number | null = null;
  draftError: string | null = null;
  savedHistoryId: string | null = null;
  savedFileUrl: string | null = null;
  isSavingFinal = false;
  isPreparingDownload = false;
  isGeneratingHighQualityPdf = false;
  isReoptimizingCv = false;
  private savedDraftHash = '';
  private readonly activitySignals = [
    'hackathon', 'club', 'association', 'organisateur', 'organizer',
    'membre', 'member', 'volunteer', 'benevole', 'benevole', 'event',
    'community', 'communaut', 'it day', 'prize', 'prix', 'participant',
    'formateur', 'trainer', 'formation', 'solihackathon', 'itwave', 'ids'
  ];

  readonly realTemplates: RealCvTemplate[] = [
    { slug: 'latex', label: 'LaTeX Tech', tone: 'Classic Engineering' },
    { slug: 'modern', label: 'Modern', tone: 'Tech Minimal' },
  ];

  readonly generationStages = [
    { key: 'profile_context', label: 'Contexte candidat' },
    { key: 'analysis_context', label: 'Analyse consolidee' },
    { key: 'cv_optimizer', label: 'CV optimizer' },
    { key: 'cv_engine', label: 'CV engine' },
    { key: 'db_persist', label: 'Finalisation' },
  ] as const;

  private readonly templateMap: Record<string, string> = {
    latex: 'latex',
    modern: 'modern',
  };

  private readonly generatedCvSync = effect(() => {
    const generated = this.generatedCvData;
    if (!this.hasRenderableCvData(generated)) return;
    if (!generated || this.hasUserEditedDraft || this.editorDraft()) return;
    this.applyInitialDraft({ cvData: generated, designConfig: this.defaultDesignConfig(this.selectedTemplate) }, 'idle');
  });

  get selectedTemplate(): string {
    const id = this.pipeline.selectedTemplateId();
    return this.templateMap[id] || 'modern';
  }

  get generatedCvData(): any {
    return this.pipeline.pipelineResult()?.cvGeneratedContent ?? null;
  }

  get currentOfferId(): string | null {
    return this.pipeline.currentOfferId();
  }

  get generationAgentProgress() {
    return this.pipeline.currentAgentProgress();
  }

  get generationProgressLabel(): string {
    return this.generationAgentProgress?.label || 'Generation du CV en cours...';
  }

  get generationProgressPercent(): number {
    return this.generationAgentProgress?.progressPercent ?? 5;
  }

  get isAwaitingGeneratedCv(): boolean {
    return !this.editorInitialData() && !this.generatedCvData;
  }

  get currentDesignConfig(): CvDesignConfig {
    return this.designConfigState();
  }

  private hasRenderableCvData(data: any | null | undefined): boolean {
    if (!data || typeof data !== 'object') return false;
    const normalized = this.unwrapCvPayload(data);
    const candidate = this.getCandidateSource(normalized);
    const hasIdentity = !!this.cleanText(candidate?.name)
      || !!this.cleanText(candidate?.nomComplet)
      || !!this.cleanText(candidate?.fullName)
      || !!this.cleanText(candidate?.email)
      || !!this.cleanText(normalized?.summary)
      || !!this.cleanText(normalized?.resume)
      || !!this.cleanText(normalized?.resumeProfessionnel);
    const hasSections = this.firstArray(normalized, ['experience', 'experiences', 'experiences_optimisees']).length > 0
      || this.firstArray(normalized, ['education', 'formations', 'formations_optimisees']).length > 0
      || this.firstArray(normalized, ['skills', 'competences', 'competences_reordonnees', 'competences_mises_en_avant']).length > 0
      || this.firstArray(normalized, ['projects', 'projets', 'projets_optimises']).length > 0;
    return hasIdentity || hasSections;
  }

  ngOnInit(): void {
    const offerId = this.currentOfferId;
    if (offerId) {
      void this.signalR.joinOfferGroup(offerId).catch((err) => {
        console.warn('[CV-PIPELINE] SignalR join failed from generation step', err);
      });
    }
    void this.prefetchSignedProfilePhoto();
    this.hydrateInitialDraft();
  }

  private async prefetchSignedProfilePhoto(): Promise<void> {
    try {
      this.signedProfilePhotoUrl = await this.profileService.getSignedProfilePhotoUrl();
      if (this.signedProfilePhotoUrl && this.currentCvData()) {
        this.queueLivePreview(this.currentCvData(), 0);
      }
    } catch (error) {
      console.warn('[CV-PIPELINE] Failed to prefetch signed profile photo URL', error);
      this.signedProfilePhotoUrl = null;
    }
  }

  ngOnDestroy(): void {
    if (this.previewDebounceId !== null) window.clearTimeout(this.previewDebounceId);
    if (this.autosaveDebounceId !== null) window.clearTimeout(this.autosaveDebounceId);
    this.previewRequestSub?.unsubscribe();
    this.autosaveSub?.unsubscribe();
    this.loadDraftSub?.unsubscribe();
  }

  async continueToResults(): Promise<void> {
    try {
      await this.saveFinalCv();
      this.pipeline.markStepDone(3);
      this.pipeline.goToStep(5);
    } catch (err: any) {
      this.pipeline.pipelineError.set(err?.error?.message || err?.message || 'Impossible de sauvegarder le CV final.');
    }
  }

  goBack(): void {
    this.pipeline.goToStep(3);
  }

  async reoptimizeCv(): Promise<void> {
    const offerId = this.currentOfferId;
    if (!offerId || this.isReoptimizingCv) return;

    const templateId = this.agentTemplateId(this.selectedTemplate);
    this.pipeline.pipelineError.set(null);
    this.isReoptimizingCv = true;
    this.pipeline.setLoading(true, 'Re-analyse du CV optimise en cours...');
    this.pipeline.currentAgentProgress.set({
      step: 'generating_cv',
      agentName: 'cv_optimizer',
      label: 'Nouvelle optimisation du CV...',
      status: 'running',
      progressPercent: 8
    });

    try {
      await this.signalR.joinOfferGroup(offerId);
    } catch (err) {
      console.warn('[CV-PIPELINE] SignalR join failed for re-optimization, polling only', err);
    }

    const currentResult = this.pipeline.pipelineResult();
    const localProfile = currentResult?.profileData ?? null;

    this.offerApi.resumePipeline(offerId, templateId).subscribe({
      next: (res: any) => {
        const current = this.pipeline.pipelineResult();
        if (!current) {
          this.finishReoptimization();
          return;
        }

        const profileForFallback = res?.profileData
          ?? res?.profile_data
          ?? current.profileData
          ?? localProfile;

        const generatedCv = res?.cvGeneratedContent
          ?? res?.cv_data
          ?? res?.cvData
          ?? res?.cv_optimized_content
          ?? res?.cvOptimizedContent;

        if (generatedCv) {
          this.pipeline.setResult({
            ...current,
            profileData: profileForFallback ?? current.profileData,
            cvGeneratedContent: generatedCv,
            emailSubject: res?.email_subject ?? current.emailSubject ?? '',
            emailBody: res?.email_body ?? current.emailBody ?? '',
            recruiterName: res?.recruiter_name ?? current.recruiterName ?? '',
          });

          this.hasUserEditedDraft = false;
          this.savedHistoryId = null;
          this.savedFileUrl = null;
          this.savedDraftHash = '';
          this.lastDraftHash = '';
          this.draftStatus = 'idle';
          this.draftError = null;

          const nextDraft: CvEditorDraft = {
            cvData: generatedCv,
            designConfig: this.currentDesignConfig,
            htmlSnapshot: null
          };
          this.applyInitialDraft(nextDraft, 'idle');
        }

        this.pipeline.currentAgentProgress.set({
          step: 'generating_cv',
          agentName: 'db_persist',
          label: 'CV re-optimise et recharge dans l editeur.',
          status: 'done',
          progressPercent: 100
        });
        this.finishReoptimization();
      },
      error: (err) => {
        console.error('[CV-PIPELINE] Re-optimization failed', err);
        this.pipeline.pipelineError.set(err?.error?.message || err?.message || 'Echec de la re-optimisation du CV.');
        this.pipeline.currentAgentProgress.set(null);
        this.finishReoptimization();
      }
    });
  }

  selectTemplate(slug: string): void {
    if (slug === this.selectedTemplate) return;
    this.pipeline.selectedTemplateId.set(slug);
    this.designConfigState.set(this.defaultDesignConfig(slug));
    this.savedHistoryId = null;
    this.savedFileUrl = null;
    this.savedDraftHash = '';
    this.lastDraftHash = '';
    this.queueLivePreview();
    this.queueBackendAutosave();
  }

  getGenerationStageStatus(stageKey: string): 'done' | 'running' | 'todo' {
    const current = this.generationAgentProgress?.agentName;
    if (!current) return stageKey === 'cv_optimizer' ? 'running' : 'todo';

    const order = this.generationStages.map((stage) => stage.key);
    const currentIndex = order.indexOf(current as typeof order[number]);
    const targetIndex = order.indexOf(stageKey as typeof order[number]);

    if (currentIndex === -1) {
      return stageKey === 'cv_optimizer' ? 'running' : 'todo';
    }
    if (targetIndex < currentIndex) return 'done';
    if (targetIndex === currentIndex) return 'running';
    return 'todo';
  }

  templateThumbnailUrl(slug: string): string {
    const cached = this.thumbnailUrlCache.get(slug);
    if (cached) return cached;

    const origin = new URL(environment.apiBaseUrl).origin;
    const url = `${origin}/api/cv/templates/${encodeURIComponent(slug)}/thumbnail?v=${this.thumbnailNonce}`;
    this.thumbnailUrlCache.set(slug, url);
    return url;
  }

  onEditorDataChange(rawData: any): void {
    const data = this.normalizeCvForBackend(rawData);
    const previous = this.editorDraft();
    const previousHash = previous ? this.buildDraftHash(previous, this.currentDesignConfig) : '';
    const nextHash = this.buildDraftHash(data, this.currentDesignConfig);

    if (previousHash && previousHash === nextHash) {
      this.writeLocalDraft(data, this.currentDesignConfig);
      return;
    }

    if (previousHash && previousHash !== nextHash) {
      this.hasUserEditedDraft = true;
    }

    this.editorDraft.set(data);
    this.writeLocalDraft(data, this.currentDesignConfig);
    this.draftStatus = 'local';
    this.draftError = null;
    this.queueLivePreview(data);
    this.queueBackendAutosave(data);
  }

  onDesignConfigChange(config: CvDesignConfig): void {
    this.designConfigState.set({ ...config });
    this.writeLocalDraft(this.currentCvData(), config);
    this.draftStatus = 'local';
    this.draftError = null;
    this.queueLivePreview();
    this.queueBackendAutosave();
  }

  async saveFinalCv(): Promise<CvSaveResponse> {
    const offerId = this.currentOfferId;
    const data = this.currentCvData();
    if (!offerId || !data) {
      throw new Error('CV introuvable pour la sauvegarde finale.');
    }

    this.isSavingFinal = true;
    this.pipeline.setLoading(true, 'Sauvegarde du PDF final...');
    try {
      const saved = await firstValueFrom(
        this.offerApi.saveFinalCv(
          this.selectedTemplate,
          `CV_${offerId}`,
          data,
          this.currentDesignConfig,
          this.renderedHtmlSnapshot
        )
      );
      this.savedHistoryId = saved?.historyId ?? null;
      this.savedFileUrl = saved?.fileUrl ?? null;
      this.savedDraftHash = this.buildDraftHash(data, this.currentDesignConfig);
      this.pipeline.cvDownloadUrl.set(saved?.fileUrl ?? null);
      return saved;
    } catch (err) {
      console.error('[CV-PIPELINE] Final CV save failed', err);
      throw err;
    } finally {
      this.isSavingFinal = false;
      this.pipeline.setLoading(false);
    }
  }

  async downloadFinalPdf(): Promise<void> {
    try {
      await this.downloadFastPdf();
    } catch (err: any) {
      console.warn('[CV-PIPELINE] Frontend download failed, falling back to backend PDF export', err);
      this.pipeline.pipelineError.set('Fast download is unavailable right now. Switching to high-quality PDF export.');
      await this.downloadHighQualityPdf();
    }
  }

  async downloadHighQualityPdf(): Promise<void> {
    try {
      const data = this.currentCvData();
      if (!data) throw new Error('CV introuvable pour le telechargement.');

      this.isGeneratingHighQualityPdf = true;
      const blob = await firstValueFrom(this.offerApi.exportCvPdf({
        templateSlug: this.selectedTemplate,
        data,
        designConfig: this.currentDesignConfig,
        htmlSnapshot: this.renderedHtmlSnapshot
      }));

      if (!blob || blob.size === 0) {
        throw new Error('PDF vide recu depuis le backend.');
      }
      this.downloadBlob(blob);
    } catch (err: any) {
      console.error('[CV-PIPELINE] Final CV download failed', err);
      this.pipeline.pipelineError.set(err?.error?.message || err?.message || 'Telechargement PDF indisponible.');
    } finally {
      this.isGeneratingHighQualityPdf = false;
    }
  }

  private queueLivePreview(data: any | null = this.currentCvData(), delayMs = 320): void {
    if (!this.hasRenderableCvData(data)) return;

    const hash = this.buildDraftHash(data, this.currentDesignConfig);
    if (this.savedDraftHash && this.savedDraftHash !== hash) {
      this.savedHistoryId = null;
      this.savedFileUrl = null;
      this.savedDraftHash = '';
    }

    if (this.previewDebounceId !== null) {
      window.clearTimeout(this.previewDebounceId);
    }

    this.previewDebounceId = window.setTimeout(() => {
      this.previewDebounceId = null;
      this.renderLivePreview(data);
    }, delayMs);
  }

  private renderLivePreview(data: any): void {
    const hash = this.buildDraftHash(data, this.currentDesignConfig);
    if (hash === this.lastDraftHash && !this.previewError) {
      this.isRenderingPreview = false;
      return;
    }

    this.previewRequestSub?.unsubscribe();
    this.lastDraftHash = hash;
    this.isRenderingPreview = true;
    this.previewError = null;

    this.previewRequestSub = this.offerApi.renderCvPreview({
      templateSlug: this.selectedTemplate,
      data,
      designConfig: this.currentDesignConfig
    }).subscribe({
      next: (response) => {
        this.applyRenderedPreview(response);
        this.previewError = null;
        this.isRenderingPreview = false;
      },
      error: async (err) => {
        console.error('[CV-PIPELINE] Live HTML preview failed', err);
        this.lastDraftHash = '';
        this.previewError = await this.extractHttpErrorMessage(err, 'Apercu HTML indisponible.');
        this.isRenderingPreview = false;
      }
    });
  }

  private applyRenderedPreview(response: CvRenderResponse): void {
    this.designConfigState.set({ ...response.designConfig });
    this.renderedHtmlSnapshot = response.html;
    this.renderedHtml = this.sanitizer.bypassSecurityTrustHtml(response.html);
  }

  private buildDraftHash(data: any, designConfig: CvDesignConfig): string {
    return JSON.stringify({ t: this.selectedTemplate, data, designConfig });
  }

  private downloadBlob(blob: Blob): void {
    const offerId = this.currentOfferId || 'candidat';
    const blobUrl = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = blobUrl;
    a.download = `CV_${offerId}_${this.selectedTemplate}.pdf`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    setTimeout(() => window.URL.revokeObjectURL(blobUrl), 30000);
  }

  private async downloadFastPdf(): Promise<void> {
    const htmlSnapshot = this.renderedHtmlSnapshot?.trim();
    if (!htmlSnapshot) {
      throw new Error('Rendered HTML preview is not ready yet.');
    }

    const offerId = this.currentOfferId || 'candidat';
    const title = `CV_${offerId}_${this.selectedTemplate}`;
    const printWindow = window.open('', '_blank', 'noopener,noreferrer,width=1200,height=900');
    if (!printWindow) {
      throw new Error('Popup blocked by browser.');
    }

    this.isPreparingDownload = true;
    this.pipeline.pipelineError.set(null);

    const printMarkup = `<!DOCTYPE html>
<html>
<head>
  <meta charset="utf-8" />
  <title>${this.escapeHtml(title)}</title>
  <style>
    @page { size: A4; margin: 0; }
    html, body { margin: 0; padding: 0; background: #e5e7eb; }
    body { -webkit-print-color-adjust: exact; print-color-adjust: exact; }
    @media print {
      html, body { background: #ffffff; }
    }
  </style>
</head>
<body>
${htmlSnapshot}
</body>
</html>`;

    await new Promise<void>((resolve, reject) => {
      let settled = false;
      const finish = (callback: () => void) => {
        if (settled) return;
        settled = true;
        callback();
      };

      const timeoutId = window.setTimeout(() => {
        finish(() => reject(new Error('Print window timed out before rendering.')));
      }, 8000);

      const triggerPrint = () => {
        window.clearTimeout(timeoutId);
        printWindow.focus();
        window.setTimeout(() => {
          try {
            printWindow.print();
            window.setTimeout(() => {
              try {
                printWindow.close();
              } catch {}
              finish(resolve);
            }, 250);
          } catch (error) {
            finish(() => reject(error instanceof Error ? error : new Error('Unable to print fast PDF.')));
          }
        }, 150);
      };

      printWindow.document.open();
      printWindow.document.write(printMarkup);
      printWindow.document.close();

      if (printWindow.document.readyState === 'complete') {
        triggerPrint();
        return;
      }

      printWindow.onload = () => triggerPrint();
    }).finally(() => {
      this.isPreparingDownload = false;
    });
  }

  private escapeHtml(value: string): string {
    return value
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#39;');
  }

  private formatDraftTime(value: string | null | undefined): string | null {
    if (!value) return null;
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return null;
    return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }

  private safeParseJson(raw: string | null): any | null {
    if (!raw) return null;
    try {
      return JSON.parse(raw);
    } catch {
      return null;
    }
  }

  private hydrateInitialDraft(): void {
    const localDraft = this.readLocalDraftForCurrentOffer();
    const validLocalDraft = localDraft && this.hasRenderableCvData(localDraft.cvData) ? localDraft : null;
    const validGenerated = this.hasRenderableCvData(this.generatedCvData) ? this.generatedCvData : null;
    const fallback = validLocalDraft ?? (validGenerated
      ? { cvData: validGenerated, designConfig: this.defaultDesignConfig(this.selectedTemplate) }
      : null);

    if (fallback) {
      this.applyInitialDraft(fallback, validLocalDraft ? 'local' : 'idle');
    }

    if (!this.currentOfferId) return;

    this.loadDraftSub = this.offerApi.getCvDraft(this.currentOfferId).subscribe({
      next: (draft) => {
        if (!draft?.data || this.hasUserEditedDraft) return;
        const parsedDraft = this.asEditorDraft(draft.data);
        if (parsedDraft === null) return;
        if (!this.hasRenderableCvData(parsedDraft.cvData)) return;

        this.applyInitialDraft(parsedDraft, 'saved');
        this.draftVersion = draft.version ?? null;
        this.draftSavedAt = this.formatDraftTime(draft.updatedAtUtc);
      },
      error: (err) => {
        if (err?.status === 404) return;
        console.warn('[CV-PIPELINE] Backend draft load failed', err);
        this.draftError = 'Brouillon backend indisponible, copie locale conservee.';
        this.draftStatus = this.editorDraft() ? 'local' : 'error';
      }
    });
  }

  private applyInitialDraft(rawDraft: CvEditorDraft, status: 'idle' | 'local' | 'saved'): void {
    if (this.autosaveDebounceId !== null) {
      window.clearTimeout(this.autosaveDebounceId);
      this.autosaveDebounceId = null;
    }

    const data = this.normalizeCvForBackend(rawDraft.cvData);
    if (!this.hasRenderableCvData(data)) return;
    this.editorInitialData.set(data);
    this.editorDraft.set(data);
    this.designConfigState.set({ ...this.defaultDesignConfig(this.selectedTemplate), ...rawDraft.designConfig });
    this.renderedHtmlSnapshot = rawDraft.htmlSnapshot ?? null;
    this.writeLocalDraft(data, this.currentDesignConfig);
    this.draftStatus = status;
    this.draftError = null;
    this.queueLivePreview(data, 0);
  }

  private readLocalDraftForCurrentOffer(): CvEditorDraft | null {
    if (!this.currentOfferId) return null;
    const draftOfferId = localStorage.getItem(this.draftOfferKey);
    if (draftOfferId !== this.currentOfferId) return null;
    return this.asEditorDraft(this.safeParseJson(localStorage.getItem(this.draftKey)));
  }

  private writeLocalDraft(data: any | null, designConfig: CvDesignConfig): void {
    if (!data) return;
    try {
      const draft: CvEditorDraft = {
        cvData: data,
        designConfig,
        htmlSnapshot: this.renderedHtmlSnapshot
      };
      localStorage.setItem(this.draftKey, JSON.stringify(draft));
      if (this.currentOfferId) {
        localStorage.setItem(this.draftOfferKey, this.currentOfferId);
      }
    } catch (err) {
      console.warn('[CV-PIPELINE] Local draft persistence failed', err);
    }
  }

  private queueBackendAutosave(data: any | null = this.currentCvData(), delayMs = 900): void {
    if (!this.currentOfferId || !this.hasRenderableCvData(data)) return;
    if (this.autosaveDebounceId !== null) {
      window.clearTimeout(this.autosaveDebounceId);
    }

    this.autosaveDebounceId = window.setTimeout(() => {
      this.autosaveDebounceId = null;
      this.persistBackendDraft(data);
    }, delayMs);
  }

  private persistBackendDraft(data: any): void {
    const offerId = this.currentOfferId;
    if (!offerId) return;

    this.autosaveSub?.unsubscribe();
    this.draftStatus = 'saving';
    this.draftError = null;

    const payload: CvEditorDraft = {
      cvData: data,
      designConfig: this.currentDesignConfig,
      htmlSnapshot: this.renderedHtmlSnapshot
    };

    this.autosaveSub = this.offerApi.saveCvDraft(offerId, payload).subscribe({
      next: (draft) => {
        this.draftStatus = 'saved';
        this.draftVersion = draft?.version ?? this.draftVersion;
        this.draftSavedAt = this.formatDraftTime(draft?.updatedAtUtc);
        this.draftError = null;
      },
      error: (err) => {
        console.warn('[CV-PIPELINE] Backend draft save failed', err);
        this.draftStatus = 'error';
        this.draftError = 'Autosave backend en attente. Le brouillon local est conserve.';
      }
    });
  }

  private currentCvData(): any | null {
    const data = this.editorDraft()?.candidate ? this.editorDraft() : this.readLocalDraftForCurrentOffer()?.cvData || this.generatedCvData;
    return data ? this.normalizeCvForBackend(data) : null;
  }

  private defaultDesignConfig(templateSlug: string): CvDesignConfig {
    return templateSlug === 'latex'
      ? {
          themeColor: '#111827',
          fontFamily: "'IBM Plex Sans', 'Segoe UI', Arial, sans-serif",
          fontSize: '13px',
          lineSpacing: '1.38',
          sectionSpacing: '1rem',
          sidebarWidth: '0%'
        }
      : {
          themeColor: '#2d3a8c',
          fontFamily: "Inter, 'Segoe UI', Arial, sans-serif",
          fontSize: '14px',
          lineSpacing: '1.45',
          sectionSpacing: '1.2rem',
          sidebarWidth: '31%'
        };
  }

  private asEditorDraft(value: any): CvEditorDraft | null {
    if (!value || typeof value !== 'object') return null;
    if (value.cvData && typeof value.cvData === 'object') {
      return {
        cvData: value.cvData,
        designConfig: { ...this.defaultDesignConfig(this.selectedTemplate), ...(value.designConfig ?? {}) },
        htmlSnapshot: typeof value.htmlSnapshot === 'string' ? value.htmlSnapshot : null
      };
    }

    return {
      cvData: value,
      designConfig: this.defaultDesignConfig(this.selectedTemplate),
      htmlSnapshot: null
    };
  }

  private async extractHttpErrorMessage(err: any, fallback: string): Promise<string> {
    const payload = err?.error;
    if (typeof payload === 'string' && payload.trim()) return payload;
    if (payload?.message) return String(payload.message);
    if (payload?.error) return String(payload.error);

    if (payload instanceof Blob) {
      try {
        const text = await payload.text();
        if (!text) return err?.message || fallback;
        try {
          const parsed = JSON.parse(text);
          return parsed?.error || parsed?.message || text;
        } catch {
          return text;
        }
      } catch {
        return err?.message || fallback;
      }
    }

    return err?.message || fallback;
  }

  private normalizeCvForBackend(data: any): any {
    data = this.unwrapCvPayload(data);
    const { fontFamily, sections, ...rawWithoutFontFamily } = data ?? {};
    void fontFamily;
    void sections;
    const pipelineResult: any = this.pipeline.pipelineResult();
    const profileSource = pipelineResult?.profileData?.data
      ?? pipelineResult?.profileData?.profile
      ?? pipelineResult?.profileData
      ?? {};
    const profilePersonal = profileSource?.personalInfo
      ?? profileSource?.personal_info
      ?? profileSource?.personal
      ?? {};
    const liveProfilePersonal = this.profileService.profile()?.personal ?? {};
    const candidate = this.getCandidateSource(data);
    const activities = this.normalizeActivities(this.firstArray(data, ['activities', 'extracurricular', 'activites', 'activitÃ©s']));
    const highlightedSkills = new Set(
      this.firstArray(data, ['competences_mises_en_avant'])
        .map((skill: any) => this.normalizeKey(typeof skill === 'string' ? skill : skill?.name ?? skill?.nom ?? skill?.label))
        .filter(Boolean)
    );
    const experience = this.firstArray(data, ['experience', 'experiences', 'experiences_optimisees'])
      .map((exp: any) => ({
        role: this.cleanText(exp?.role ?? exp?.title ?? exp?.poste ?? exp?.titre),
        company: this.cleanText(exp?.company ?? exp?.entreprise),
        start: this.toMonthValue(exp?.start ?? exp?.dateDebut ?? exp?.date_debut),
        end: this.toMonthValue(exp?.end ?? exp?.dateFin ?? exp?.date_fin),
        bullets: this.dedupeStrings(this.asStringArray(exp?.bullets ?? exp?.taches_optimisees ?? exp?.missions ?? exp?.taches ?? exp?.description_optimisee ?? exp?.description)),
        relevance: this.cleanText(exp?.niveau_pertinence ?? exp?.relevance),
        keywords: this.dedupeStrings(this.asStringArray(exp?.mots_cles_cibles ?? exp?.keywords)),
      }))
      .filter((exp: any) => {
        if (!exp.role && !exp.company) return false;
        if (!this.looksLikeActivity(exp)) return true;
        activities.push({
          title: exp.role || exp.company,
          role: exp.company || null,
          description: exp.bullets[0] ?? '',
          startDate: exp.start ?? null,
          endDate: exp.end ?? null,
        });
        return false;
      });
    const projects = this.normalizeProjects(this.firstArray(data, ['projects', 'projets', 'projets_optimises']));
    const normalizedSections = this.normalizeSections(data?.sections);

    return {
      ...rawWithoutFontFamily,
      candidate: {
        name: this.resolvePreferredCandidateName(candidate, profilePersonal, liveProfilePersonal),
        email: this.cleanText(candidate?.email ?? candidate?.mail ?? profilePersonal?.email ?? profilePersonal?.mail ?? liveProfilePersonal?.email),
        phone: this.cleanText(candidate?.phone ?? candidate?.telephone ?? profilePersonal?.phone ?? profilePersonal?.telephone ?? liveProfilePersonal?.phone),
        location: this.resolvePreferredLocation(candidate, profilePersonal, liveProfilePersonal),
        title: this.cleanText(candidate?.title ?? candidate?.titrePoste ?? candidate?.poste ?? profilePersonal?.title ?? profilePersonal?.titrePoste ?? profilePersonal?.poste ?? liveProfilePersonal?.jobTitle),
        photoUrl: this.extractCandidatePhotoUrl(candidate, profilePersonal, liveProfilePersonal),
        linkedIn: candidate?.linkedIn ?? candidate?.linkedin ?? candidate?.lienLinkedin ?? profilePersonal?.linkedIn ?? profilePersonal?.linkedin ?? profilePersonal?.lienLinkedin ?? liveProfilePersonal?.linkedinUrl ?? null,
        gitHub: candidate?.gitHub ?? candidate?.github ?? candidate?.lienGithub ?? profilePersonal?.gitHub ?? profilePersonal?.github ?? profilePersonal?.lienGithub ?? liveProfilePersonal?.githubUrl ?? null,
        portfolio: candidate?.portfolio ?? candidate?.lienPortfolio ?? profilePersonal?.portfolio ?? profilePersonal?.lienPortfolio ?? liveProfilePersonal?.portfolioUrl ?? null,
      },
      summary: this.cleanText(data?.summary ?? data?.resume ?? data?.resumeProfessionnel ?? candidate?.resumeProfessionnel),
      experience,
      education: this.firstArray(data, ['education', 'formations', 'formations_optimisees']).map((edu: any) => ({
        degree: this.cleanText(edu?.degree ?? edu?.diplome ?? edu?.titre),
        institution: this.cleanText(edu?.institution ?? edu?.etablissement ?? edu?.ecole),
        year: this.cleanText(edu?.year ?? edu?.annee ?? edu?.anneeFin ?? edu?.dateFin),
        startYear: this.cleanText(edu?.startYear ?? edu?.anneeDebut ?? edu?.dateDebut),
        endYear: this.cleanText(edu?.endYear ?? edu?.anneeFin ?? edu?.dateFin),
      })),
      skills: this.firstArray(data, ['skills', 'competences', 'competences_reordonnees']).map((skill: any) => {
        const name = typeof skill === 'string' ? skill : this.cleanText(skill?.name ?? skill?.nom ?? skill?.label);
        return {
          name: this.cleanText(name),
          level: this.toSkillLevel(skill?.level ?? skill?.niveau ?? skill?.score ?? 3),
          isMatched: !!(skill?.isMatched ?? skill?.matched ?? skill?.statut === 'correspond'),
          isHighlighted: highlightedSkills.has(this.normalizeKey(name)),
          category: this.cleanText(skill?.category ?? skill?.categorie ?? skill?.typeCompetence),
          typeCompetence: this.cleanText(skill?.typeCompetence ?? skill?.type_competence),
        };
      }).filter((skill: any) => !!skill.name),
      projects,
      certifications: this.dedupeStrings(this.asStringArray(this.firstArray(data, ['certifications', 'certificats', 'certifications_optimisees']))),
      languages: this.dedupeStrings(this.asStringArray(this.firstArray(data, ['languages', 'langues']))),
      activities: this.dedupeActivities(activities),
      sections: normalizedSections,
      atsScore: Number(data?.atsScore ?? data?.ats_score ?? 0),
      matchingScore: Number(data?.matchingScore ?? data?.matching_score ?? 0),
      atsCoveragePct: Number(data?.atsCoveragePct ?? data?.ats_coverage_pct ?? data?.atsScore ?? data?.ats_score ?? 0),
    };
  }

  private normalizeSections(value: any): any[] {
    return this.asArray(value)
      .map((section: any, index: number) => {
        const normalizedId = this.normalizeSectionId(section?.id ?? section?.type);
        return {
        id: normalizedId || `section-${index + 1}`,
        type: normalizedId || this.cleanText(section?.type) || 'custom',
        title: this.cleanText(section?.title),
        placement: section?.placement === 'sidebar' ? 'sidebar' : 'main',
        isVisible: section?.isVisible !== false,
        order: Number.isFinite(Number(section?.order)) ? Number(section.order) : index,
        text: this.cleanText(section?.text) || null,
        items: this.asArray(section?.items)
          .map((item: any) => ({
            primaryText: this.stringifySectionText(item?.primaryText),
            secondaryText: this.stringifySectionText(item?.secondaryText),
            startDate: this.cleanText(item?.startDate) || null,
            endDate: this.cleanText(item?.endDate) || null,
            location: this.cleanText(item?.location) || null,
            description: this.cleanText(item?.description) || null,
            level: item?.level == null ? null : Math.min(5, Math.max(1, Number(item.level))),
            isMatched: !!item?.isMatched,
            bullets: this.dedupeStrings(this.asStringArray(item?.bullets)),
          }))
          .filter((item: any) =>
            !!item.primaryText ||
            !!item.secondaryText ||
            !!item.description ||
            item.bullets.length > 0
          ),
      };
      })
      .filter((section: any) => !!section.title || !!section.text || section.items.length > 0);
  }

  private stringifySectionText(value: any): string {
    if (typeof value === 'string') return this.cleanText(value);
    if (typeof value === 'number' || typeof value === 'boolean') return String(value);
    if (value && typeof value === 'object') {
      return this.cleanText(
        value.name
        ?? value.label
        ?? value.title
        ?? value.value
        ?? value.primaryText
        ?? ''
      );
    }
    return '';
  }

  private normalizeActivities(value: any): any[] {
    return this.asArray(value)
      .map((activity: any) => {
        if (typeof activity === 'string') {
          return {
            title: this.cleanText(activity),
            role: null,
            description: '',
            startDate: null,
            endDate: null,
          };
        }
        return {
          title: this.cleanText(activity?.title ?? activity?.name),
          role: this.cleanText(activity?.role) || null,
          description: this.cleanText(activity?.description),
          startDate: this.toMonthValue(activity?.startDate ?? activity?.start ?? activity?.dateDebut ?? activity?.date_debut),
          endDate: this.toMonthValue(activity?.endDate ?? activity?.end ?? activity?.dateFin ?? activity?.date_fin),
        };
      })
      .filter((activity: any) => !!activity.title || !!activity.description);
  }

  private normalizeProjects(value: any): any[] {
    const seen = new Set<string>();
    return this.asArray(value)
      .map((project: any) => {
        const description = this.cleanText(project?.description ?? project?.description_optimisee);
        const bullets = this.dedupeStrings(this.asStringArray(project?.bullets ?? project?.taches_optimisees ?? project?.taches ?? project?.missions))
          .filter((bullet) => !this.isSameMeaning(bullet, description));
        return {
          title: this.cleanText(project?.title ?? project?.name ?? project?.titreProjet ?? project?.titre),
          description,
          technologies: this.dedupeStrings(this.asStringArray(project?.technologies)),
          dateRealisation: this.toMonthValue(project?.dateRealisation ?? project?.date_realisation),
          relevance: this.cleanText(project?.niveau_pertinence ?? project?.relevance),
          keywords: this.dedupeStrings(this.asStringArray(project?.mots_cles_cibles ?? project?.keywords)),
          bullets,
        };
      })
      .filter((project: any) => {
        if (!project.title && !project.description && project.bullets.length === 0) return false;
        const key = this.normalizeKey(`${project.title}|${project.description ?? ''}`);
        if (seen.has(key)) return false;
        seen.add(key);
        return true;
      });
  }

  private unwrapCvPayload(value: any): any {
    if (!value || typeof value !== 'object') return value;
    return value.cvData
      ?? value.cv_data
      ?? value.cvGeneratedContent
      ?? value.cv_optimized_content
      ?? value.cvOptimizedContent
      ?? value.data
      ?? value;
  }

  private getCandidateSource(data: any): any {
    return data?.candidate
      ?? data?.personal
      ?? data?.personalInfo
      ?? data?.personal_info
      ?? data?.informations_personnelles
      ?? {};
  }

  private resolvePreferredCandidateName(candidate: any, profilePersonal: any, liveProfilePersonal: any): string {
    const candidateName = this.extractCandidateName(candidate);
    const profileName = this.extractCandidateName(profilePersonal);
    const liveName = this.extractCandidateName(liveProfilePersonal);
    if (candidateName && !this.isGenericCandidateName(candidateName)) {
      return candidateName;
    }
    return profileName || liveName || candidateName;
  }

  private resolvePreferredLocation(candidate: any, profilePersonal: any, liveProfilePersonal: any): string {
    const candidateLocation = this.cleanText(
      candidate?.location ?? [candidate?.ville ?? candidate?.city, candidate?.pays ?? candidate?.country].filter(Boolean).join(', ')
    );
    const profileLocation = this.cleanText(
      profilePersonal?.location ?? [profilePersonal?.ville ?? profilePersonal?.city, profilePersonal?.pays ?? profilePersonal?.country].filter(Boolean).join(', ')
    );
    const liveLocation = this.cleanText(
      [liveProfilePersonal?.city, liveProfilePersonal?.country].filter(Boolean).join(', ')
    );
    return candidateLocation || profileLocation || liveLocation;
  }

  private extractCandidateName(source: any): string {
    return this.cleanText(
      source?.name
      ?? source?.nomComplet
      ?? source?.fullName
      ?? [source?.firstName, source?.lastName].filter(Boolean).join(' ')
      ?? [source?.prenom ?? source?.firstName, source?.nom ?? source?.lastName].filter(Boolean).join(' ')
    );
  }

  private extractCandidatePhotoUrl(candidate: any, profilePersonal: any, liveProfilePersonal: any): string | null {
    const photo = candidate?.photoUrl
      ?? candidate?.photo_url
      ?? candidate?.profilePhoto
      ?? candidate?.profile_photo
      ?? candidate?.avatar
      ?? profilePersonal?.photoUrl
      ?? profilePersonal?.photo_url
      ?? profilePersonal?.profilePhoto
      ?? profilePersonal?.profile_photo
      ?? profilePersonal?.avatar
      ?? liveProfilePersonal?.photoUrl
      ?? this.signedProfilePhotoUrl;
    const clean = this.cleanText(photo);
    return clean || null;
  }

  private normalizeSectionId(sectionId: any): string {
    const normalized = this.cleanText(sectionId).toLowerCase();
    const aliases: Record<string, string> = {
      summary: 'summary',
      resume: 'summary',
      experience: 'experience',
      experiences: 'experience',
      project: 'projects',
      projects: 'projects',
      education: 'education',
      skill: 'skills',
      skills: 'skills',
      softskills: 'skills',
      'soft-skills': 'skills',
      soft_skills: 'skills',
      certification: 'certifications',
      certifications: 'certifications',
      language: 'languages',
      languages: 'languages',
      activity: 'activities',
      activities: 'activities',
      achievement: 'accomplishments',
      achievements: 'accomplishments',
      accomplishments: 'accomplishments',
    };
    return aliases[normalized] ?? normalized;
  }

  private isGenericCandidateName(value: string): boolean {
    const normalized = this.cleanText(value).toLowerCase();
    return normalized === 'candidat' || normalized === 'candidate';
  }

  private firstArray(source: any, keys: string[]): any[] {
    for (const key of keys) {
      const value = source?.[key];
      if (Array.isArray(value)) return value;
    }
    return [];
  }

  private dedupeActivities(activities: any[]): any[] {
    const seen = new Set<string>();
    return activities.filter((activity) => {
      const key = this.normalizeKey(`${activity.role ?? ''}|${activity.title ?? ''}`);
      if (!key || seen.has(key)) return false;
      seen.add(key);
      return true;
    });
  }

  private looksLikeActivity(exp: any): boolean {
    const text = this.normalizeKey(`${exp?.role ?? ''} ${exp?.company ?? ''} ${this.asStringArray(exp?.bullets).join(' ')}`);
    if (!text) return false;
    if (text.includes('stage') || text.includes('intern')) return false;
    return this.activitySignals.some(signal => text.includes(this.normalizeKey(signal)));
  }

  private isSameMeaning(left: string, right: string): boolean {
    const a = this.normalizeKey(left);
    const b = this.normalizeKey(right);
    if (!a || !b) return false;
    return a === b || a.includes(b) || b.includes(a);
  }

  private dedupeStrings(values: string[]): string[] {
    const seen = new Set<string>();
    return values.filter((value) => {
      const clean = this.cleanText(value);
      const key = this.normalizeKey(clean);
      if (!key || seen.has(key)) return false;
      seen.add(key);
      return true;
    });
  }

  private cleanText(value: any): string {
    return String(value ?? '').trim().replace(/\s+/g, ' ');
  }

  private normalizeKey(value: any): string {
    return this.cleanText(value)
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, ' ')
      .trim();
  }

  private asArray(value: any): any[] {
    return Array.isArray(value) ? value : [];
  }

  private asStringArray(value: any): string[] {
    const values = Array.isArray(value) ? value : [value];
    return values.map((item: any) => {
      if (typeof item === 'string') return item.trim();
      if (typeof item === 'number' || typeof item === 'boolean') return String(item);
      if (item && typeof item === 'object')
        return this.cleanText(item.name ?? item.nom ?? item.label ?? item.title ?? item.titre ?? item.description ?? '');
      return String(item ?? '').trim();
    }).filter(Boolean);
  }

  private toSkillLevel(value: any): number {
    if (typeof value === 'number') return Math.min(5, Math.max(1, Math.round(value)));
    const normalized = this.normalizeKey(value);
    if (['expert', 'avance', 'advanced', 'proficient', 'native', 'maternelle'].includes(normalized)) return 5;
    if (['intermediaire', 'intermediate', 'courant', 'upper intermediate'].includes(normalized)) return 4;
    if (['elementaire', 'elementary', 'debutant', 'beginner'].includes(normalized)) return 2;
    return 3;
  }

  private toMonthValue(value: any): string {
    const text = String(value ?? '').trim();
    const match = text.match(/^(\d{4})-(\d{2})/);
    return match ? `${match[1]}-${match[2]}` : '';
  }

  private finishReoptimization(): void {
    this.isReoptimizingCv = false;
    this.pipeline.setLoading(false);
  }

  private agentTemplateId(slug: string | null): number {
    return slug === 'modern' ? 4 : 1;
  }
}


