import { describe, it, expect } from 'vitest';
import { CvPayloadContext, normalizeCvForBackend, unwrapCvPayload } from './cv-payload.normalizer';

const context = (over: Partial<CvPayloadContext> = {}): CvPayloadContext => ({
  pipelineResult: null,
  liveProfilePersonal: {},
  signedProfilePhotoUrl: null,
  ...over,
});

describe('normalizeCvForBackend', () => {
  it('déballe les enveloppes de payload connues', () => {
    const cv = { candidate: { name: 'A' } };
    expect(unwrapCvPayload({ cvData: cv })).toBe(cv);
    expect(unwrapCvPayload({ cv_optimized_content: cv })).toBe(cv);
    expect(unwrapCvPayload(cv)).toBe(cv);
  });

  it('remplace un nom générique par celui du profil et complète la photo signée', () => {
    const result = normalizeCvForBackend(
      { candidate: { name: 'Candidat' }, fontFamily: 'Inter' },
      context({
        liveProfilePersonal: { firstName: 'Sara', lastName: 'Alami', city: 'Rabat', country: 'Maroc' },
        signedProfilePhotoUrl: 'https://cdn/photo.jpg',
      }),
    );
    expect(result.candidate.name).toBe('Sara Alami');
    expect(result.candidate.location).toBe('Rabat, Maroc');
    expect(result.candidate.photoUrl).toBe('https://cdn/photo.jpg');
    expect(result.fontFamily).toBeUndefined();
  });

  it('reprend le prénom et le nom (clés françaises) du profil du pipeline', () => {
    const result = normalizeCvForBackend(
      { candidate: { name: 'Candidat' } },
      context({ pipelineResult: { profileData: { personalInfo: { prenom: 'Sara', nom: 'Alami' } } } }),
    );
    expect(result.candidate.name).toBe('Sara Alami');
  });

  it('déplace une expérience de type activité (hackathon) vers les activités', () => {
    const result = normalizeCvForBackend(
      {
        candidate: { name: 'Sara Alami' },
        experience: [
          { role: 'Développeuse', company: 'Acme', bullets: ['API'] },
          { role: 'Participant hackathon', company: 'Club IT', bullets: ['Prototype'] },
        ],
      },
      context(),
    );
    expect(result.experience.map((e: any) => e.company)).toEqual(['Acme']);
    expect(result.activities.some((a: any) => /hackathon/i.test(a.title))).toBe(true);
  });

  it('garde un vrai poste même si ses puces parlent de formation, d’événements ou de club', () => {
    const result = normalizeCvForBackend(
      {
        candidate: { name: 'Sara Alami' },
        experience: [
          { role: 'Formatrice technique', company: 'Kids Academy', bullets: ['Animé des formations et des events pour un club client'] },
        ],
      },
      context(),
    );
    expect(result.experience.map((e: any) => e.company)).toEqual(['Kids Academy']);
    expect(result.activities).toEqual([]);
  });
});
