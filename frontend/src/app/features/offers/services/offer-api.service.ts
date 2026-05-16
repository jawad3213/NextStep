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
  descriptionPoste: string | null;
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
    return this.http.post(`${this.base}/offers/${offerId}/resume`, { templateId });
  }
}
