import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
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

export interface CvDraftResponse {
  offerId: string;
  data: any;
  version: number;
  updatedAtUtc: string;
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

  submitOffer(payload: OfferSubmitPayload): Observable<OfferSubmitResponse> {
    return this.http.post<OfferSubmitResponse>(
      `${this.base}/offers/submit`,
      payload
    );
  }

  getOffersHistory(): Observable<OfferHistoryItem[]> {
    return this.http.get<OfferHistoryItem[]>(`${this.base}/offers`);
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
    console.log('[CV-PIPELINE] API resumePipeline request', { offerId, templateId, endpoint: `${this.base}/offers/${offerId}/resume` });
    return new Observable(observer => {
      let cancelled = false;
      let timerId: number | undefined;

      const poll = (attempt = 1) => {
        if (cancelled) return;

        this.getAnalysis(offerId).subscribe({
          next: (analysis: any) => {
            const cvData = analysis?.cvGeneratedContent ?? analysis?.cv_data ?? analysis?.cvData;
            console.log('[CV-PIPELINE] API resumePipeline poll', {
              attempt,
              hasCvData: !!cvData,
              keys: analysis && typeof analysis === 'object' ? Object.keys(analysis) : [],
            });

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

      const sub = this.http.post(`${this.base}/offers/${offerId}/resume`, { templateId }).subscribe({
        next: (res: any) => {
          console.log('[CV-PIPELINE] API resumePipeline accepted', {
            status: res?.status,
            keys: res && typeof res === 'object' ? Object.keys(res) : [],
          });
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

  getCvDownloadUrl(historyId: string): Observable<{ downloadUrl: string }> {
    return this.http.get<{ downloadUrl: string }>(`${this.base}/cv/${historyId}/download`);
  }

  downloadCvHistoryFile(historyId: string): Observable<Blob> {
    return this.http.get(`${this.base}/cv/${historyId}/download-file`, {
      responseType: 'blob'
    });
  }

  renderCvPreview(templateSlug: string, data: any): Observable<Blob> {
    return this.http.post(`${this.base}/cv/preview/render?template=${encodeURIComponent(templateSlug)}`, data, {
      responseType: 'blob'
    });
  }

  saveFinalCv(templateSlug: string, title: string, data: any): Observable<CvSaveResponse> {
    return this.http.post<CvSaveResponse>(`${this.base}/cv/save`, {
      templateSlug,
      title,
      data
    });
  }
}
