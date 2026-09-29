/*
 * Rules of the applications board: contract type detection, dates, channel/badge display
 * and CSV export. Pure functions, no Angular.
 */

import { getDaysSince, isFollowUpDue } from '../../data-access/candidature-status';

// The status rules live in data-access (shared with the dashboard); re-exported for the board.
export { KANBAN_COLUMNS, getDaysSince, mapKanbanToStatus, mapStatusToKanban } from '../../data-access/candidature-status';

export interface CandidatureCard {
  id: string;
  idOffre: string;
  entreprise: string;
  role: string;
  type: string;
  statut: string;
  channel: string;
  applicationDate: string;
  dateCreation: string;
  hasResponse: boolean;
  responseStatus: string;
  lastResponseSnippet?: string;
  responseSummary?: string;
  recommendedAction?: string;
  lastResponseAtUtc?: string;
  followUpNeeded?: boolean;
  lastFollowUpAtUtc?: string;
  notes?: string;
}

export function resolveContractType(rawType?: string | null, role?: string | null, notes?: string | null): string {
  const typeStr = (rawType || '').trim();
  if (typeStr && typeStr !== 'CDI' && typeStr !== 'Non spécifié') {
    if (/pfe/i.test(typeStr)) return 'Stage PFE';
    if (/pfa/i.test(typeStr)) return 'Stage PFA';
    if (/stage|intern/i.test(typeStr)) return 'Stage';
    if (/alternan/i.test(typeStr)) return 'Alternance';
    if (/freelance/i.test(typeStr)) return 'Freelance';
    if (/cdd/i.test(typeStr)) return 'CDD';
    return typeStr;
  }

  if (notes) {
    const lines = notes.split('\n').map((l) => l.trim()).filter(Boolean);
    if (lines.length >= 3) {
      const candidate = lines[2];
      if (/pfe/i.test(candidate)) return 'Stage PFE';
      if (/pfa/i.test(candidate)) return 'Stage PFA';
      if (/stage|intern/i.test(candidate)) return 'Stage';
      if (/alternan/i.test(candidate)) return 'Alternance';
      if (/freelance/i.test(candidate)) return 'Freelance';
      if (/cdd/i.test(candidate)) return 'CDD';
      if (/cdi/i.test(candidate)) return 'CDI';
      if (candidate.length > 2 && candidate.length < 30) return candidate;
    }
  }

  const combined = `${role || ''} ${notes || ''}`.toLowerCase();
  if (combined.includes('pfe') || combined.includes("fin d'études") || combined.includes('fin d’études')) {
    return 'Stage PFE';
  }
  if (combined.includes('pfa')) {
    return 'Stage PFA';
  }
  if (combined.includes('intern') || combined.includes('stagiaire') || combined.includes('stage')) {
    return 'Stage';
  }
  if (combined.includes('alternan') || combined.includes('apprenti') || combined.includes('contrat pro')) {
    return 'Alternance';
  }
  if (combined.includes('freelance') || combined.includes('independant') || combined.includes('consultant')) {
    return 'Freelance';
  }
  if (combined.includes('cdd')) {
    return 'CDD';
  }
  if (combined.includes('cdi')) {
    return 'CDI';
  }

  return rawType && rawType !== 'CDI' ? rawType : 'Stage PFE';
}

export function getJobCategoryBadgeClass(category: string): string {
  switch (category) {
    case 'Engineering & Dev':
    case 'Ingénierie & Dev':
      return 'bg-blue-50 text-blue-700 border border-blue-200';
    case 'Data & AI':
    case 'Data & IA':
      return 'bg-purple-50 text-purple-700 border border-purple-200';
    case 'Cloud & DevOps':
      return 'bg-cyan-50 text-cyan-700 border border-cyan-200';
    case 'Product & Design':
    case 'Produit & Design':
      return 'bg-rose-50 text-rose-700 border border-rose-200';
    case 'Cybersecurity':
    case 'Cybersécurité':
      return 'bg-emerald-50 text-emerald-700 border border-emerald-200';
    case 'Management':
      return 'bg-amber-50 text-amber-700 border border-amber-200';
    case 'QA & Testing':
    case 'QA & Test':
      return 'bg-orange-50 text-orange-700 border border-orange-200';
    default:
      return 'bg-gray-100 text-gray-700 border border-gray-200';
  }
}

