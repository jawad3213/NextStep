import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

export interface OfferSubmitPayload {
  rawText: string;
  templateId: number;
}

export interface OfferSubmitResponse {
  offerId: string;
  status: string;
}

export interface PdfGeneratePayload {
  templateId: string;
}

export interface PdfGenerateResponse {
  offerId: string;
  downloadUrl: string;
  status: string;
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
    return this.http.post(`${this.base}/offers/${offerId}/resume`, { templateId });
  }
}
