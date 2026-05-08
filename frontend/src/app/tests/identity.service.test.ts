import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { IdentityService } from '../services/identity.service';
import { UserProfileResponse } from '../core/auth/models/user-profile.model';

describe('IdentityService', () => {
  let service: IdentityService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        IdentityService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });
    service = TestBed.inject(IdentityService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify(); // Vérifie qu'il n'y a pas de requêtes en attente
  });

  it('devrait être créé', () => {
    expect(service).toBeTruthy();
  });

  it('devrait récupérer le profil Keycloak de l\'utilisateur via GET', () => {
    const mockProfileResponse: UserProfileResponse = {
      message: 'Success',
      data: {
        id: '123',
        keycloakId: 'kk-123',
        email: 'test@example.com',
        prenom: 'Test',
        nom: 'User',
        onboardingCompleted: true,
        profileScore: 100,
        dateInscription: new Date().toISOString()
      }
    };

    service.getProfile().subscribe(profile => {
      expect(profile).toEqual(mockProfileResponse);
      expect(profile.data.email).toBe('test@example.com');
    });

    const req = httpMock.expectOne('http://localhost:5000/api/identity/profile');
    expect(req.request.method).toBe('GET');
    req.flush(mockProfileResponse);
  });
});
