import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

// ── DTOs matching backend OfferAnalysisDto ───────────────────────────────────

export interface OfferDto {
  offerId: string;
  titre: string;
  entreprise: string | null;
  typeContrat: string | null;
  localisation: string | null;
  descriptionPoste: string | null;
  anneesExperience: number | null;
  niveauEtudes: string | null;
  competencesRequises: string[];
  competencesSouhaitees: string[];
  keywordsAts: string[];
  scoreMatching: number;
  scoreAts: number;
  recommandations: string[];
  dateAnalyse: string;
}

export interface OfferHistoryItemDto {
  offerId: string;
  titre: string;
  entreprise: string;
  localisation: string;
  scoreMatching?: number | null;
  status: string;
  currentStep: number;
  dateCreation: string;
}

export interface OfferSubmitDto {
  rawText: string;
  titre?: string;
  entreprise?: string;
  templateId: string;
}

@Injectable({ providedIn: 'root' })
export class OfferService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBaseUrl;

  getMyOffers(): Observable<OfferDto[]> {
    return this.http.get<OfferDto[]>(`${this.base}/offers`);
  }

  getMyOfferHistory(): Observable<OfferHistoryItemDto[]> {
    return this.http.get<OfferHistoryItemDto[]>(`${this.base}/offers`);
  }

  getOfferById(id: string): Observable<OfferDto> {
    return this.http.get<OfferDto>(`${this.base}/offers/${id}/analysis`);
  }

  submitOffer(dto: OfferSubmitDto): Observable<OfferDto> {
    return this.http.post<OfferDto>(`${this.base}/offers/submit`, dto);
  }
}
