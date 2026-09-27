import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  ProfileStatus,
  SoftOnboardingPayload,
} from '../core/auth/models/user-profile.model';
import { environment } from '../../environments/environment';

export interface OnboardingStatus {
  /** The onboarding questions (objective, level, sector) were answered. */
  onboardingCompleted: boolean;
  /** The profile passed the server-side completion check: the app is unlocked. */
  profileCompleted: boolean;
  profileScore: number;
  completionPercent: number;
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
  private readonly baseUrl = `${environment.apiBaseUrl}/identity`;

  constructor() {
    // Legacy browser-only unlock flags: access is now decided by the backend.
    localStorage.removeItem('nextstep_profile_unlocked');
    localStorage.removeItem('nextstep_soft_onboarding_done');
  }

  getStatus(): Observable<OnboardingStatus> {
    return this.http.get<OnboardingStatus>(`${this.baseUrl}/onboarding-status`);
  }

  /** Server-side "Finish": validates the profile (85% rule) and unlocks the application. */
  completeProfile(): Observable<CompleteProfileResult> {
    return this.http.post<CompleteProfileResult>(`${this.baseUrl}/complete-profile`, {});
  }

  submitSoftOnboarding(data: SoftOnboardingPayload): Observable<any> {
    return this.http.post(`${this.baseUrl}/soft-onboarding`, data);
  }

  getProfileStatus(): Observable<ProfileStatus> {
    return this.http.get<ProfileStatus>(`${this.baseUrl}/profile-status`);
  }
}
