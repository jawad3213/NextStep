import { describe, it, expect, beforeEach, afterEach,vi } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { signal } from '@angular/core';

import { ProfileService } from '../features/profile/profile.service';
import { AuthService } from '../core/auth/services/auth.service';
import { Experience, Education, Skill, Project, Certification, PersonalInfo } from '../features/profile/profile.types';

describe('ProfileFeatureService (State & Signals)', () => {
  let service: ProfileService;
  let httpMock: HttpTestingController;
  
  // Création d'un mock pour AuthService car ProfileService en dépend
  const mockAuthService = {
    user: signal({ firstName: 'Test', lastName: 'User', email: 'test@example.com' })
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        ProfileService,
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: mockAuthService }
      ]
    });
    
    service = TestBed.inject(ProfileService);
    httpMock = TestBed.inject(HttpTestingController);

    const bootstrapRequests = httpMock.match('http://localhost:5000/api/profile');
    bootstrapRequests.forEach((req) => {
      req.flush({
        personalInfo: { prenom: 'Test', nom: 'User', email: 'test@example.com' },
        formations: [],
        experiences: [],
        competences: [],
        projets: [],
        certifications: []
      });
    });
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('devrait être créé et initialiser les Signals', () => {
    expect(service).toBeTruthy();
    expect(service.profile()).toBeDefined();
    expect(service.currentStep()).toBe('coordonnees');
  });

  it('devrait calculer le bon pourcentage de complétion', () => {
    // Par défaut, le profil est vide
    expect(service.completionPercentage()).toBe(0);

    // Ajout d'informations personnelles (devrait ajouter 6 points: prenom + nom + email)
    service.updateProfile({
      personal: {
        ...service.profile().personal,
        firstName: 'John',
        lastName: 'Doe',
        email: 'john@example.com'
      }
    });

    expect(service.completionPercentage()).toBe(6);
  });

  it('devrait mettre à jour l\'étape courante via setStep', () => {
    service.setStep('experience');
    expect(service.currentStep()).toBe('experience');
  });

  it('devrait vérifier si une section est complète (isSectionComplete)', () => {
    // Coordonnées incomplètes par défaut
    expect(service.isSectionComplete('coordonnees')).toBe(false);

    service.updateProfile({
      personal: {
        ...service.profile().personal,
        firstName: 'John',
        lastName: 'Doe',
        email: 'john@example.com'
      }
    });
    
    // Devient complète après ajout des infos requises
    expect(service.isSectionComplete('coordonnees')).toBe(true);
  });

  // --- Tests des opérations CRUD HTTP ---

  it('devrait charger le profil depuis l\'API (loadProfile)', async () => {
    const mockApiData = {
      personalInfo: { prenom: 'John', nom: 'Doe', email: 'john@test.com' },
      formations: [],
      experiences: [],
      competences: [],
      projets: [],
      certifications: []
    };

    const promise = service.loadProfile();
    
    const req = httpMock.expectOne('http://localhost:5000/api/profile');
    expect(req.request.method).toBe('GET');
    req.flush(mockApiData);

    await promise;
    expect(service.profile().personal.firstName).toBe('John');
  });

  it('devrait générer un CV via IA (generateResume)', async () => {
    const mockAiResponse = { resume: 'Profil de développeur dynamique...' };
    
    const promise = service.generateResume({});
    
    const req = httpMock.expectOne('http://localhost:5000/api/profile/generate-resume');
    expect(req.request.method).toBe('POST');
    req.flush(mockAiResponse);

    const result = await promise;
    expect(result).toBe('Profil de développeur dynamique...');
  });

  it('devrait récupérer les mots-clés (getKeywords)', async () => {
    const mockKeywords = [{ mot: 'Angular', categorie: 'Technique' }];
    const promise = service.getKeywords();
    const req = httpMock.expectOne('http://localhost:5000/api/profile/keywords');
    req.flush(mockKeywords);
    expect(await promise).toEqual(mockKeywords);
  });

  it('devrait normaliser une reponse d import stringifiee sans dupliquer les langues', () => {
    const serviceAny = service as any;

    const normalized = serviceAny.normalizeImportedPayload(JSON.stringify({
      personal: {
        prenom: 'Jane',
        nom: 'Doe',
        email: 'jane@test.com',
        telephone: '+212600000000',
        titrePoste: 'Frontend Engineer',
        ville: 'Casablanca',
        pays: 'Morocco',
        summary: 'Angular engineer with product experience.'
      },
      experiences: [
        {
          entreprise: 'NextStep',
          poste: 'Frontend Engineer',
          dateDebut: '2023-02-01',
          dateFin: '2024-05-01',
          missions: 'Built profile flows'
        }
      ],
      formations: [
        {
          etablissement: 'ENSA',
          diplome: 'Engineering Degree',
          annee: '2020-09',
          anneeFin: '2023-06'
        }
      ],
      languages: [{ name: 'English', level: 'C1' }],
      skills: ['English', { name: 'Angular', typeCompetence: 'Technical' }],
      projects: [
        { title: 'Portfolio', technologies: 'Angular, Firebase' }
      ],
      certifications: [
        { title: 'AWS Cloud Practitioner', issuer: 'Amazon' }
      ]
    }));

    expect(normalized.personal.firstName).toBe('Jane');
    expect(normalized.resume).toContain('Angular engineer');
    expect(normalized.experience).toHaveLength(1);
    expect(normalized.education[0].startYear).toBe('2020');
    expect(normalized.languages.map((lang: any) => lang.name)).toEqual(['English']);
    expect(normalized.skills.map((skill: any) => skill.name)).toEqual(['Angular']);
    expect(normalized.projects[0].stack).toEqual(['Angular', 'Firebase']);
    expect(normalized.certifications[0].name).toBe('AWS Cloud Practitioner');
  });

  // --- NOUVEAUX TESTS CRUD AJOUTÉS --- //

  it('devrait mettre à jour les infos personnelles (savePersonalInfo)', async () => {
    const info: PersonalInfo = { firstName: 'Jane', lastName: 'Doe', email: 'jane@test.com', phone: '', city: '', country: '', jobTitle: '', linkedinUrl: '', githubUrl: '', portfolioUrl: 'https://portfolio.test', photoUrl: null, useAsHeadline: true, address: '' };
    const promise = service.savePersonalInfo(info);
    const req = httpMock.expectOne('http://localhost:5000/api/profile/personal-info');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body.lienPortfolio).toBe('https://portfolio.test');
    req.flush({});
    await promise;
  });

  it('devrait ajouter une expérience et recharger le profil (addExperience)', async () => {
    const exp: Experience = { id: '', company: 'Tech', title: 'Dev', startDate: '2023-01', endDate: '', description: 'Code', city: 'Paris', type: 'CDI', current: true, taches: ['Built APIs'] };
    const loadSpy = vi.spyOn(service, 'loadProfile').mockResolvedValue(undefined);
    const promise = service.addExperience(exp);
    const req = httpMock.expectOne('http://localhost:5000/api/profile/experiences');
    expect(req.request.method).toBe('POST');
    expect(req.request.body.taches).toEqual(['Built APIs']);
    req.flush({});
    await promise;
    expect(loadSpy).toHaveBeenCalled();
  });

  it('devrait mettre à jour une expérience (updateExperience)', async () => {
    const loadSpy = vi.spyOn(service, 'loadProfile').mockResolvedValue(undefined);
    const exp: Experience = { id: '1', company: 'X', title: 'Y', startDate: '', endDate: '', description: '', city: '', type: 'Internship', current: false, taches: ['Maintained UI'] };
    const promise = service.updateExperience(exp);
    const req = httpMock.expectOne('http://localhost:5000/api/profile/experiences');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body.taches).toEqual(['Maintained UI']);
    req.flush({});
    await promise;
    expect(loadSpy).toHaveBeenCalled();
  });

  it('devrait supprimer une expérience (deleteExperience)', async () => {
    const loadSpy = vi.spyOn(service, 'loadProfile').mockResolvedValue(undefined);
    const promise = service.deleteExperience('1');
    const req = httpMock.expectOne('http://localhost:5000/api/profile/experiences/1');
    expect(req.request.method).toBe('DELETE');
    req.flush({});
    await promise;
    expect(loadSpy).toHaveBeenCalled();
  });

  it('devrait ajouter une formation (addEducation)', async () => {
    const loadSpy = vi.spyOn(service, 'loadProfile').mockResolvedValue(undefined);
    const edu: Education = { id: '', institution: 'Z', degree: 'W', startYear: '2020', endYear: '2023', city: '', specialization: '', mention: 'Passable', current: false };
    const promise = service.addEducation(edu);
    const req = httpMock.expectOne('http://localhost:5000/api/profile/formations');
    expect(req.request.method).toBe('POST');
    req.flush({});
    await promise;
    expect(loadSpy).toHaveBeenCalled();
  });

  it('devrait mettre à jour une formation (updateEducation)', async () => {
    const loadSpy = vi.spyOn(service, 'loadProfile').mockResolvedValue(undefined);
    const edu: Education = { id: '2', institution: 'Z', degree: 'W', startYear: '2020', endYear: '2023', city: '', specialization: '', mention: 'Passable', current: false };
    const promise = service.updateEducation(edu);
    const req = httpMock.expectOne('http://localhost:5000/api/profile/formations');
    expect(req.request.method).toBe('PUT');
    req.flush({});
    await promise;
    expect(loadSpy).toHaveBeenCalled();
  });

  it('devrait ajouter une compétence (addSkill)', async () => {
    const loadSpy = vi.spyOn(service, 'loadProfile').mockResolvedValue(undefined);
    const skill: Skill = { id: '', name: 'Angular', category: 'Technique' };
    const promise = service.addSkill(skill);
    const req = httpMock.expectOne('http://localhost:5000/api/profile/competences');
    expect(req.request.method).toBe('POST');
    req.flush({});
    await promise;
    expect(loadSpy).toHaveBeenCalled();
  });

  it('devrait mettre à jour une compétence (updateSkill)', async () => {
    const loadSpy = vi.spyOn(service, 'loadProfile').mockResolvedValue(undefined);
    const skill: Skill = { id: '3', name: 'Angular', category: 'Technique' };
    const promise = service.updateSkill(skill);
    const req = httpMock.expectOne('http://localhost:5000/api/profile/competences');
    expect(req.request.method).toBe('PUT');
    req.flush({});
    await promise;
    expect(loadSpy).toHaveBeenCalled();
  });

  it('devrait supprimer une compétence (deleteSkill)', async () => {
    const loadSpy = vi.spyOn(service, 'loadProfile').mockResolvedValue(undefined);
    const promise = service.deleteSkill('3');
    const req = httpMock.expectOne('http://localhost:5000/api/profile/competences/3');
    expect(req.request.method).toBe('DELETE');
    req.flush({});
    await promise;
    expect(loadSpy).toHaveBeenCalled();
  });

  it('devrait ajouter un projet (addProject)', async () => {
    const loadSpy = vi.spyOn(service, 'loadProfile').mockResolvedValue(undefined);
    const proj: Project = { id: '', title: 'A', description: 'B', stack: [], githubUrl: '', demoUrl: '', isUniversity: false, taches: ['Designed dashboard'] };
    const promise = service.addProject(proj);
    const req = httpMock.expectOne('http://localhost:5000/api/profile/projets');
    expect(req.request.method).toBe('POST');
    expect(req.request.body.taches).toEqual(['Designed dashboard']);
    req.flush({});
    await promise;
    expect(loadSpy).toHaveBeenCalled();
  });

  it('devrait mettre à jour un projet (updateProject)', async () => {
    const loadSpy = vi.spyOn(service, 'loadProfile').mockResolvedValue(undefined);
    const proj: Project = { id: '4', title: 'A', description: 'B', stack: [], githubUrl: '', demoUrl: '', isUniversity: false, taches: ['Improved CI'] };
    const promise = service.updateProject(proj);
    const req = httpMock.expectOne('http://localhost:5000/api/profile/projets');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body.taches).toEqual(['Improved CI']);
    req.flush({});
    await promise;
    expect(loadSpy).toHaveBeenCalled();
  });

  it('devrait supprimer un projet (deleteProject)', async () => {
    const loadSpy = vi.spyOn(service, 'loadProfile').mockResolvedValue(undefined);
    const promise = service.deleteProject('4');
    const req = httpMock.expectOne('http://localhost:5000/api/profile/projets/4');
    expect(req.request.method).toBe('DELETE');
    req.flush({});
    await promise;
    expect(loadSpy).toHaveBeenCalled();
  });

  it('devrait ajouter une certification (addCertification)', async () => {
    const loadSpy = vi.spyOn(service, 'loadProfile').mockResolvedValue(undefined);
    const cert: Certification = { id: '', name: 'C', issuer: 'D', date: '', verificationUrl: '' };
    const promise = service.addCertification(cert);
    const req = httpMock.expectOne('http://localhost:5000/api/profile/certifications');
    expect(req.request.method).toBe('POST');
    req.flush({});
    await promise;
    expect(loadSpy).toHaveBeenCalled();
  });

  it('devrait mettre à jour une certification (updateCertification)', async () => {
    const loadSpy = vi.spyOn(service, 'loadProfile').mockResolvedValue(undefined);
    const cert: Certification = { id: '5', name: 'C', issuer: 'D', date: '', verificationUrl: '' };
    const promise = service.updateCertification(cert);
    const req = httpMock.expectOne('http://localhost:5000/api/profile/certifications');
    expect(req.request.method).toBe('PUT');
    req.flush({});
    await promise;
    expect(loadSpy).toHaveBeenCalled();
  });

  it('devrait supprimer une certification (deleteCertification)', async () => {
    const loadSpy = vi.spyOn(service, 'loadProfile').mockResolvedValue(undefined);
    const promise = service.deleteCertification('5');
    const req = httpMock.expectOne('http://localhost:5000/api/profile/certifications/5');
    expect(req.request.method).toBe('DELETE');
    req.flush({});
    await promise;
    expect(loadSpy).toHaveBeenCalled();
  });
});
