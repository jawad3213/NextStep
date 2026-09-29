import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { apiUrl } from '@core/http/api-url';

/** Offer context sent with a company analysis (snake_case fields go to the Python agent as-is). */
export interface CompanyAnalysisRequest {
  company_name: string;
  user_id?: string | number;
  profile_data?: unknown;
  offer_data?: unknown;
  [key: string]: unknown;
}

/** Raw company-intelligence payload returned by the agent (mapped by the pages). */
// eslint-disable-next-line @typescript-eslint/no-explicit-any -- shape defined by the Python agent
export type CompanyAnalysisResponse = any;

/** Company intelligence through the backend proxy to the AI agents. */
@Injectable({ providedIn: 'root' })
export class CompanyIntelApiService {
  private readonly http = inject(HttpClient);

  analyzeCompany(payload: CompanyAnalysisRequest): Observable<CompanyAnalysisResponse> {
    return this.http.post<CompanyAnalysisResponse>(apiUrl('/agents/company/analyze'), payload);
  }
}
