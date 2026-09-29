// ── API: /api/emails and /api/email-connections ─────────────────────────────

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
  tone?: string;
  cvHistoryId?: string | null;
}

export interface GenerateFollowUpDraftPayload {
  candidatureId: string;
  language: string;
  tone: string;
}

export interface GenerateReplyDraftPayload {
  candidatureId: string;
  language: string;
  tone: string;
  userInstructions?: string;
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
  /** Sending failed because Gmail must be reconnected (Settings > Gmail). */
  needsReconnect?: boolean;
  sentAtUtc: string | null;
}

export interface EmailConnectionStatusDto {
  isConnected: boolean;
  isTokenValid: boolean;
  hasCustomClientCredentials?: boolean;
  /** The user must reconnect Gmail (false for temporary problems). */
  needsReconnect?: boolean;
  errorMessage: string | null;
  emailAddress: string | null;
  provider: string;
}

export interface SaveGoogleClientCredentialsPayload {
  clientId: string;
  clientSecret: string;
  redirectUri?: string | null;
}

export interface GoogleClientCredentialsSummaryDto {
  hasCredentials: boolean;
  clientIdMasked: string | null;
  usesCustomRedirectUri: boolean;
  redirectUri: string | null;
  updatedAtUtc: string | null;
}

export interface SendApplicationEmailRequest {
  offerId: string;
  cvHistoryId?: string | null;
  recipientEmail: string;
  subject: string;
  body: string;
  emailType?: string;
  language?: string;
}
