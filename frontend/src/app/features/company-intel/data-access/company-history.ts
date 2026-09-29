/**
 * Company analyses the user ran (from an offer or from the Company Intel page), kept in this
 * browser. Newest first, one entry per company + role, at most MAX_ENTRIES.
 */
const STORAGE_KEY = 'nextstep.company.history';
const LAST_PAYLOAD_KEY = 'nextstep.company.last_payload';
const MAX_ENTRIES = 20;

export interface CompanyHistoryItem {
  id: string;
  companyName: string;
  jobTitle: string;
  analyzedAt: string;
  data: {
    nom: string;
    summary: string;
    compatibilityScore: number;
  };
  // eslint-disable-next-line @typescript-eslint/no-explicit-any -- raw agent payload, mapped by the analysis page
  rawPayload?: any;
}

export function loadCompanyHistory(): CompanyHistoryItem[] {
  try {
    const parsed = JSON.parse(localStorage.getItem(STORAGE_KEY) ?? '[]');
    if (!Array.isArray(parsed)) return [];
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return parsed.map((h: any) => ({
      id: String(h?.id ?? Date.now()),
      companyName: String(h?.companyName ?? ''),
      jobTitle: String(h?.jobTitle ?? ''),
      analyzedAt: String(h?.analyzedAt ?? new Date().toISOString()),
      data: {
        nom: h?.data?.nom ?? h?.companyName ?? '',
        summary: h?.data?.summary ?? '',
        compatibilityScore: h?.data?.compatibilityScore ?? 0,
      },
      rawPayload: h?.rawPayload,
    }));
  } catch {
    return [];
  }
}

/** Records an analysis (replacing an older one for the same company + role) and returns the new list. */
// eslint-disable-next-line @typescript-eslint/no-explicit-any
export function saveCompanyAnalysis(apiRes: any, companyName: string, jobTitle: string): CompanyHistoryItem[] {
  const company = (companyName || '').trim();
  const title = (jobTitle || '').trim();
  const current = loadCompanyHistory();
  if (!company) return current;

  const entry: CompanyHistoryItem = {
    id: `${Date.now()}`,
    companyName: company,
    jobTitle: title,
    analyzedAt: new Date().toISOString(),
    data: {
      nom: apiRes?.intelligence?.nom ?? company,
      summary: apiRes?.intelligence?.summary ?? '',
      compatibilityScore: apiRes?.score ?? 0,
    },
    rawPayload: apiRes,
  };
  const others = current.filter(h =>
    !(h.companyName.toLowerCase() === company.toLowerCase() && h.jobTitle.toLowerCase() === title.toLowerCase())
  );
  const next = [entry, ...others].slice(0, MAX_ENTRIES);

  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(next));
    // The analysis page falls back to this snapshot when opened without navigation state (reload).
    sessionStorage.setItem(LAST_PAYLOAD_KEY, JSON.stringify({ companyIntelPayload: apiRes, companyName: company, jobTitle: title }));
  } catch (err) {
    console.warn('[CompanyHistory] Failed to persist company history:', err);
  }
  return next;
}

export function removeCompanyAnalysis(id: string): CompanyHistoryItem[] {
  const next = loadCompanyHistory().filter(h => h.id !== id);
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(next));
  } catch { /* storage unavailable: the list is still updated on screen */ }
  return next;
}
