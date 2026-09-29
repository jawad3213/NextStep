/*
 * Candidature lifecycle as the UI shows it: the backend statut grouped into board stages,
 * and the follow-up rule. Shared by the applications board and the dashboard.
 */

export type CandidatureStage =
  | 'brouillon' | 'envoye' | 'en-attente' | 'relance' | 'entretien' | 'test-tech' | 'accepte' | 'refuse';

export interface StageColumn {
  key: CandidatureStage;
  label: string;
  color: string;
}

export const KANBAN_COLUMNS: StageColumn[] = [
  { key: 'brouillon', label: 'Draft', color: '#9CA3AF' },
  { key: 'envoye', label: 'Sent', color: '#465fff' },
  { key: 'en-attente', label: 'Pending', color: '#F59B00' },
  { key: 'relance', label: 'Follow-up', color: '#F97316' },
  { key: 'entretien', label: 'Interview', color: '#7c3aed' },
  { key: 'test-tech', label: 'Tech Test', color: '#757575' },
  { key: 'accepte', label: 'Accepted', color: '#34A853' },
  { key: 'refuse', label: 'Rejected', color: '#D93025' },
];

/** A candidature with no reply is worth a follow-up after this many days. */
export const FOLLOW_UP_AFTER_DAYS = 5;

export function mapStatusToKanban(backendStatus: string): CandidatureStage {
  switch (backendStatus) {
    case 'BROUILLON': return 'brouillon';
    case 'ENVOYE': return 'envoye';
    case 'ACCUSE_RECEPTION': return 'envoye';
    case 'EN_COURS_EXAMEN': return 'en-attente';
    case 'RELANCE_NECESSAIRE': return 'relance';
    case 'RELANCE_ENVOYEE': return 'relance';
    case 'REPONSE_RECUE': return 'en-attente';
    case 'TEST_TECHNIQUE': return 'test-tech';
    case 'ENTRETIEN_PROPOSE': return 'entretien';
    case 'ENTRETIEN_EFFECTUE': return 'entretien';
    case 'OFFRE_RECUE': return 'accepte';
    case 'ACCEPTE': return 'accepte';
    case 'REFUSE': return 'refuse';
    case 'ABANDONNE': return 'refuse';
    default: return 'envoye';
  }
}

export function mapKanbanToStatus(col: string): string {
  switch (col) {
    case 'brouillon': return 'BROUILLON';
    case 'envoye': return 'ENVOYE';
    case 'en-attente': return 'EN_COURS_EXAMEN';
    case 'relance': return 'RELANCE_NECESSAIRE';
    case 'entretien': return 'ENTRETIEN_PROPOSE';
    case 'test-tech': return 'TEST_TECHNIQUE';
    case 'accepte': return 'ACCEPTE';
    case 'refuse': return 'REFUSE';
    default: return 'ENVOYE';
  }
}

export function stageColumn(stage: string): StageColumn {
  return KANBAN_COLUMNS.find(c => c.key === stage) ?? KANBAN_COLUMNS[1];
}

export function getDaysSince(dateStr?: string | null): number {
  if (!dateStr) return 0;
  const then = new Date(dateStr).getTime();
  if (isNaN(then)) return 0;
  const now = new Date().getTime();
  const diffDays = Math.floor((now - then) / (1000 * 60 * 60 * 24));
  return diffDays >= 0 ? diffDays : 0;
}

/** Sent (or under review), still no reply, and sent at least FOLLOW_UP_AFTER_DAYS ago. */
export function isFollowUpDue(stage: string, hasResponse: boolean, applicationDate?: string | null): boolean {
  if (stage !== 'en-attente' && stage !== 'envoye') return false;
  if (hasResponse) return false;
  return getDaysSince(applicationDate) >= FOLLOW_UP_AFTER_DAYS;
}
