import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, map } from 'rxjs';
import { environment } from '../../environments/environment';

// ── DTOs matching backend ────────────────────────────────────────────────────

export interface CandidatureDto {
  idCandidature: string;
  idUtilisateur: string;
  idOffre: string;
  dateCreation: string;
  inclureLettreMotivation: boolean;
  statut: string;
  responseStatus: string;
  hasResponse: boolean;
  lastCheckedAtUtc?: string;
  lastResponseAtUtc?: string;

  // ── AI Classification ─────────────────────────────────────────────────────────
  lastResponseFrom?: string;
  lastResponseSnippet?: string;
  responseSummary?: string;
  recommendedAction?: string;
  responseConfidence?: number;
  responseClassifiedAtUtc?: string;

  // ── Follow-up tracking (set by Hangfire DetectFollowUpNeededJob) ──────────────
  followUpNeeded?: boolean;
  lastFollowUpAtUtc?: string;
}

export interface CreateCandidaturePayload {
  idOffre: string;
  inclureLettreMotivation?: boolean;
}

export interface PagedResponse<T> {
  offset: number;
  limit: number;
  total: number;
  hasMore: boolean;
  items: T[];
}

// ── Service ──────────────────────────────────────────────────────────────────

@Injectable({ providedIn: 'root' })
export class CandidatureService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBaseUrl;

  getMyCandidatures(): Observable<CandidatureDto[]> {
    return this.http.get<CandidatureDto[]>(`${this.base}/candidatures`);
  }

  getMyCandidaturesPaged(offset = 0, limit = 10, interviewOnly = false): Observable<PagedResponse<CandidatureDto>> {
    return this.http
      .get<PagedResponse<CandidatureDto>>(
        `${this.base}/candidatures/paged?offset=${offset}&limit=${limit}&interviewOnly=${interviewOnly}`
      )
      .pipe(
        catchError(() =>
          this.getMyCandidatures().pipe(
            map((all) => {
              const filtered = interviewOnly
                ? all.filter((c) => {
                    const status = `${c.statut ?? ''} ${c.responseStatus ?? ''}`.toUpperCase();
                    return status.includes('ENTRETIEN');
                  })
                : all;

              const sorted = [...filtered].sort(
                (a, b) =>
                  new Date(b.lastResponseAtUtc ?? b.dateCreation).getTime() -
                  new Date(a.lastResponseAtUtc ?? a.dateCreation).getTime()
              );

              const safeOffset = Math.max(0, offset);
              const safeLimit = Math.max(1, limit);
              const items = sorted.slice(safeOffset, safeOffset + safeLimit);
              const total = sorted.length;

              return {
                offset: safeOffset,
                limit: safeLimit,
                total,
                hasMore: safeOffset + items.length < total,
                items,
              } as PagedResponse<CandidatureDto>;
            })
          )
        )
      );
  }

  getById(id: string): Observable<CandidatureDto> {
    return this.http.get<CandidatureDto>(`${this.base}/candidatures/${id}`);
  }

  create(payload: CreateCandidaturePayload): Observable<CandidatureDto> {
    return this.http.post<CandidatureDto>(`${this.base}/candidatures`, payload);
  }
}
