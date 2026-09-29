/** Tolerant readers for the AI-generated CV JSON (shared by the resume editor and the generation step). */

export function cleanText(value: any): string {
  return String(value ?? '').trim().replace(/\s+/g, ' ');
}

export function firstArray(source: any, keys: string[]): any[] {
  for (const key of keys) {
    const value = source?.[key];
    if (Array.isArray(value)) return value;
  }
  return [];
}

export function normalizeKey(value: any): string {
  return cleanText(value)
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, ' ')
    .trim();
}

export function isGenericCandidateName(value: string): boolean {
  const normalized = cleanText(value).toLowerCase();
  return normalized === 'candidat' || normalized === 'candidate';
}

/**
 * Full name of a candidate/profile object: the first non-empty full-name field, otherwise
 * first + last name (French or English keys). Empty strings fall through to the next source.
 */
export function extractCandidateName(source: any): string {
  const fullName = [source?.name, source?.nomComplet, source?.fullName].map(cleanText).find(Boolean);
  if (fullName) return fullName;

  const firstName = cleanText(source?.prenom) || cleanText(source?.firstName);
  const lastName = cleanText(source?.nom) || cleanText(source?.lastName);
  return [firstName, lastName].filter(Boolean).join(' ');
}
