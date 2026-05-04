import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

// ── DTOs matching backend ────────────────────────────────────────────────────

export interface CandidatureDto {
  idCandidature: string;
  idUtilisateur: string;
  idOffre: string;
  dateCreation: string;
  inclureLettreMotivation: boolean;
  statut: string;
}

export interface CreateCandidaturePayload {
  idOffre: string;
  inclureLettreMotivation?: boolean;
}

// ── Service ──────────────────────────────────────────────────────────────────

@Injectable({ providedIn: 'root' })
export class CandidatureService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBaseUrl;

  getMyCandidatures(): Observable<CandidatureDto[]> {
    return this.http.get<CandidatureDto[]>(`${this.base}/candidatures`);
  }

  getById(id: string): Observable<CandidatureDto> {
    return this.http.get<CandidatureDto>(`${this.base}/candidatures/${id}`);
  }

  create(payload: CreateCandidaturePayload): Observable<CandidatureDto> {
    return this.http.post<CandidatureDto>(`${this.base}/candidatures`, payload);
  }
}
