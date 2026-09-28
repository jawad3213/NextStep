import { Component, OnDestroy, OnInit, effect, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { Subscription, firstValueFrom, timeout } from 'rxjs';
import { PipelineStateService } from '../../data-access/pipeline-state.service';
import { ResumeEditorComponent } from '../resume-editor/resume-editor.component';
import { SignalRService } from '../../data-access/signalr.service';
import { ProfileService } from '@features/profile/data-access/profile.service';
import { extractApiError } from '@core/http/extract-api-error';
import { CV_TEMPLATES, CV_TEMPLATE_SLUGS, defaultCvDesignConfig } from '../../data-access/cv-templates';
import { CvApiService } from '@features/cv-builder/data-access/cv-api.service';
import { CvDesignConfig } from '@features/cv-builder/data-access/cv.models';
import { OfferApiService } from '../../data-access/offer-api.service';
import { CvRenderResponse, CvSaveResponse } from '@features/cv-builder/data-access/cv.models';
import { backendOrigin } from '@core/http/api-url';
import { getCandidateSource, normalizeCvForBackend, unwrapCvPayload } from './cv-payload.normalizer';
import { cleanText, firstArray } from '../../utils/cv-json.utils';

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
  private readonly cvApi = inject(CvApiService);
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
  isGeneratingHighQualityPdf = false;
  isReoptimizingCv = false;
  private savedDraftHash = '';

  readonly realTemplates: RealCvTemplate[] = CV_TEMPLATES.map(({ slug, label, tone }) => ({ slug, label, tone }));

  readonly generationStages = [
    { key: 'profile_context', label: 'Contexte candidat' },
    { key: 'analysis_context', label: 'Analyse consolidee' },
    { key: 'cv_optimizer', label: 'CV optimizer' },
    { key: 'cv_engine', label: 'CV engine' },
    { key: 'db_persist', label: 'Finalisation' },
  ] as const;

  private readonly templateMap: Record<string, string> = Object.fromEntries(CV_TEMPLATE_SLUGS.map((slug) => [slug, slug]));

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
    const normalized = unwrapCvPayload(data);
    const candidate = getCandidateSource(normalized);
    const hasIdentity = !!cleanText(candidate?.name)
      || !!cleanText(candidate?.nomComplet)
      || !!cleanText(candidate?.fullName)
      || !!cleanText(candidate?.email)
      || !!cleanText(normalized?.summary)
      || !!cleanText(normalized?.resume)
      || !!cleanText(normalized?.resumeProfessionnel);
    const hasSections = firstArray(normalized, ['experience', 'experiences', 'experiences_optimisees']).length > 0
      || firstArray(normalized, ['education', 'formations', 'formations_optimisees']).length > 0
      || firstArray(normalized, ['skills', 'competences', 'competences_reordonnees', 'competences_mises_en_avant']).length > 0
      || firstArray(normalized, ['projects', 'projets', 'projets_optimises']).length > 0;
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
    if (this.isSavingFinal) {
      return;
    }

    try {
      await this.saveFinalCv();
    } catch (err: any) {
      this.pipeline.pipelineError.set(extractApiError(err).message || 'Impossible de sauvegarder le CV final.');
    } finally {
      // Always move to the email composer step so the flow cannot get stuck.
      this.pipeline.markStepDone(3);
      this.pipeline.goToStep(5);
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
        this.pipeline.pipelineError.set(extractApiError(err).message || 'Echec de la re-optimisation du CV.');
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

    const origin = backendOrigin();
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
    if (!data) {
      throw new Error('CV introuvable pour la sauvegarde finale.');
    }

    const finalTitle = offerId
      ? `CV_${offerId}`
      : `CV_${new Date().toISOString().slice(0, 19).replace(/[:T]/g, '-')}`;

    this.isSavingFinal = true;
    this.pipeline.setLoading(true, 'Sauvegarde du PDF final...');
    try {
      const saved = await firstValueFrom(
        this.cvApi.saveFinalCv({
          templateSlug: this.selectedTemplate,
          title: finalTitle,
          offerId,
          data,
          designConfig: this.currentDesignConfig,
          htmlSnapshot: this.renderedHtmlSnapshot
        }).pipe(
          timeout(45000)
        )
      );
      this.savedHistoryId = saved?.historyId ?? null;
      this.savedFileUrl = saved?.fileUrl ?? null;
      this.savedDraftHash = this.buildDraftHash(data, this.currentDesignConfig);
      this.pipeline.cvDownloadUrl.set(saved?.fileUrl ?? null);
      this.pipeline.finalCvHistoryId.set(saved?.historyId ?? null);
      this.pipeline.finalCvTitle.set(finalTitle);
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
      const data = this.currentCvData();
      if (!data) throw new Error('CV introuvable pour le telechargement.');

      this.isGeneratingHighQualityPdf = true;
      this.pipeline.pipelineError.set(null);
      const blob = await firstValueFrom(this.cvApi.exportCvPdf({
        templateSlug: this.selectedTemplate,
        data,
        designConfig: this.currentDesignConfig,
        htmlSnapshot: this.renderedHtmlSnapshot
      }).pipe(
        timeout(45000)
      ));

      if (!blob || blob.size === 0) {
        throw new Error('PDF vide recu depuis le backend.');
      }
      this.downloadBlob(blob);
    } catch (err: any) {
      console.error('[CV-PIPELINE] Final CV download failed', err);
      this.pipeline.pipelineError.set(extractApiError(err).message || 'Telechargement PDF indisponible.');
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

    this.previewRequestSub = this.cvApi.renderCvPreview({
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

    this.loadDraftSub = this.cvApi.getCvDraft(this.currentOfferId).subscribe({
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

    this.autosaveSub = this.cvApi.saveCvDraft(offerId, payload).subscribe({
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
    return defaultCvDesignConfig(templateSlug);
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
    return normalizeCvForBackend(data, {
      pipelineResult: this.pipeline.pipelineResult(),
      liveProfilePersonal: this.profileService.profile()?.personal ?? {},
      signedProfilePhotoUrl: this.signedProfilePhotoUrl,
    });
  }

  private finishReoptimization(): void {
    this.isReoptimizingCv = false;
    this.pipeline.setLoading(false);
  }

  private agentTemplateId(slug: string | null): number {
    return slug === 'modern' ? 4 : 1;
  }
}


