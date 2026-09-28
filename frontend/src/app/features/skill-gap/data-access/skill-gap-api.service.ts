import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { apiUrl } from '@core/http/api-url';

export interface SkillGapRequest {
  offerText: string;
}

export interface SkillGapResult {
  candidateName: string;
  jobTitle: string;
  /** 0–100 */
  relevanceScore: number;
  matchedSkills: { name: string; category: string }[];
  missingSkills: { name: string; category: string; priority: string }[];
  requiredCerts: string[];
  certMatch: boolean;
  experienceYears: number;
  requiredYears: number;
  experienceGapYears: number;
  flag: 'PERFECT' | 'MINOR' | 'CRITICAL';
  recommendations: { type: string; title: string; description: string; priority: string }[];
  revisionHints: string[];
}

/** Skill-gap analysis of a job offer against the candidate's stored profile. */
@Injectable({ providedIn: 'root' })
export class SkillGapApiService {
  private readonly http = inject(HttpClient);

  /** POST /api/offers/skill-gap: the backend analyses the offer and matches it with the profile. */
  match(request: SkillGapRequest): Observable<SkillGapResult> {
    return this.http.post<SkillGapResult>(apiUrl('/offers/skill-gap'), request);
  }
}
