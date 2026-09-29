import { describe, it, expect } from 'vitest';
import { CandidatureDto } from '@features/applications/data-access/candidature.models';
import { OfferHistoryItem } from '@features/offers/data-access/offers.models';
import {
  buildKpis,
  buildPipeline,
  buildSummaryLine,
  buildTodos,
  companyInitials,
  toDashboardApplications,
} from './dashboard-summary';

const daysAgo = (n: number) => new Date(Date.now() - n * 24 * 3600 * 1000).toISOString();

const candidature = (id: string, over: Partial<CandidatureDto> = {}): CandidatureDto => ({
  idCandidature: id,
  idUtilisateur: 'u1',
  idOffre: `o-${id}`,
  dateCreation: daysAgo(1),
  applicationDate: daysAgo(1),
  inclureLettreMotivation: false,
  statut: 'ENVOYE',
  channel: 'EMAIL',
  appliedManually: false,
  language: 'fr',
  responseStatus: '',
  hasResponse: false,
  ...over,
});

const offer = (id: string, entreprise: string, titre: string): OfferHistoryItem => ({
  offerId: `o-${id}`, entreprise, titre, localisation: '', status: 'analysee', currentStep: 2, dateCreation: daysAgo(1),
});

describe('dashboard summary', () => {
  const offers = [offer('a', 'Phi Partners', 'Quant Dev'), offer('b', 'BCG', 'AI Engineer'), offer('c', 'SQLI', 'Front')];

  it('résout entreprise et poste depuis les offres et trie du plus récent au plus ancien', () => {
    const apps = toDashboardApplications(
      [candidature('a', { applicationDate: daysAgo(6) }), candidature('b', { applicationDate: daysAgo(1) }), candidature('x')],
      offers,
    );
    expect(apps.map(a => a.company)).toEqual(['BCG', 'Company', 'Phi Partners']);
    expect(apps[0].role).toBe('AI Engineer');
    expect(apps[0].stage.label).toBe('Sent');
  });

  it('priorise entretien, puis réponse, puis relance, puis brouillon', () => {
    const apps = toDashboardApplications([
      candidature('a', { statut: 'BROUILLON' }),
      candidature('b', { applicationDate: daysAgo(8) }),
      candidature('c', { statut: 'ENTRETIEN_PROPOSE' }),
      candidature('d', { hasResponse: true, lastResponseAtUtc: daysAgo(2), statut: 'REPONSE_RECUE', responseSummary: 'Demande de disponibilités' }),
    ], [...offers, offer('d', 'Acme', 'Dev')]);

    const todos = buildTodos(apps);
    expect(todos.map(t => t.kind)).toEqual(['interview', 'reply', 'follow-up', 'draft']);
    expect(todos[1].detail).toBe('Demande de disponibilités');
    expect(todos[2]).toMatchObject({ title: 'Follow up with BCG', link: ['/letters', 'b'] });
    expect(todos[2].detail).toContain('8 days');
  });

  it('ne propose pas de relance avant 5 jours ni après une réponse', () => {
    const apps = toDashboardApplications([
      candidature('a', { applicationDate: daysAgo(2) }),
      candidature('b', { applicationDate: daysAgo(9), hasResponse: true, lastResponseAtUtc: daysAgo(20) }),
    ], offers);
    expect(buildTodos(apps)).toEqual([]);
  });

  it('calcule chiffres clés et suivi par étape', () => {
    const apps = toDashboardApplications([
      candidature('a'),
      candidature('b', { statut: 'ENTRETIEN_PROPOSE', hasResponse: true }),
      candidature('c', { statut: 'BROUILLON' }),
      candidature('d', { statut: 'REFUSE', hasResponse: true }),
    ], offers);

    expect(buildKpis(apps)).toEqual({ total: 4, waiting: 1, responseRate: 67, interviews: 1 });

    const pipeline = buildPipeline(apps);
    expect(pipeline.segments.map(s => [s.column.key, s.count])).toEqual([
      ['brouillon', 1], ['envoye', 1], ['entretien', 1], ['refuse', 1],
    ]);
    expect(pipeline.segments.reduce((sum, s) => sum + s.pct, 0)).toBeCloseTo(100);
    expect(pipeline).toMatchObject({ sent: 3, repliesReceived: 2, replyRate: 67 });
  });

  it('résume la journée en une ligne', () => {
    expect(buildSummaryLine([], 0)).toContain('first application');
    const apps = toDashboardApplications([candidature('a'), candidature('b', { statut: 'REFUSE' })], offers);
    expect(buildSummaryLine(apps, 1)).toBe('1 application in progress · 1 action to do');
    expect(buildSummaryLine(apps, 0)).toBe('1 application in progress · all up to date');
  });

  it('calcule les initiales d’une entreprise', () => {
    expect(companyInitials('Boston Consulting Group')).toBe('BC');
    expect(companyInitials('SQLI')).toBe('SQ');
    expect(companyInitials('  ')).toBe('?');
  });
});
