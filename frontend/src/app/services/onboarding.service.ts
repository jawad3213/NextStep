import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  ProfileStatus,
  SoftOnboardingPayload,
} from '../core/auth/models/user-profile.model';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root',
})
export class OnboardingService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/identity`;

  getStatus(): Observable<{ onboardingCompleted: boolean; profileScore: number }> {
    return this.http.get<{ onboardingCompleted: boolean; profileScore: number }>(`${this.baseUrl}/onboarding-status`);
  }

  submitSoftOnboarding(data: SoftOnboardingPayload): Observable<any> {
    return this.http.post(`${this.baseUrl}/soft-onboarding`, data);
  }

  getProfileStatus(): Observable<ProfileStatus> {
    return this.http.get<ProfileStatus>(`${this.baseUrl}/profile-status`);
  }
}
