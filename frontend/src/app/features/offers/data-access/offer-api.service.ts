import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpContext } from '@angular/common/http';
import { Observable, timeout } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { apiUrl } from '@core/http/api-url';
import { SUPPRESS_ERROR_TOAST } from '@core/http/api-error.interceptor';
import {
  OfferAnalysisResponse,
  OfferHistoryItem,
  OfferSubmitPayload,
  OfferSubmitResponse,
  ResumePipelineResponse,
} from './offers.models';

/**
 * Distinguishes "the agents have run" from "only the raw offer is stored".
 * Until the analysis is saved the offer carries a zero match score and no
 * required skills, both of which are filled in by the pipeline.
 */
function hasAnalysis(analysis: any): boolean {
  if (!analysis) return false;
  if (Array.isArray(analysis.competencesRequises) && analysis.competencesRequises.length > 0) return true;
  return typeof analysis.scoreMatching === 'number' && analysis.scoreMatching > 0;
}

/** Poll cadence and give-up budget for the fire-and-forget analysis (150 x 2s = 5 min). */
const ANALYSIS_POLL_INTERVAL_MS = 2000;
const ANALYSIS_POLL_MAX_ATTEMPTS = 150;

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

  /**
   * Resolves with null while the analysis has not been produced yet (the agents store
   * their result only when they are done) and throws when the offer is unknown or
   * belongs to somebody else. Callers poll this, so the global error toast is
   * suppressed: "not ready" is expected here, not a failure worth reporting.
   */
  getAnalysis(offerId: string): Observable<OfferAnalysisResponse | null> {
    return this.http.get<OfferAnalysisResponse | null>(`${this.base}/${offerId}/analysis`, {
      context: new HttpContext().set(SUPPRESS_ERROR_TOAST, true),
    });
  }

  bulkDeleteOffers(offerIds: string[]): Observable<{ deletedCount: number }> {
    return this.http.post<{ deletedCount: number }>(`${this.base}/delete`, { offerIds }).pipe(
      catchError(() => this.http.post<{ deletedCount: number }>(`${this.base}/bulk-delete`, { offerIds }))
    );
  }

  /**
   * Step 1 — Fire ONLY the 3 agents (Offer Analysis + Profile Retriever + Skill Gap).
   * POST /api/offers/{id}/analyze-sync
   *
   * The endpoint returns 202 immediately and the analysis runs in the background: the three
   * agents run in sequence and can take minutes, which is longer than the idle timeout of a
   * reverse proxy, so holding the request open would drop the connection mid-run. The
   * analysis is stored on the offer, so it is polled until the new run lands. SignalR
   * progress is best-effort here: the group is only joined from the generation step on.
   */
  analyzeSync(offerId: string, templateId: number = 1): Observable<OfferAnalysisResponse> {
    return new Observable<OfferAnalysisResponse>(observer => {
      let cancelled = false;
      let timerId: number | undefined;

      // A previous run may already have stored an analysis on this offer, so waiting for
      // "an analysis exists" would return that stale one straight away. The baseline is
      // captured before the new run starts and the poll waits for a result that is not it.
      // runId identifies the run exactly; the content signature is only a fallback for
      // results stored before runId existed, and for a re-run that returns the very same
      // score (where the outcome is identical anyway, so returning it is correct).
      let baselineRunId: string | null = null;
      let baselineSignature: string | null = null;

      const signature = (a: OfferAnalysisResponse | null): string => {
        if (!a) return '';
        return JSON.stringify([a.scoreMatching, a.competencesRequises?.length, a.keywordsPresents?.length]);
      };

      const isFreshRun = (a: OfferAnalysisResponse): boolean => {
        if (a.runId) return a.runId !== baselineRunId;
        return signature(a) !== baselineSignature;
      };

      let startSub: { unsubscribe(): void } | null = null;
      let pollSub: { unsubscribe(): void } | null = null;

      const poll = (attempt = 1) => {
        if (cancelled) return;

        pollSub?.unsubscribe();
        pollSub = this.getAnalysis(offerId).pipe(timeout(12000)).subscribe({
          next: (analysis) => {
            if (cancelled) return;

            if (analysis && hasAnalysis(analysis) && isFreshRun(analysis)) {
              observer.next(analysis);
              observer.complete();
              return;
            }

            if (attempt >= ANALYSIS_POLL_MAX_ATTEMPTS) {
              observer.error(new Error('Analysis timed out. Please try again.'));
              return;
            }

            timerId = window.setTimeout(() => poll(attempt + 1), ANALYSIS_POLL_INTERVAL_MS);
          },
          error: () => {
            if (cancelled) return;

            if (attempt >= ANALYSIS_POLL_MAX_ATTEMPTS) {
              observer.error(new Error('Analysis timed out. Please try again.'));
              return;
            }

            timerId = window.setTimeout(() => poll(attempt + 1), ANALYSIS_POLL_INTERVAL_MS);
          },
        });
      };

      const start = () => {
        if (cancelled) return;
        startSub = this.http
          .post<{ offerId: string; status: string }>(`${this.base}/${offerId}/analyze-sync`, { templateId })
          .pipe(timeout(12000))
          .subscribe({
            next: () => poll(),
            error: err => observer.error(err),
          });
      };

      const sub = this.getAnalysis(offerId).pipe(timeout(12000)).subscribe({
        next: (before) => {
          baselineRunId = before?.runId ?? null;
          baselineSignature = signature(before);
          start();
        },
        // Nothing to compare against (offer not readable yet): just launch.
        error: () => start(),
      });

      return () => {
        cancelled = true;
        sub.unsubscribe();
        startSub?.unsubscribe();
        pollSub?.unsubscribe();
        if (timerId) window.clearTimeout(timerId);
      };
    });
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
