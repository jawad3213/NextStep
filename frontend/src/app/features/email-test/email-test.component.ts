import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-email-test',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './email-test.component.html',
  styleUrls: []
})
export class EmailTestComponent {
  private http = inject(HttpClient);
  private baseUrl = environment.apiBaseUrl;

  candidatureId = '';
  recipientEmail = '';
  subject = '';
  body = '';
  emailType = 'application';
  language = 'fr';

  currentDraftId: string | null = null;
  isApproved = false;
  isSent = false;
  providerMessageId: string | null = null;
  errorMessage: string | null = null;
  sentAtUtc: string | null = null;

  rawResponse: string = '';

  checkGmailStatus() {
    this.http.get<any>(`${this.baseUrl}/email-connections/status`).subscribe({
      next: (res) => {
        this.rawResponse = JSON.stringify(res, null, 2);
      },
      error: (err) => {
        this.rawResponse = JSON.stringify(err, null, 2);
      }
    });
  }

  connectGmail() {
    this.http.get<{url: string}>(`${this.baseUrl}/email-connections/google/login-url`).subscribe({
      next: (res) => {
        if (res.url) {
          window.location.href = res.url;
        }
      },
      error: (err) => {
        this.rawResponse = JSON.stringify(err, null, 2);
      }
    });
  }

  generateDraft() {
    const cid = this.candidatureId.trim();
    if (!cid) {
      this.rawResponse = 'Error: candidatureId is required.';
      return;
    }
    const payload = {
      candidatureId: cid,
      emailType: this.emailType,
      language: this.language
    };
    this.rawResponse = 'Sending: ' + JSON.stringify(payload, null, 2);
    this.http.post<any>(`${this.baseUrl}/emails/generate`, payload).subscribe({
      next: (res) => {
        this.updateStateFromDraft(res);
        this.rawResponse = JSON.stringify(res, null, 2);
      },
      error: (err) => {
        this.rawResponse = JSON.stringify(err, null, 2);
      }
    });
  }

  loadDrafts() {
    const cid = this.candidatureId.trim();
    if (!cid) {
      this.rawResponse = 'Please enter a candidatureId';
      return;
    }
    this.http.get<any[]>(`${this.baseUrl}/emails/candidature/${cid}`).subscribe({
      next: (res) => {
        if (res && res.length > 0) {
          // just load the first one for test
          this.updateStateFromDraft(res[0]);
        }
        this.rawResponse = JSON.stringify(res, null, 2);
      },
      error: (err) => {
        this.rawResponse = JSON.stringify(err, null, 2);
      }
    });
  }

  saveDraft() {
    if (!this.currentDraftId) return;
    const payload = {
      recipientEmail: this.recipientEmail,
      subject: this.subject,
      body: this.body
    };
    this.http.put<any>(`${this.baseUrl}/emails/drafts/${this.currentDraftId}`, payload).subscribe({
      next: (res) => {
        this.updateStateFromDraft(res);
        this.rawResponse = JSON.stringify(res, null, 2);
      },
      error: (err) => {
        this.rawResponse = JSON.stringify(err, null, 2);
      }
    });
  }

  approveDraft() {
    if (!this.currentDraftId) return;
    this.http.post<any>(`${this.baseUrl}/emails/drafts/${this.currentDraftId}/approve`, {}).subscribe({
      next: (res) => {
        this.updateStateFromDraft(res);
        this.rawResponse = JSON.stringify(res, null, 2);
      },
      error: (err) => {
        this.rawResponse = JSON.stringify(err, null, 2);
      }
    });
  }

  sendDraft() {
    if (!this.currentDraftId) return;
    this.http.post<any>(`${this.baseUrl}/emails/drafts/${this.currentDraftId}/send`, {}).subscribe({
      next: (res) => {
        this.rawResponse = JSON.stringify(res, null, 2);
        if (res.success) {
          this.isSent = true;
          this.sentAtUtc = res.sentAtUtc;
          this.providerMessageId = res.providerMessageId;
        } else {
          this.errorMessage = res.errorMessage;
        }
      },
      error: (err) => {
        this.rawResponse = JSON.stringify(err, null, 2);
      }
    });
  }

  private updateStateFromDraft(draft: any) {
    this.currentDraftId = draft.id;
    this.recipientEmail = draft.recipientEmail;
    this.subject = draft.subject;
    this.body = draft.body;
    this.isApproved = draft.isApproved;
    this.isSent = draft.isSent;
    this.providerMessageId = draft.providerMessageId;
    this.errorMessage = draft.errorMessage;
    this.sentAtUtc = draft.sentAtUtc;
  }
}
