import { describe, it, expect } from 'vitest';
import { extractCandidateName } from './cv-json.utils';

describe('extractCandidateName', () => {
  it('prend le premier champ de nom complet non vide', () => {
    expect(extractCandidateName({ name: '  Sara   Alami ' })).toBe('Sara Alami');
    expect(extractCandidateName({ name: '', nomComplet: 'Sara Alami' })).toBe('Sara Alami');
    expect(extractCandidateName({ name: '   ', fullName: 'Sara Alami' })).toBe('Sara Alami');
  });

  it('utilise prénom + nom (clés françaises) quand il n’y a pas de nom complet', () => {
    expect(extractCandidateName({ prenom: 'Sara', nom: 'Alami' })).toBe('Sara Alami');
    expect(extractCandidateName({ name: '', prenom: 'Sara', nom: 'Alami' })).toBe('Sara Alami');
  });

  it('utilise firstName + lastName, et mélange les clés si besoin', () => {
    expect(extractCandidateName({ firstName: 'Sara', lastName: 'Alami' })).toBe('Sara Alami');
    expect(extractCandidateName({ prenom: '', firstName: 'Sara', nom: 'Alami' })).toBe('Sara Alami');
  });

  it('renvoie une chaîne vide quand rien n’est renseigné', () => {
    expect(extractCandidateName(null)).toBe('');
    expect(extractCandidateName({ name: '', prenom: ' ' })).toBe('');
  });
});
