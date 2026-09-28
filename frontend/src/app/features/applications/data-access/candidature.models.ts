// ── API: /api/candidatures ──────────────────────────────────────────────────

export interface CandidatureNoteDto {
  id: string;
  candidatureId: string;
  contenu: string;
  auteur: string;
  createdAt: string;
}

export interface CandidatureStatusHistoryDto {
  id: string;
  candidatureId: string;
  ancienStatut?: string;
  nouveauStatut: string;
  source: string;
  details?: string;
  createdAt: string;
}

export interface CandidatureDto {
  idCandidature: string;
  idUtilisateur: string;
  idOffre: string;
  dateCreation: string;
  inclureLettreMotivation: boolean;
  statut: string;

  // ── Multi-channel tracking ────────────────────────────────────────────────────
  channel: string;
  channelUrl?: string;
  channelContact?: string;
  applicationDate: string;
  appliedManually: boolean;
  offerSource?: string;
  notes?: string;
  language: string;

  // ── Email reply tracking ──────────────────────────────────────────────────────
  responseStatus: string;
  hasResponse: boolean;
  lastCheckedAtUtc?: string;
  lastResponseAtUtc?: string;

  // ── AI Classification ─────────────────────────────────────────────────────────
  lastResponseFrom?: string;
  lastResponseSnippet?: string;
  responseSummary?: string;
  recommendedAction?: string;
  responseConfidence?: number;
  responseClassifiedAtUtc?: string;

  // ── Follow-up tracking ────────────────────────────────────────────────────────
  followUpNeeded?: boolean;
  lastFollowUpAtUtc?: string;

  // ── Notes & History ───────────────────────────────────────────────────────────
  candidatureNotes?: CandidatureNoteDto[];
  statusHistoryEntries?: CandidatureStatusHistoryDto[];
}

export interface CreateCandidaturePayload {
  idOffre?: string;
  entreprise?: string;
  poste?: string;
  channel: string;
  channelUrl?: string;
  channelContact?: string;
  applicationDate?: string;
  appliedManually?: boolean;
  inclureLettreMotivation?: boolean;
  language?: string;
  offerSource?: string;
  notes?: string;
}

export interface UpdateCandidaturePayload {
  channel?: string;
  channelUrl?: string;
  channelContact?: string;
  notes?: string;
  language?: string;
  inclureLettreMotivation?: boolean;
}

export interface UpdateStatutPayload {
  nouveauStatut: string;
  details?: string;
}

export interface AddNotePayload {
  contenu: string;
  auteur?: string;
}


