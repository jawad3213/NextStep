import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { ProfileService } from '../services/profile.service';

describe('ProfileService', () => {
  let service: ProfileService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        ProfileService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    service = TestBed.inject(ProfileService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('devrait être créé', () => {
    expect(service).toBeTruthy();
  });

  it('devrait récupérer le profil complet via GET', () => {
    const mockProfile = { personalInfo: { name: 'John' }, experiences: [] };
    
    service.getFullProfile().subscribe((profile) => {
      expect(profile).toEqual(mockProfile);
    });

    const req = httpMock.expectOne('http://localhost:5000/api/profile');
    expect(req.request.method).toBe('GET');
    req.flush(mockProfile);
  });

  it('devrait ajouter une expérience via POST', () => {
    const newExp = { title: 'Développeur' };
    
    service.addExperience(newExp).subscribe((res) => {
      expect(res).toEqual(newExp);
    });

    const req = httpMock.expectOne('http://localhost:5000/api/profile/experiences');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(newExp);
    req.flush(newExp);
  });
});
