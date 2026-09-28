import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, timeout } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { apiUrl } from '@core/http/api-url';
import {
  OfferAnalysisResponse,
  OfferHistoryItem,
  OfferSubmitPayload,
  OfferSubmitResponse,
  ResumePipelineResponse,
} from './offers.models';

/** Analysed job offers: /api/offers. */
@Injectable({ providedIn: 'root' })
export class OfferApiService {
  private readonly http = inject(HttpClient);
  private readonly base = apiUrl('/offers');

  submitOffer(payload: OfferSubmitPayload): Observable<OfferSubmitResponse> {
    return this.http.post<OfferSubmitResponse>(`${this.base}/submit`, payload);
  }

  getOffersHistory(): Observable<OfferHistoryItem[]> {
    return this.http.get<OfferHistoryItem[]>(this.base);
  }

  getAnalysis(offerId: string): Observable<OfferAnalysisResponse> {
    return this.http.get<OfferAnalysisResponse>(`${this.base}/${offerId}/analysis`);
  }

  bulkDeleteOffers(offerIds: string[]): Observable<{ deletedCount: number }> {
    return this.http.post<{ deletedCount: number }>(`${this.base}/delete`, { offerIds }).pipe(
      catchError(() => this.http.post<{ deletedCount: number }>(`${this.base}/bulk-delete`, { offerIds }))
    );
  }

  /**
   * Step 1 — Fire ONLY the 3 agents (Offer Analysis + Profile Retriever + Skill Gap)
   * POST /api/offers/{id}/analyze-sync
   */
  analyzeSync(offerId: string, templateId: number = 1): Observable<OfferAnalysisResponse> {
    return this.http.post<OfferAnalysisResponse>(`${this.base}/${offerId}/analyze-sync`, { templateId });
  }

  /**
   * Starts CV generation, then polls the analysis until the generated CV is available
   * (up to 12 attempts; emits the analysis with `_cvPending: true` if it is still missing).
   */
  resumePipeline(offerId: string, templateId: number): Observable<ResumePipelineResponse> {
    return new Observable<ResumePipelineResponse>(observer => {
      let cancelled = false;
      let timerId: number | undefined;

      const poll = (attempt = 1) => {
        if (cancelled) return;

        this.getAnalysis(offerId).pipe(timeout(12000)).subscribe({
          next: (analysis: any) => {
            const cvData = analysis?.cvGeneratedContent ?? analysis?.cv_data ?? analysis?.cvData;

            if (cvData) {
              observer.next(analysis);
              observer.complete();
              return;
            }

            if (attempt >= 12) {
              observer.next({ ...analysis, _cvPending: true });
              observer.complete();
              return;
            }

            timerId = window.setTimeout(() => poll(attempt + 1), 2000);
          },
          error: () => {
            if (attempt >= 12) {
              observer.next({ _cvPending: true });
              observer.complete();
              return;
            }
            timerId = window.setTimeout(() => poll(attempt + 1), 2000);
          },
        });
      };

      const sub = this.http.post(`${this.base}/${offerId}/resume`, { templateId }).pipe(timeout(12000)).subscribe({
        next: () => poll(),
        error: err => observer.error(err),
      });

      return () => {
        cancelled = true;
        sub.unsubscribe();
        if (timerId) window.clearTimeout(timerId);
      };
    });
  }
}
