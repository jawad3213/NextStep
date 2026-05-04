import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

// ── DTOs matching backend ────────────────────────────────────────────────────

export interface EmailDraftDto {
  id: string;
  candidatureId: string;
  emailType: string;
  recipientEmail: string | null;
  subject: string;
  body: string;
  language: string;
  isApproved: boolean;
  isSent: boolean;
  createdAtUtc: string;
  updatedAtUtc: string | null;
  approvedAtUtc: string | null;
  sentAtUtc: string | null;
  errorMessage: string | null;
  providerMessageId: string | null;
  sendAttemptCount: number;
}

export interface GenerateDraftPayload {
  candidatureId: string;
  emailType: string;
  language: string;
}

export interface UpdateDraftPayload {
  recipientEmail?: string;
  subject?: string;
  body?: string;
}

export interface SendEmailResultDto {
  success: boolean;
  draftId: string;
  providerMessageId: string | null;
  errorMessage: string | null;
  sentAtUtc: string | null;
}

export interface EmailConnectionStatusDto {
  isConnected: boolean;
  emailAddress: string | null;
  provider: string;
}

// ── Service ──────────────────────────────────────────────────────────────────

@Injectable({ providedIn: 'root' })
export class EmailService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBaseUrl;

  // Email Drafts
  getDraftsByCandidature(candidatureId: string): Observable<EmailDraftDto[]> {
    return this.http.get<EmailDraftDto[]>(`${this.base}/emails/candidature/${candidatureId}`);
  }

  generateDraft(payload: GenerateDraftPayload): Observable<EmailDraftDto> {
    return this.http.post<EmailDraftDto>(`${this.base}/emails/generate`, payload);
  }

  updateDraft(draftId: string, payload: UpdateDraftPayload): Observable<EmailDraftDto> {
    return this.http.put<EmailDraftDto>(`${this.base}/emails/drafts/${draftId}`, payload);
  }

  approveDraft(draftId: string): Observable<EmailDraftDto> {
    return this.http.post<EmailDraftDto>(`${this.base}/emails/drafts/${draftId}/approve`, {});
  }

  sendDraft(draftId: string): Observable<SendEmailResultDto> {
    return this.http.post<SendEmailResultDto>(`${this.base}/emails/drafts/${draftId}/send`, {});
  }

  // Gmail Connection
  getGmailStatus(): Observable<EmailConnectionStatusDto> {
    return this.http.get<EmailConnectionStatusDto>(`${this.base}/email-connections/status`);
  }

  getGmailLoginUrl(): Observable<{ url: string }> {
    return this.http.get<{ url: string }>(`${this.base}/email-connections/google/login-url`);
  }
}
