import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { OnboardingService } from '../services/onboarding.service';
import { SoftOnboardingPayload } from '../core/auth/models/user-profile.model';

describe('OnboardingService', () => {
  let service: OnboardingService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        OnboardingService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });
    service = TestBed.inject(OnboardingService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('devrait être créé', () => {
    expect(service).toBeTruthy();
  });

  it('devrait récupérer le statut de l\'onboarding (getStatus)', () => {
    const mockStatus = { onboardingCompleted: true, profileScore: 80 };

    service.getStatus().subscribe(status => {
      expect(status).toEqual(mockStatus);
    });

    const req = httpMock.expectOne('http://localhost:5000/api/identity/onboarding-status');
    expect(req.request.method).toBe('GET');
    req.flush(mockStatus);
  });

  it('devrait soumettre les données d\'onboarding (submitSoftOnboarding)', () => {
    const payload: SoftOnboardingPayload = { 
      objectif: 'CDI', 
      niveau: 'BAC_PLUS_5', 
      secteur: 'INFORMATIQUE' 
    };

    service.submitSoftOnboarding(payload).subscribe(res => {
      expect(res).toBeTruthy();
    });

    const req = httpMock.expectOne('http://localhost:5000/api/identity/soft-onboarding');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(payload);
    req.flush({ success: true });
  });

  it('devrait récupérer le statut du profil (getProfileStatus)', () => {
    const mockProfileStatus = { isComplete: false, missingFields: ['phone'] };

    service.getProfileStatus().subscribe(status => {
      expect(status).toEqual(mockProfileStatus);
    });

    const req = httpMock.expectOne('http://localhost:5000/api/identity/profile-status');
    expect(req.request.method).toBe('GET');
    req.flush(mockProfileStatus);
  });
});
