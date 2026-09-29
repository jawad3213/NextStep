import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import {
  ProfileStatus,
  SoftOnboardingPayload,
} from './user-profile.model';
import { apiUrl } from '@core/http/api-url';

export interface OnboardingStatus {
  /** The onboarding questions (objective, level, sector) were answered. */
  onboardingCompleted: boolean;
  /** The profile passed the server-side completion check: the app is unlocked. */
  profileCompleted: boolean;
  profileScore: number;
  completionPercent: number;
}

/** POST /api/identity/soft-onboarding */
export interface SoftOnboardingResponse {
  message: string;
  data: { onboardingCompleted: boolean };
}

export interface CompleteProfileResult {
  succeeded: boolean;
  message: string;
  completionPercent: number;
  requiredPercent: number;
}

@Injectable({
  providedIn: 'root',
})
export class OnboardingService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = apiUrl('/identity');

  constructor() {
    // Legacy browser-only unlock flags: access is now decided by the backend.
    localStorage.removeItem('nextstep_profile_unlocked');
    localStorage.removeItem('nextstep_soft_onboarding_done');
  }

  /**
   * Latest status the server returned (set by every getStatus call, e.g. the route guard).
   * Lets a page render in the right mode immediately instead of guessing until it re-asks.
   */
  readonly lastStatus = signal<OnboardingStatus | null>(null);

  getStatus(): Observable<OnboardingStatus> {
    return this.http.get<OnboardingStatus>(`${this.baseUrl}/onboarding-status`).pipe(
      tap(status => this.lastStatus.set(status)),
    );
  }

  /** Server-side "Finish": validates the profile (85% rule) and unlocks the application. */
  completeProfile(): Observable<CompleteProfileResult> {
    return this.http.post<CompleteProfileResult>(`${this.baseUrl}/complete-profile`, {}).pipe(
      tap(result => {
        const current = this.lastStatus();
        if (result.succeeded && current) this.lastStatus.set({ ...current, profileCompleted: true });
      }),
    );
  }

  submitSoftOnboarding(data: SoftOnboardingPayload): Observable<SoftOnboardingResponse> {
    return this.http.post<SoftOnboardingResponse>(`${this.baseUrl}/soft-onboarding`, data);
  }

  getProfileStatus(): Observable<ProfileStatus> {
    return this.http.get<ProfileStatus>(`${this.baseUrl}/profile-status`);
  }
}
