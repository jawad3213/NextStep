import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  EmailDraftDto,
  GenerateDraftPayload,
  GenerateFollowUpDraftPayload,
  GenerateReplyDraftPayload,
  UpdateDraftPayload,
  SendEmailResultDto,
  EmailConnectionStatusDto,
  SaveGoogleClientCredentialsPayload,
  GoogleClientCredentialsSummaryDto,
  SendApplicationEmailRequest,
} from './email.models';
import { API_BASE_URL } from '@core/http/api-url';

// ── Service ──────────────────────────────────────────────────────────────────

@Injectable({ providedIn: 'root' })
export class EmailService {
  private readonly http = inject(HttpClient);
  private readonly base = API_BASE_URL;

  /** Sends the application email with the final CV attached (Gmail, SMTP fallback). */
  sendApplicationEmail(payload: SendApplicationEmailRequest): Observable<EmailDraftDto> {
    return this.http.post<EmailDraftDto>(`${this.base}/emails/send`, payload);
  }

  // Email Drafts
  getDraftsByCandidature(candidatureId: string): Observable<EmailDraftDto[]> {
    return this.http.get<EmailDraftDto[]>(`${this.base}/emails/candidature/${candidatureId}`);
  }

  generateDraft(payload: GenerateDraftPayload): Observable<EmailDraftDto> {
    return this.http.post<EmailDraftDto>(`${this.base}/emails/generate`, payload);
  }

  generateFollowUpDraft(payload: GenerateFollowUpDraftPayload): Observable<EmailDraftDto> {
    return this.http.post<EmailDraftDto>(`${this.base}/emails/generate-follow-up`, payload);
  }

  generateReplyDraft(payload: GenerateReplyDraftPayload): Observable<EmailDraftDto> {
    return this.http.post<EmailDraftDto>(`${this.base}/emails/generate-reply`, payload);
  }

  getDraftById(draftId: string): Observable<EmailDraftDto> {
    return this.http.get<EmailDraftDto>(`${this.base}/emails/drafts/${draftId}`);
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

  disconnectGmail(): Observable<void> {
    return this.http.delete<void>(`${this.base}/email-connections`);
  }

  verifyGmailConnection(): Observable<EmailConnectionStatusDto> {
    return this.http.post<EmailConnectionStatusDto>(`${this.base}/email-connections/verify`, {});
  }

  saveGoogleClientCredentials(payload: SaveGoogleClientCredentialsPayload): Observable<void> {
    return this.http.post<void>(`${this.base}/email-connections/google/credentials`, payload);
  }

  getGoogleClientCredentialsSummary(): Observable<GoogleClientCredentialsSummaryDto> {
    return this.http.get<GoogleClientCredentialsSummaryDto>(`${this.base}/email-connections/google/credentials`);
  }

  deleteGoogleClientCredentials(): Observable<void> {
    return this.http.delete<void>(`${this.base}/email-connections/google/credentials`);
  }
}
