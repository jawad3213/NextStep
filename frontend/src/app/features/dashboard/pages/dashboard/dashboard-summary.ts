import { CandidatureDto } from '@features/applications/data-access/candidature.models';
import {
  CandidatureStage,
  KANBAN_COLUMNS,
  StageColumn,
  getDaysSince,
  isFollowUpDue,
  mapStatusToKanban,
  stageColumn,
} from '@features/applications/data-access/candidature-status';
import { OfferHistoryItem } from '@features/offers/data-access/offers.models';

/*
 * What the dashboard shows, computed from the user's candidatures (+ offer titles).
 * Pure functions, no Angular. Stages follow the applications board.
 */

export interface DashboardApplication {
  id: string;
  company: string;
  role: string;
  /** When the application was sent (falls back to its creation date). */
  sentAt: string;
  stage: StageColumn;
  hasResponse: boolean;
  lastResponseAtUtc?: string;
  responseSummary?: string;
  followUpNeeded: boolean;
}

export interface DashboardKpis {
  total: number;
  waiting: number;
  /** Share of sent applications that got a reply, 0–100. */
  responseRate: number;
  interviews: number;
}

export type TodoKind = 'interview' | 'reply' | 'follow-up' | 'draft';

export interface TodoItem {
  kind: TodoKind;
  applicationId: string;
  title: string;
  detail: string;
  link: string[];
}

export interface PipelineSegment {
  column: StageColumn;
  count: number;
  /** Width of the segment in the bar, 0–100. */
  pct: number;
}

export interface PipelineSummary {
  total: number;
  segments: PipelineSegment[];
  repliesReceived: number;
  sent: number;
  replyRate: number;
}

const WAITING: CandidatureStage[] = ['envoye', 'en-attente', 'relance'];
const INTERVIEW: CandidatureStage[] = ['entretien', 'test-tech'];
const CLOSED: CandidatureStage[] = ['accepte', 'refuse'];
const TODO_ORDER: TodoKind[] = ['interview', 'reply', 'follow-up', 'draft'];
/** A reply stays in "À faire" for this many days. */
const RECENT_REPLY_DAYS = 7;

export function toDashboardApplications(candidatures: CandidatureDto[], offers: OfferHistoryItem[]): DashboardApplication[] {
  const offerById = new Map(offers.map(o => [o.offerId, o]));
  return candidatures
    .map(c => {
      const offer = offerById.get(c.idOffre ?? '');
      return {
        id: c.idCandidature,
        company: offer?.entreprise?.trim() || 'Company',
        role: offer?.titre?.trim() || 'Position',
        sentAt: c.applicationDate || c.dateCreation,
        stage: stageColumn(mapStatusToKanban(c.statut)),
        hasResponse: !!c.hasResponse,
        lastResponseAtUtc: c.lastResponseAtUtc,
        responseSummary: c.responseSummary,
        followUpNeeded: !!c.followUpNeeded,
      };
    })
    .sort((a, b) => toTimestamp(b.sentAt) - toTimestamp(a.sentAt));
}

export function buildKpis(apps: DashboardApplication[]): DashboardKpis {
  const pipeline = buildPipeline(apps);
  return {
    total: apps.length,
    waiting: apps.filter(a => WAITING.includes(a.stage.key)).length,
    responseRate: pipeline.replyRate,
    interviews: apps.filter(a => INTERVIEW.includes(a.stage.key)).length,
  };
}

/** Everything that needs the user's attention, most urgent first. */
export function buildTodos(apps: DashboardApplication[]): TodoItem[] {
  const todos: TodoItem[] = [];

  for (const app of apps) {
    const stage = app.stage.key;
    if (INTERVIEW.includes(stage)) {
      todos.push({
        kind: 'interview',
        applicationId: app.id,
        title: `Prepare interview – ${app.company}`,
        detail: app.role,
        link: ['/applications', app.id],
      });
    } else if (app.hasResponse && !CLOSED.includes(stage) && isRecent(app.lastResponseAtUtc, RECENT_REPLY_DAYS)) {
      todos.push({
        kind: 'reply',
        applicationId: app.id,
        title: `New reply – ${app.company}`,
        detail: app.responseSummary?.trim() || app.role,
        link: ['/applications', app.id],
      });
    } else if (isFollowUpDue(stage, app.hasResponse, app.sentAt) || (app.followUpNeeded && !app.hasResponse && WAITING.includes(stage))) {
      const days = getDaysSince(app.sentAt);
      todos.push({
        kind: 'follow-up',
        applicationId: app.id,
        title: `Follow up with ${app.company}`,
        detail: `Sent ${days} day${days > 1 ? 's' : ''} ago, no reply yet`,
        link: ['/letters', app.id],
      });
    } else if (stage === 'brouillon') {
      todos.push({
        kind: 'draft',
        applicationId: app.id,
        title: `Finalize application – ${app.company}`,
        detail: app.role,
        link: ['/letters', app.id],
      });
    }
  }

  return todos.sort((a, b) => TODO_ORDER.indexOf(a.kind) - TODO_ORDER.indexOf(b.kind));
}

export function buildPipeline(apps: DashboardApplication[]): PipelineSummary {
  const total = apps.length;
  const segments = KANBAN_COLUMNS
    .map(column => {
      const count = apps.filter(a => a.stage.key === column.key).length;
      return { column, count, pct: total ? (count / total) * 100 : 0 };
    })
    .filter(s => s.count > 0);

  const sent = apps.filter(a => a.stage.key !== 'brouillon').length;
  const repliesReceived = apps.filter(a => a.stage.key !== 'brouillon' && a.hasResponse).length;
  return {
    total,
    segments,
    repliesReceived,
    sent,
    replyRate: sent ? Math.round((repliesReceived / sent) * 100) : 0,
  };
}

/** "2 candidatures en cours · 1 action à faire" */
export function buildSummaryLine(apps: DashboardApplication[], todoCount: number): string {
  if (!apps.length) return 'Analyze an offer to start your first application.';
  const active = apps.filter(a => !CLOSED.includes(a.stage.key)).length;
  const activeText = `${active} application${active > 1 ? 's' : ''} in progress`;
  const todoText = todoCount
    ? `${todoCount} action${todoCount > 1 ? 's' : ''} to do`
    : 'all up to date';
  return `${activeText} · ${todoText}`;
}

export function companyInitials(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (!parts.length) return '?';
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return (parts[0][0] + parts[1][0]).toUpperCase();
}

function isRecent(dateStr: string | undefined, days: number): boolean {
  return !!dateStr && toTimestamp(dateStr) > 0 && getDaysSince(dateStr) <= days;
}

function toTimestamp(dateLike?: string): number {
  if (!dateLike) return 0;
  const ts = new Date(dateLike).getTime();
  return Number.isFinite(ts) ? ts : 0;
}
