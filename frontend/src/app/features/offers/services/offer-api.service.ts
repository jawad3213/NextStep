import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, timeout } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../../../environments/environment';

export interface OfferSubmitPayload {
  rawText: string;
  templateId: number;
}

export interface OfferSubmitResponse {
  offerId: string;
  status: string;
}

export interface OfferHistoryItem {
  offerId: string;
  titre: string;
  entreprise: string;
  localisation: string;
  scoreMatching?: number;
  status: 'cv_genere' | 'non_traitee' | 'analysee';
  currentStep: number;
  dateCreation: string;
}

export type ScrapeProvider = 'linkedin' | 'indeed' | 'glassdoor';

export interface LinkedInJobsSearchRequest {
  keywords: string;
  location?: string;
  limit?: number;
  posted_since_seconds?: number;
  search_url?: string;
  fetch_details?: boolean;
  it_only?: boolean;
}

export interface LinkedInScrapedJob {
  job_id?: string | null;
  title: string;
  company?: string | null;
  location?: string | null;
  posted_at_text?: string | null;
  url?: string | null;
  description?: string | null;
  employment_type?: string | null;
  seniority_level?: string | null;
  job_function?: string | null;
  industries?: string[];
  source: string;
  search_url?: string | null;
  is_it_offer?: boolean;
  matched_it_terms?: string[];
}

export interface LinkedInJobsSearchResponse {
  keywords?: string | null;
  location?: string | null;
  total_found: number;
  total_returned: number;
  it_only: boolean;
  search_urls: string[];
  jobs: LinkedInScrapedJob[];
  errors: string[];
}

export interface IndeedJobsSearchRequest {
  keywords: string;
  location?: string;
  limit?: number;
  search_url?: string;
  fetch_details?: boolean;
  it_only?: boolean;
  country_code?: string;
}

export interface IndeedScrapedJob {
  job_id?: string | null;
  title: string;
  company?: string | null;
  location?: string | null;
  posted_at_text?: string | null;
  url?: string | null;
  description?: string | null;
  employment_type?: string | null;
  seniority_level?: string | null;
  job_function?: string | null;
  industries?: string[];
  source: string;
  search_url?: string | null;
  is_it_offer?: boolean;
  matched_it_terms?: string[];
}

export interface IndeedJobsSearchResponse {
  keywords?: string | null;
  location?: string | null;
  total_found: number;
  total_returned: number;
  it_only: boolean;
  search_urls: string[];
  jobs: IndeedScrapedJob[];
  errors: string[];
}

export interface GlassdoorJobsSearchRequest {
  keywords: string;
  location?: string;
  limit?: number;
  search_url?: string;
  fetch_details?: boolean;
  it_only?: boolean;
}

export interface GlassdoorScrapedJob {
  job_id?: string | null;
  title: string;
  company?: string | null;
  location?: string | null;
  posted_at_text?: string | null;
  url?: string | null;
  description?: string | null;
  employment_type?: string | null;
  seniority_level?: string | null;
  job_function?: string | null;
  industries?: string[];
  source: string;
  search_url?: string | null;
  is_it_offer?: boolean;
  matched_it_terms?: string[];
}

export interface GlassdoorJobsSearchResponse {
  keywords?: string | null;
  location?: string | null;
  total_found: number;
  total_returned: number;
  it_only: boolean;
  search_urls: string[];
  jobs: GlassdoorScrapedJob[];
  errors: string[];
}

export type PostedWindow = '24h' | '3d' | '7d' | '14d' | '30d' | 'any';
export type NormalizedContractType =
  | 'internship'
  | 'cdi'
  | 'cdd'
  | 'freelance'
  | 'alternance'
  | 'part_time'
  | 'full_time'
  | 'temporary'
  | 'other';

export interface SourcedOfferSearchRequest {
  keywords?: string | null;
  location?: string | null;
  providers?: ScrapeProvider[];
  limit?: number;
  postedWindow?: PostedWindow;
  contractTypes?: NormalizedContractType[];
  indeedCountryCode?: string | null;
  workflowState?: 'saved' | 'shortlisted' | 'archived' | null;
}

export interface ScrapeSessionDto {
  id: string;
  keywords?: string | null;
  location?: string | null;
  providers: string[];
  countryCode?: string | null;
  postedWindow: PostedWindow;
  contractTypes: NormalizedContractType[];
  limit: number;
  resultCount: number;
  warnings: string[];
  errors: string[];
  createdAtUtc: string;
}

