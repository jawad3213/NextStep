import { Component, OnDestroy, OnInit, effect, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { Subscription, firstValueFrom } from 'rxjs';
import { PipelineStateService } from '../../../../services/pipeline-state.service';
import { ResumeEditorComponent } from '../resume-editor/resume-editor.component';
import { CvSaveResponse, OfferApiService } from '../../services/offer-api.service';
import { environment } from '../../../../../environments/environment';

interface RealCvTemplate {
  slug: string;
  label: string;
  tone: string;
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
  private previewDebounceId: number | null = null;
  private autosaveDebounceId: number | null = null;
  private previewRequestSub: Subscription | null = null;
  private autosaveSub: Subscription | null = null;
  private loadDraftSub: Subscription | null = null;
  private lastDraftHash = '';
  private previewBlobUrl: string | null = null;
  private readonly thumbnailUrlCache = new Map<string, SafeResourceUrl>();
  private readonly draftKey = 'nextstep_cv_draft';
  private readonly draftOfferKey = 'nextstep_cv_draft_offer_id';
  private hasUserEditedDraft = false;

  editorInitialData = signal<any | null>(null);
  private editorDraft = signal<any | null>(null);
  livePreviewUrl: SafeResourceUrl | null = null;
  isRenderingPreview = false;
  showPreviewModal = false;
  previewError: string | null = null;
  draftStatus: 'idle' | 'local' | 'saving' | 'saved' | 'error' = 'idle';
  draftSavedAt: string | null = null;
  draftVersion: number | null = null;
  draftError: string | null = null;
  savedHistoryId: string | null = null;
  savedFileUrl: string | null = null;
  isSavingFinal = false;
  isDownloadingFinal = false;
  private savedDraftHash = '';
  private readonly activitySignals = [
    'hackathon', 'club', 'association', 'organisateur', 'organizer',
    'membre', 'member', 'volunteer', 'benevole', 'bénévole', 'event',
    'community', 'communaut', 'it day', 'prize', 'prix', 'participant',
    'formateur', 'trainer', 'formation', 'solihackathon', 'itwave', 'ids'
  ];

  // Floating designer bar state
  showFontDropdown = false;
  showSizeDropdown = false;

  get currentFontFamily(): string {
    const data = this.currentCvData();
    return data?.fontFamily || 'Lato';
  }

  get currentThemeColor(): string {
    const data = this.currentCvData();
    return data?.themeColor || '#1A91F0';
  }

  get currentFontSize(): string {
    const data = this.currentCvData();
    return data?.fontSize || '14px';
  }

  get currentLineSpacing(): string {
    const data = this.currentCvData();
    return data?.lineSpacing || '1.15';
  }

  get draftStatusText(): string {
    if (this.draftError) return this.draftError;
    if (this.draftStatus === 'saving') return 'Autosave backend en cours...';
    if (this.draftStatus === 'saved') {
      const version = this.draftVersion ? ` v${this.draftVersion}` : '';
      const time = this.draftSavedAt ? ` a ${this.draftSavedAt}` : '';
      return `Brouillon synchronise${version}${time}`;
    }
    if (this.draftStatus === 'local') return 'Brouillon local protege, synchronisation en attente.';
    return 'Brouillon pret.';
  }

  toggleFontDropdown(): void {
    this.showFontDropdown = !this.showFontDropdown;
    this.showSizeDropdown = false;
  }

  toggleSizeDropdown(): void {
    this.showSizeDropdown = !this.showSizeDropdown;
    this.showFontDropdown = false;
  }

  selectFont(font: string): void {
    this.changeFontFamily(font);
    this.showFontDropdown = false;
  }

  selectSize(size: string): void {
    this.changeFontSize(size);
    this.showSizeDropdown = false;
  }

  selectThemeColor(color: string): void {
    this.changeThemeColor(color);
  }

  changeFontFamily(font: string): void {
    this.patchCurrentDraft({ fontFamily: font });
  }

  changeThemeColor(color: string): void {
    this.patchCurrentDraft({ themeColor: color });
  }

  changeFontSize(size: string): void {
    this.patchCurrentDraft({ fontSize: size });
  }

  changeLineSpacing(spacing: string): void {
    this.patchCurrentDraft({ lineSpacing: spacing });
  }

  readonly realTemplates: RealCvTemplate[] = [
    { slug: 'chrono', label: 'Chrono', tone: 'Green Accent' },
    { slug: 'elegant', label: 'Elegant', tone: 'Dark Teal' },
    { slug: 'circular', label: 'Circular', tone: 'Wavy Sidebar' },
    { slug: 'modern', label: 'Modern', tone: 'Tech Minimal' },
    { slug: 'luxe', label: 'Luxe', tone: 'Premium Centered' },
  ];

  private readonly templateMap: Record<string, string> = {
    chrono: 'chrono',
    elegant: 'elegant',
    circular: 'circular',
    modern: 'modern',
    luxe: 'luxe',
  };

  private readonly generationTrace = effect(() => {
    const offerId = this.pipeline.currentOfferId();
    const result = this.pipeline.pipelineResult();
    const generated = result?.cvGeneratedContent;
    console.log('[CV-PIPELINE] StepGeneration state', {
      offerId,
      hasPipelineResult: !!result,
      hasGeneratedCv: !!generated,
      hasCandidate: !!generated?.candidate,
      experienceCount: Array.isArray(generated?.experience) ? generated.experience.length : 0,
      skillsCount: Array.isArray(generated?.skills) ? generated.skills.length : 0,
      languagesCount: Array.isArray(generated?.languages) ? generated.languages.length : 0
    });
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

  ngOnInit(): void {
    this.hydrateInitialDraft();
  }

  ngOnDestroy(): void {
    if (this.previewDebounceId !== null) window.clearTimeout(this.previewDebounceId);
    if (this.autosaveDebounceId !== null) window.clearTimeout(this.autosaveDebounceId);
    this.previewRequestSub?.unsubscribe();
    this.autosaveSub?.unsubscribe();
    this.loadDraftSub?.unsubscribe();
    if (this.previewBlobUrl) {
      window.URL.revokeObjectURL(this.previewBlobUrl);
      this.previewBlobUrl = null;
    }
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

  openPreviewModal(): void {
    if (!this.livePreviewUrl) return;
    this.showPreviewModal = true;
  }

  closePreviewModal(): void {
    this.showPreviewModal = false;
  }

  selectTemplate(slug: string): void {
    if (slug === this.selectedTemplate) return;
    this.pipeline.selectedTemplateId.set(slug);
    this.savedHistoryId = null;
    this.savedFileUrl = null;
    this.savedDraftHash = '';
    this.lastDraftHash = '';
    this.queueLivePreview();
  }

  templateThumbnailUrl(slug: string): SafeResourceUrl {
    const cached = this.thumbnailUrlCache.get(slug);
    if (cached) return cached;

    const origin = new URL(environment.apiBaseUrl).origin;
    const safeUrl = this.sanitizer.bypassSecurityTrustResourceUrl(
      `${origin}/api/cv/templates/${encodeURIComponent(slug)}/thumbnail#toolbar=0&navpanes=0&scrollbar=0`
    );
    this.thumbnailUrlCache.set(slug, safeUrl);
    return safeUrl;
  }

  onEditorDataChange(rawData: any): void {
    const data = this.normalizeCvForBackend(rawData);
    const previous = this.editorDraft();
    const previousHash = previous ? this.buildDraftHash(previous) : '';
    const nextHash = this.buildDraftHash(data);

    if (previousHash && previousHash === nextHash) {
      this.writeLocalDraft(data);
      return;
    }

    if (previousHash && previousHash !== nextHash) {
      this.hasUserEditedDraft = true;
    }

    this.editorDraft.set(data);
    this.writeLocalDraft(data);
    this.draftStatus = 'local';
    this.draftError = null;
    this.queueLivePreview(data);
    this.queueBackendAutosave(data);
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
        this.offerApi.saveFinalCv(this.selectedTemplate, `CV_${offerId}`, data)
      );
      this.savedHistoryId = saved?.historyId ?? null;
      this.savedFileUrl = saved?.fileUrl ?? null;
      this.savedDraftHash = this.buildDraftHash(data);
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
      if (!this.savedHistoryId || this.savedDraftHash !== this.buildDraftHash(this.currentCvData())) {
        await this.saveFinalCv();
      }

      if (!this.savedHistoryId) throw new Error('Historique CV introuvable.');

      this.isDownloadingFinal = true;
      const blob = await firstValueFrom(this.offerApi.downloadCvHistoryFile(this.savedHistoryId));
      this.downloadBlob(blob);
    } catch (err: any) {
      console.error('[CV-PIPELINE] Final CV download failed', err);
      this.pipeline.pipelineError.set(err?.error?.message || err?.message || 'Telechargement PDF indisponible.');
    } finally {
      this.isDownloadingFinal = false;
    }
  }

  private queueLivePreview(data: any | null = this.currentCvData(), delayMs = 320): void {
    if (!data) return;

    const hash = this.buildDraftHash(data);
    if (this.savedDraftHash && this.savedDraftHash !== hash) {
      this.savedHistoryId = null;
      this.savedFileUrl = null;
      this.savedDraftHash = '';
    }

    if (hash !== this.lastDraftHash || this.previewError) {
      this.isRenderingPreview = true;
      this.previewError = null;
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
    const hash = this.buildDraftHash(data);
    if (hash === this.lastDraftHash && !this.previewError) {
      this.isRenderingPreview = false;
      return;
    }

    this.previewRequestSub?.unsubscribe();
    this.lastDraftHash = hash;
    this.isRenderingPreview = true;
    this.previewError = null;

    this.previewRequestSub = this.offerApi.renderCvPreview(this.selectedTemplate, data).subscribe({
      next: (blob) => {
        if (this.previewBlobUrl) {
          window.URL.revokeObjectURL(this.previewBlobUrl);
        }
        this.previewBlobUrl = window.URL.createObjectURL(blob);
        this.livePreviewUrl = this.sanitizer.bypassSecurityTrustResourceUrl(
          `${this.previewBlobUrl}#toolbar=0&navpanes=0&scrollbar=0`
        );
        this.previewError = null;
        this.isRenderingPreview = false;
      },
      error: (err) => {
        console.error('[CV-PIPELINE] Live PDF preview failed', err);
        this.lastDraftHash = '';
        this.previewError = err?.error?.message || err?.error || err?.message || 'Apercu PDF indisponible.';
        this.isRenderingPreview = false;
      }
    });
  }

  private buildDraftHash(data: any): string {
    return JSON.stringify({ t: this.selectedTemplate, data });
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
    const fallback = localDraft || this.generatedCvData;

    if (fallback) {
      this.applyInitialDraft(fallback, localDraft ? 'local' : 'idle');
    }

    const offerId = this.currentOfferId;
    if (!offerId) return;

    this.loadDraftSub = this.offerApi.getCvDraft(offerId).subscribe({
      next: (draft) => {
        if (!draft?.data || this.hasUserEditedDraft) return;

        this.applyInitialDraft(draft.data, 'saved');
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

  private applyInitialDraft(rawData: any, status: 'idle' | 'local' | 'saved'): void {
    if (this.autosaveDebounceId !== null) {
      window.clearTimeout(this.autosaveDebounceId);
      this.autosaveDebounceId = null;
    }

    const data = this.normalizeCvForBackend(rawData);
    this.editorInitialData.set(data);
    this.editorDraft.set(data);
    this.writeLocalDraft(data);
    this.draftStatus = status;
    this.draftError = null;
    this.queueLivePreview(data, 0);
  }

  private readLocalDraftForCurrentOffer(): any | null {
    if (!this.currentOfferId) return null;
    const draftOfferId = localStorage.getItem(this.draftOfferKey);
    if (draftOfferId !== this.currentOfferId) return null;
    return this.safeParseJson(localStorage.getItem(this.draftKey));
  }

  private writeLocalDraft(data: any): void {
    try {
      localStorage.setItem(this.draftKey, JSON.stringify(data));
      if (this.currentOfferId) {
        localStorage.setItem(this.draftOfferKey, this.currentOfferId);
      }
    } catch (err) {
      console.warn('[CV-PIPELINE] Local draft persistence failed', err);
    }
  }

  private patchCurrentDraft(patch: Record<string, any>): void {
    const data = this.currentCvData();
    if (!data) return;
    this.onEditorDataChange({ ...data, ...patch });
  }

  private queueBackendAutosave(data: any | null = this.currentCvData(), delayMs = 900): void {
    if (!this.currentOfferId || !data) return;
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

    this.autosaveSub = this.offerApi.saveCvDraft(offerId, data).subscribe({
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
    const data = this.editorDraft() || this.readLocalDraftForCurrentOffer() || this.generatedCvData;
    return data ? this.normalizeCvForBackend(data) : null;
  }

  private prepareDraftForCurrentOffer(): void {
    const offerId = this.currentOfferId;
    const generated = this.generatedCvData;
    const draftOfferId = localStorage.getItem(this.draftOfferKey);

    if (!offerId || !generated) return;

    if (draftOfferId === offerId) {
      const draft = this.safeParseJson(localStorage.getItem(this.draftKey));
      if (draft && !this.shouldReplaceDraft(draft, generated)) return;
    }

    localStorage.setItem(this.draftKey, JSON.stringify(generated));
    localStorage.setItem(this.draftOfferKey, offerId);
  }

  private shouldReplaceDraft(draft: any, generated: any): boolean {
    const draftName = String(draft?.candidate?.name ?? '').trim().toLowerCase();
    const generatedName = String(generated?.candidate?.name ?? '').trim();
    const draftEmail = String(draft?.candidate?.email ?? '').trim();
    const generatedEmail = String(generated?.candidate?.email ?? '').trim();

    if (!draftName || draftName === 'candidat') return !!generatedName && generatedName.toLowerCase() !== 'candidat';
    if (!draftEmail && generatedEmail) return true;
    if (!Array.isArray(draft?.skills) || draft.skills.length === 0) return Array.isArray(generated?.skills) && generated.skills.length > 0;
    return false;
  }

  private normalizeCvForBackend(data: any): any {
    const candidate = data?.candidate ?? {};
    const activities = this.normalizeActivities(data?.activities);
    const experience = this.asArray(data?.experience)
      .map((exp: any) => ({
        role: this.cleanText(exp?.role ?? exp?.title),
        company: this.cleanText(exp?.company),
        start: this.toMonthValue(exp?.start),
        end: this.toMonthValue(exp?.end),
        bullets: this.dedupeStrings(this.asStringArray(exp?.bullets)).slice(0, 4),
      }))
      .filter((exp: any) => {
        if (!exp.role && !exp.company) return false;
        if (!this.looksLikeActivity(exp)) return true;
        activities.push({
          title: exp.company || exp.role,
          role: exp.company ? exp.role : null,
          description: exp.bullets[0] ?? '',
        });
        return false;
      });
    const projects = this.normalizeProjects(data?.projects);

    return {
      ...data,
      candidate: {
        name: this.cleanText(candidate?.name),
        email: this.cleanText(candidate?.email),
        phone: this.cleanText(candidate?.phone),
        location: this.cleanText(candidate?.location),
        linkedIn: candidate?.linkedIn ?? candidate?.linkedin ?? null,
        gitHub: candidate?.gitHub ?? candidate?.github ?? null,
        portfolio: candidate?.portfolio ?? null,
      },
      summary: this.cleanText(data?.summary),
      experience,
      education: this.asArray(data?.education).map((edu: any) => ({
        degree: this.cleanText(edu?.degree),
        institution: this.cleanText(edu?.institution),
        year: this.cleanText(edu?.year),
      })),
      skills: this.asArray(data?.skills).map((skill: any) => ({
        name: this.cleanText(skill?.name),
        level: Math.min(5, Math.max(1, Number(skill?.level ?? 3))),
        isMatched: !!skill?.isMatched,
      })).filter((skill: any) => !!skill.name).slice(0, 18),
      projects,
      certifications: this.dedupeStrings(this.asStringArray(data?.certifications)).slice(0, 6),
      languages: this.dedupeStrings(this.asStringArray(data?.languages)).slice(0, 6),
      activities: this.dedupeActivities(activities).slice(0, 5),
      atsScore: Number(data?.atsScore ?? 0),
      matchingScore: Number(data?.matchingScore ?? 0),
      atsCoveragePct: Number(data?.atsCoveragePct ?? data?.atsScore ?? 0),
    };
  }

  private normalizeActivities(value: any): any[] {
    return this.asArray(value)
      .map((activity: any) => {
        if (typeof activity === 'string') {
          return { title: this.cleanText(activity), role: null, description: '' };
        }
        return {
          title: this.cleanText(activity?.title ?? activity?.name),
          role: this.cleanText(activity?.role) || null,
          description: this.cleanText(activity?.description),
        };
      })
      .filter((activity: any) => !!activity.title || !!activity.description);
  }

  private normalizeProjects(value: any): any[] {
    const seen = new Set<string>();
    return this.asArray(value)
      .map((project: any) => {
        const description = this.cleanText(project?.description);
        const bullets = this.dedupeStrings(this.asStringArray(project?.bullets))
          .filter((bullet) => !this.isSameMeaning(bullet, description))
          .slice(0, 4);
        return {
          title: this.cleanText(project?.title ?? project?.name),
          description: bullets.length > 0 ? null : description,
          bullets,
        };
      })
      .filter((project: any) => {
        if (!project.title && project.bullets.length === 0) return false;
        const key = this.normalizeKey(project.title);
        if (seen.has(key)) return false;
        seen.add(key);
        return true;
      })
      .slice(0, 4);
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
    return this.asArray(value).map((item: any) => String(item ?? '').trim()).filter(Boolean);
  }

  private toMonthValue(value: any): string {
    const text = String(value ?? '').trim();
    const match = text.match(/^(\d{4})-(\d{2})/);
    return match ? `${match[1]}-${match[2]}` : '';
  }
}
