import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { PagedResponse } from '@core/http/paged-response';
import { Observable, catchError, map } from 'rxjs';
import {
  CandidatureNoteDto,
  CandidatureStatusHistoryDto,
  CandidatureDto,
  CreateCandidaturePayload,
  UpdateCandidaturePayload,
  UpdateStatutPayload,
  AddNotePayload,
} from './candidature.models';
import { API_BASE_URL } from '@core/http/api-url';

// ── Service ──────────────────────────────────────────────────────────────────

@Injectable({ providedIn: 'root' })
export class CandidatureService {
  private readonly http = inject(HttpClient);
  private readonly base = API_BASE_URL;

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

  updateStatut(id: string, payload: UpdateStatutPayload): Observable<CandidatureDto> {
    return this.http.patch<CandidatureDto>(`${this.base}/candidatures/${id}/statut`, payload);
  }

  updateCandidature(id: string, payload: UpdateCandidaturePayload): Observable<CandidatureDto> {
    return this.http.put<CandidatureDto>(`${this.base}/candidatures/${id}`, payload);
  }

  deleteCandidature(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/candidatures/${id}`);
  }

  addNote(id: string, payload: AddNotePayload): Observable<CandidatureNoteDto> {
    return this.http.post<CandidatureNoteDto>(`${this.base}/candidatures/${id}/notes`, payload);
  }

  getNotes(id: string): Observable<CandidatureNoteDto[]> {
    return this.http.get<CandidatureNoteDto[]>(`${this.base}/candidatures/${id}/notes`);
  }

  getHistory(id: string): Observable<CandidatureStatusHistoryDto[]> {
    return this.http.get<CandidatureStatusHistoryDto[]>(`${this.base}/candidatures/${id}/history`);
  }
}