export interface SourcedOfferListItemDto {
  id: string;
  provider: ScrapeProvider;
  providerJobId?: string | null;
  externalUrl?: string | null;
  title: string;
  company?: string | null;
  location?: string | null;
  description?: string | null;
  postedAtText?: string | null;
  postedWindow?: PostedWindow | null;
  rawContractType?: string | null;
  normalizedContractType?: NormalizedContractType | null;
  employmentType?: string | null;
  seniorityLevel?: string | null;
  matchedItTerms: string[];
  aiScore?: number | null;
  aiScoreSkills?: number | null;
  aiScoreTitle?: number | null;
  aiScoreLocation?: number | null;
  aiScoreContract?: number | null;
  aiScoreFreshness?: number | null;
  aiConfidence?: number | null;
  aiMatchedSkills?: string[];
  aiMissingSkills?: string[];
  aiReasons?: string[];
  aiSummary?: string | null;
  aiRankedAtUtc?: string | null;
  isSaved: boolean;
  isShortlisted: boolean;
  isArchived: boolean;
  promotedOfferId?: string | null;
  firstSeenAtUtc: string;
  lastSeenAtUtc: string;
  scrapedAtUtc: string;
}

export interface SourcedOfferDetailDto extends SourcedOfferListItemDto {
  sourceQuery: Record<string, unknown>;
  similarOffers: SourcedOfferListItemDto[];
}

export interface SourcedOfferSearchResponse {
  session?: ScrapeSessionDto | null;
  offers: SourcedOfferListItemDto[];
  warnings: string[];
}

export interface SourcedOfferUpdateRequest {
  isSaved?: boolean;
  isShortlisted?: boolean;
  isArchived?: boolean;
}

export interface PromoteSourcedOfferResponse {
  offerId: string;
  alreadyPromoted: boolean;
}

export interface PdfGeneratePayload {
  templateId: string;
}

export interface PdfGenerateResponse {
  offerId: string;
  downloadUrl: string;
  status: string;
}

export interface ResumePipelineResponse {
  cvGeneratedContent?: any;
  cv_data?: any;
  cvData?: any;
  profileData?: any;
  profile_data?: any;
  _cvPending?: boolean;
  cv_optimized_content?: any;
  cvOptimizedContent?: any;
  email_subject?: string;
  email_body?: string;
  recruiter_name?: string;
  [key: string]: any;
}

export interface CvHistoryItem {
  id: string;
  title?: string | null;
  templateSlug: string;
  templateName?: string | null;
  fileUrl: string;
  fileSizeBytes: number;
  createdAt: string;
  updatedAt?: string | null;
}

export interface CvSaveResponse {
  historyId: string;
  fileUrl: string;
  fileSizeBytes: number;
}

export interface SaveFinalCvRequest {
  templateSlug: string;
  title: string;
  offerId?: string | null;
  data: any;
  designConfig: CvDesignConfig;
  htmlSnapshot?: string | null;
}

export interface CvDesignConfig {
  themeColor: string;
  fontFamily: string;
  fontSize: string;
  lineSpacing: string;
  sectionSpacing: string;
  sidebarWidth: string;
}

export interface CvTemplateDto {
  id: string;
  slug: string;
  name: string;
  description?: string | null;
  thumbnailUrl?: string | null;
  industries: string[];
  experienceLevels: string[];
  style: string;
  layoutFlags: string[];
  backgroundColor: string;
  tags: string[];
}

export interface CvRenderRequest {
  templateSlug: string;
  data: any;
  designConfig: CvDesignConfig;
}

export interface CvRenderResponse {
  templateSlug: string;
  designConfig: CvDesignConfig;
  html: string;
}

export interface CvPreviewResponse {
  templateSlug: string;
  data: any;
  designConfig: CvDesignConfig;
  html: string;
}

export interface CvExportPdfRequest {
  templateSlug: string;
  data: any;
  designConfig: CvDesignConfig;
  htmlSnapshot?: string | null;
}

export interface CvDraftResponse {
  offerId: string;
  data: any;
  version: number;
  updatedAtUtc: string;
}

export interface SendApplicationEmailRequest {
  offerId: string;
  cvHistoryId?: string | null;
  recipientEmail: string;
  subject: string;
  body: string;
  emailType?: string;
  language?: string;
}

export interface EmailDraftResponse {
  id: string;
  candidatureId: string;
  emailType: string;
  recipientEmail?: string | null;
  subject: string;
  body: string;
  language: string;
  isApproved: boolean;
  isSent: boolean;
  createdAtUtc: string;
  updatedAtUtc?: string | null;
  sentAtUtc?: string | null;
  errorMessage?: string | null;
}

export interface SkillDetail {
  nom: string;
  categorie: 'technique' | 'soft' | 'langue' | 'certification';
  statut: 'correspond' | 'partiel' | 'manquant';
}

export interface RecommendationPriorisee {
  texte: string;
  priorite: 'haute' | 'moyenne' | 'basse';
}

