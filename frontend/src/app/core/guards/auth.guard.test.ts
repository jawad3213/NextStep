import { describe, it, expect, vi, beforeEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { RouterStateSnapshot, ActivatedRouteSnapshot } from '@angular/router';
import Keycloak from 'keycloak-js';
import { authGuard } from '../core/guards/auth.guard';

describe('AuthGuard', () => {
  let mockKeycloak: any;
  let dummyRoute: ActivatedRouteSnapshot;
  let dummyState: RouterStateSnapshot;

  beforeEach(() => {
    mockKeycloak = {
      authenticated: false,
      login: vi.fn().mockResolvedValue(undefined)
    };

    TestBed.configureTestingModule({
      providers: [
        { provide: Keycloak, useValue: mockKeycloak }
      ]
    });

    dummyRoute = {} as ActivatedRouteSnapshot;
    dummyState = { url: '/dashboard' } as RouterStateSnapshot;
  });

  it('devrait autoriser l\'accès si l\'utilisateur est authentifié', async () => {
    mockKeycloak.authenticated = true;
    
    // Pour tester une CanActivateFn (qui utilise inject()), on utilise runInInjectionContext
    const result = await TestBed.runInInjectionContext(() => authGuard(dummyRoute, dummyState));
    
    expect(result).toBe(true);
    expect(mockKeycloak.login).not.toHaveBeenCalled();
  });

  it('devrait bloquer l\'accès et rediriger vers le login si non authentifié', async () => {
    mockKeycloak.authenticated = false;
    
    const result = await TestBed.runInInjectionContext(() => authGuard(dummyRoute, dummyState));
    
    expect(result).toBe(false);
    expect(mockKeycloak.login).toHaveBeenCalledWith({
      redirectUri: expect.stringContaining('/dashboard')
    });
  });
});
