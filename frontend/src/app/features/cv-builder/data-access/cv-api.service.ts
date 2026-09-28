import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { apiUrl } from '@core/http/api-url';
import { PagedResponse } from '@core/http/paged-response';
import {
  CvDraftResponse,
  CvExportPdfRequest,
  CvHistoryItem,
  CvRenderRequest,
  CvRenderResponse,
  CvSaveResponse,
  CvTemplateDto,
  SaveFinalCvRequest,
} from './cv.models';

/** CV documents: templates, drafts, previews, PDFs and saved CVs (/api/cv). */
@Injectable({ providedIn: 'root' })
export class CvApiService {
  private readonly http = inject(HttpClient);
  private readonly base = apiUrl('/cv');

  // ── Templates ──────────────────────────────────────────────────────────────

  getCvTemplates(): Observable<CvTemplateDto[]> {
    return this.http.get<CvTemplateDto[]>(`${this.base}/templates`);
  }

  // ── Draft of an offer's CV ─────────────────────────────────────────────────

  getCvDraft(offerId: string): Observable<CvDraftResponse> {
    return this.http.get<CvDraftResponse>(apiUrl(`/offers/${offerId}/cv-draft`));
  }

  saveCvDraft(offerId: string, draft: unknown): Observable<CvDraftResponse> {
    return this.http.patch<CvDraftResponse>(apiUrl(`/offers/${offerId}/cv-draft`), draft);
  }

  // ── Rendering ──────────────────────────────────────────────────────────────

  renderCvPreview(request: CvRenderRequest): Observable<CvRenderResponse> {
    return this.http.post<CvRenderResponse>(`${this.base}/preview/render`, request);
  }

  exportCvPdf(request: CvExportPdfRequest): Observable<Blob> {
    return this.http.post(`${this.base}/export/pdf`, request, { responseType: 'blob' });
  }

  // ── Saved CVs ──────────────────────────────────────────────────────────────

  saveFinalCv(payload: SaveFinalCvRequest): Observable<CvSaveResponse> {
    return this.http.post<CvSaveResponse>(`${this.base}/save`, payload);
  }

  getCvHistory(): Observable<CvHistoryItem[]> {
    return this.http.get<CvHistoryItem[]>(`${this.base}/history`);
  }

  getCvHistoryPage(offset: number, limit: number): Observable<PagedResponse<CvHistoryItem>> {
    return this.http.get<PagedResponse<CvHistoryItem>>(`${this.base}/history/paged?offset=${offset}&limit=${limit}`);
  }

  downloadCvHistoryFile(historyId: string): Observable<Blob> {
    return this.http.get(`${this.base}/${historyId}/download-file`, { responseType: 'blob' });
  }

  deleteCv(historyId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${historyId}`);
  }
}