export interface KeywordPondere {
  mot: string;
  poids: number;
}

export interface OfferAnalysisResponse {
  offerId: string;
  titre: string;
  entreprise: string | null;
  typeContrat: string | null;
  localisation: string | null;
  competencesRequises: string[];
  competencesSouhaitees: string[];
  keywordsAts: string[];
  anneesExperience: number | null;
  niveauEtudes: string | null;
  modeTravail?: string;
  descriptionPoste: string | null;
  texteBrut: string | null;
  scoreMatching: number;
  scoreAts: number;
  keywordsPresents: string[];
  keywordsManquants: string[];
  recommandations: string[];
  competencesMatching: string[];
  competencesManquantes: string[];
  companyCultureScore: number;
  companySalaryMin: number;
  companySalaryMax: number;
  companySize: string;
  companyNews: { title: string; date: string }[];
  dateAnalyse: string;
  erreurs: string[];
  competencesAvecDetails?: SkillDetail[];
  recommandationsAvecPriorite?: RecommendationPriorisee[];
  keywordsAvecPoids?: KeywordPondere[];
  forcesProfil?: string[];
  cvGeneratedContent?: any;
  cv_data?: any;
  cvData?: any;
  profileData?: any;
  profile_data?: any;
}

@Injectable({ providedIn: 'root' })
export class OfferApiService {
  private http = inject(HttpClient);
  private base = environment.apiBaseUrl;
  private agentsBase = environment.agentsBaseUrl;

  submitOffer(payload: OfferSubmitPayload): Observable<OfferSubmitResponse> {
    return this.http.post<OfferSubmitResponse>(
      `${this.base}/offers/submit`,
      payload
    );
  }

  getOffersHistory(): Observable<OfferHistoryItem[]> {
    return this.http.get<OfferHistoryItem[]>(`${this.base}/offers`);
  }

  searchSourcedOffers(payload: SourcedOfferSearchRequest): Observable<SourcedOfferSearchResponse> {
    return this.http.post<SourcedOfferSearchResponse>(`${this.base}/sourced-offers/search`, payload);
  }

  getSourcedOffers(params: SourcedOfferSearchRequest = {}): Observable<SourcedOfferListItemDto[]> {
    const query = new URLSearchParams();
    if (params.keywords) query.set('keywords', params.keywords);
    if (params.location) query.set('location', params.location);
    for (const provider of params.providers ?? []) query.append('providers', provider);
    if (params.limit) query.set('limit', String(params.limit));
    if (params.postedWindow) query.set('postedWindow', params.postedWindow);
    for (const contractType of params.contractTypes ?? []) query.append('contractTypes', contractType);
    if (params.indeedCountryCode) query.set('indeedCountryCode', params.indeedCountryCode);
    if (params.workflowState) query.set('workflowState', params.workflowState);
    const suffix = query.toString() ? `?${query.toString()}` : '';
    return this.http.get<SourcedOfferListItemDto[]>(`${this.base}/sourced-offers${suffix}`);
  }

  getSourcedOffer(id: string): Observable<SourcedOfferDetailDto> {
    return this.http.get<SourcedOfferDetailDto>(`${this.base}/sourced-offers/${id}`);
  }

  updateSourcedOffer(id: string, payload: SourcedOfferUpdateRequest): Observable<SourcedOfferDetailDto> {
    return this.http.patch<SourcedOfferDetailDto>(`${this.base}/sourced-offers/${id}`, payload);
  }

  promoteSourcedOffer(id: string): Observable<PromoteSourcedOfferResponse> {
    return this.http.post<PromoteSourcedOfferResponse>(`${this.base}/sourced-offers/${id}/promote`, {});
  }

  searchLinkedInJobs(payload: LinkedInJobsSearchRequest): Observable<LinkedInJobsSearchResponse> {
    return this.http.post<LinkedInJobsSearchResponse>(`${this.agentsBase}/linkedin-jobs/search`, payload);
  }

  searchIndeedJobs(payload: IndeedJobsSearchRequest): Observable<IndeedJobsSearchResponse> {
    return this.http.post<IndeedJobsSearchResponse>(`${this.agentsBase}/indeed-jobs/search`, payload);
  }

  searchGlassdoorJobs(payload: GlassdoorJobsSearchRequest): Observable<GlassdoorJobsSearchResponse> {
    return this.http.post<GlassdoorJobsSearchResponse>(`${this.agentsBase}/glassdoor-jobs/search`, payload);
  }

  bulkDeleteOffers(offerIds: string[]): Observable<{ deletedCount: number }> {
    return this.http.post<{ deletedCount: number }>(`${this.base}/offers/delete`, { offerIds }).pipe(
      catchError(() =>
        this.http.post<{ deletedCount: number }>(`${this.base}/offers/bulk-delete`, { offerIds })
      )
    );
  }