export function getCompanyInitials(name: string): string {
  if (!name) return 'CO';
  const parts = name.trim().split(/\s+/);
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return (parts[0][0] + parts[1][0]).toUpperCase();
}

export function formatRelativeDate(dateStr?: string | null): string {
  if (!dateStr) return '';
  const days = getDaysSince(dateStr);
  if (days === 0) return 'Today';
  if (days === 1) return 'Yesterday';
  return `D+${days}`;
}

export function formatDateShort(dateStr?: string | null): string {
  if (!dateStr) return 'No date specified';
  const d = new Date(dateStr);
  if (isNaN(d.getTime())) return 'Invalid date';
  return d.toLocaleDateString('en-US', { day: '2-digit', month: 'short', year: 'numeric' });
}

export function isFollowUpSuggested(card: CandidatureCard): boolean {
  return isFollowUpDue(card.statut, card.hasResponse, card.applicationDate);
}

export function getChannelLabel(channel: string): string {
  switch (channel) {
    case 'EMAIL': return 'Email';
    case 'LINKEDIN': return 'LinkedIn';
    case 'INDEED': return 'Indeed';
    case 'WHATSAPP': return 'WhatsApp';
    case 'WEBSITE': return 'Website';
    case 'PHONE': return 'Phone';
    default: return channel || 'Direct';
  }
}

export function getChannelIcon(channel: string): string {
  switch (channel) {
    case 'EMAIL': return 'mail';
    case 'LINKEDIN': return 'in';
    case 'INDEED': return 'i';
    case 'WHATSAPP': return 'chat';
    case 'WEBSITE': return 'language';
    case 'PHONE': return 'call';
    default: return 'send';
  }
}

export function getChannelColor(channel: string): string {
  switch (channel) {
    case 'EMAIL': return '#465fff';
    case 'LINKEDIN': return '#0A66C2';
    case 'INDEED': return '#2164F3';
    case 'WHATSAPP': return '#128C7E';
    case 'WEBSITE': return '#6366F1';
    case 'PHONE': return '#F59B00';
    default: return '#6B7280';
  }
}

export function getInitials(name: string): string {
  return name.split(' ').map((w) => w[0]).join('').toUpperCase().slice(0, 2) || '?';
}

export function getTypeBadgeClass(type: string): string {
  const map: Record<string, string> = {
    'Stage PFE': 'bg-orange-50 text-orange-700 border border-orange-200/80',
    'Stage PFA': 'bg-amber-50 text-amber-700 border border-amber-200/80',
    Stage: 'bg-indigo-50 text-indigo-700 border border-indigo-200/80',
    Alternance: 'bg-sky-50 text-sky-700 border border-sky-200/80',
    CDI: 'bg-emerald-50 text-emerald-700 border border-emerald-200/80',
    CDD: 'bg-purple-50 text-purple-700 border border-purple-200/80',
    Freelance: 'bg-teal-50 text-teal-700 border border-teal-200/80',
  };
  return map[type] || 'bg-gray-100 text-gray-700 border border-gray-200';
}

export function getCompanyColor(name: string): string {
  const colors = ['#465fff', '#8f4900', '#34A853', '#F59B00', '#D93025', '#7d5700', '#1A91F0', '#b35e00'];
  let sum = 0;
  for (let i = 0; i < name.length; i += 1) sum += name.charCodeAt(i);
  return colors[sum % colors.length];
}

/** CSV of the visible candidatures (Excel-friendly: quoted cells; the BOM is added on download). */
export function buildCandidaturesCsv(rows: CandidatureCard[]): string {
  const headers = ['Company', 'Role', 'Channel', 'Status', 'Date', 'Response', 'FollowUp'];
  const csvRows = [headers.join(',')];

  for (const c of rows) {
    const row = [
      `"${(c.entreprise || '').replace(/"/g, '""')}"`,
      `"${(c.role || '').replace(/"/g, '""')}"`,
      `"${c.channel}"`,
      `"${c.statut}"`,
      `"${c.applicationDate}"`,
      `"${c.hasResponse ? 'Yes' : 'No'}"`,
      `"${c.followUpNeeded ? 'Yes' : 'No'}"`,
    ];
    csvRows.push(row.join(','));
  }
  return csvRows.join('\n');
}