  /**
   * Step 1 — Fire ONLY the 3 agents (Offer Analysis + Profile Retriever + Skill Gap)
   * POST /api/offers/{id}/analyze-sync
   */
  analyzeSync(offerId: string, templateId: number = 1): Observable<OfferAnalysisResponse> {
    return this.http.post<OfferAnalysisResponse>(
      `${this.base}/offers/${offerId}/analyze-sync`,
      { templateId }
    );
  }

  generatePdf(offerId: string, templateId: string): Observable<PdfGenerateResponse> {
    return this.http.post<PdfGenerateResponse>(
      `${this.base}/offers/${offerId}/generate-pdf`,
      { templateId }
    );
  }

  getAnalysis(offerId: string): Observable<any> {
    return this.http.get(`${this.base}/offers/${offerId}/analysis`);
  }

  downloadCv(offerId: string): Observable<Blob> {
    return this.http.get(`${this.base}/cv/${offerId}/download`, {
      responseType: 'blob'
    });
  }
  resumePipeline(offerId: string, templateId: number): Observable<any> {
    return new Observable(observer => {
      let cancelled = false;
      let timerId: number | undefined;

      const poll = (attempt = 1) => {
        if (cancelled) return;

        this.getAnalysis(offerId).pipe(
          timeout(12000)
        ).subscribe({
          next: (analysis: any) => {
            const cvData = analysis?.cvGeneratedContent ?? analysis?.cv_data ?? analysis?.cvData;

            if (cvData) {
              observer.next(analysis);
              observer.complete();
              return;
            }

            if (attempt >= 12) {
              observer.next({
                ...analysis,
                _cvPending: true,
              });
              observer.complete();
              return;
            }

            timerId = window.setTimeout(() => poll(attempt + 1), 2000);
          },
          error: (err) => {
            if (attempt >= 12) {
              observer.next({
                _cvPending: true,
              });
              observer.complete();
              return;
            }
            timerId = window.setTimeout(() => poll(attempt + 1), 2000);
          }
        });
      };

      const sub = this.http.post(`${this.base}/offers/${offerId}/resume`, { templateId }).pipe(
        timeout(12000)
      ).subscribe({
        next: (res: any) => {
          poll();
        },
        error: (err) => observer.error(err)
      });

      return () => {
        cancelled = true;
        sub.unsubscribe();
        if (timerId) window.clearTimeout(timerId);
      };
    });
  }

  getCvDraft(offerId: string): Observable<CvDraftResponse> {
    return this.http.get<CvDraftResponse>(`${this.base}/offers/${offerId}/cv-draft`);
  }

  saveCvDraft(offerId: string, draft: any): Observable<CvDraftResponse> {
    return this.http.patch<CvDraftResponse>(`${this.base}/offers/${offerId}/cv-draft`, draft);
  }

  getCvHistory(): Observable<CvHistoryItem[]> {
    return this.http.get<CvHistoryItem[]>(`${this.base}/cv/history`);
  }

  getCvTemplates(): Observable<CvTemplateDto[]> {
    return this.http.get<CvTemplateDto[]>(`${this.base}/cv/templates`);
  }

  getCvDownloadUrl(historyId: string): Observable<{ downloadUrl: string }> {
    return this.http.get<{ downloadUrl: string }>(`${this.base}/cv/${historyId}/download`);
  }

  downloadCvHistoryFile(historyId: string): Observable<Blob> {
    return this.http.get(`${this.base}/cv/${historyId}/download-file`, {
      responseType: 'blob'
    });
  }

  previewCv(templateSlug: string, offerId?: string | null): Observable<CvPreviewResponse> {
    const params = new URLSearchParams();
    params.set('template', templateSlug);
    if (offerId) params.set('offerId', offerId);
    return this.http.post<CvPreviewResponse>(`${this.base}/cv/preview?${params.toString()}`, {});
  }

  renderCvPreview(request: CvRenderRequest): Observable<CvRenderResponse> {
    return this.http.post<CvRenderResponse>(`${this.base}/cv/preview/render`, request);
  }

  exportCvPdf(request: CvExportPdfRequest): Observable<Blob> {
    return this.http.post(`${this.base}/cv/export/pdf`, request, {
      responseType: 'blob'
    });
  }

  saveFinalCv(payload: SaveFinalCvRequest): Observable<CvSaveResponse> {
    return this.http.post<CvSaveResponse>(`${this.base}/cv/save`, payload);
  }

  sendApplicationEmail(payload: SendApplicationEmailRequest): Observable<EmailDraftResponse> {
    return this.http.post<EmailDraftResponse>(`${this.base}/emails/send`, payload);
  }
}
