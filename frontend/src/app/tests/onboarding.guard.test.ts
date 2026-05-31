import { describe, it, expect, vi, beforeEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { Router, ActivatedRouteSnapshot, RouterStateSnapshot, UrlTree } from '@angular/router';
import { of } from 'rxjs';
import { onboardingGuard, alreadyOnboardedGuard } from '../core/guards/onboarding.guard';
import { OnboardingService } from '../services/onboarding.service';

describe('OnboardingGuards', () => {
  let mockOnboardingService: any;
  let mockRouter: any;
  let dummyRoute: ActivatedRouteSnapshot;
  let dummyState: RouterStateSnapshot;

  beforeEach(() => {
    localStorage.clear();

    mockOnboardingService = {
      getStatus: vi.fn()
    };

    mockRouter = {
      parseUrl: vi.fn((url: string) => `UrlTree(${url})` as unknown as UrlTree)
    };

    TestBed.configureTestingModule({
      providers: [
        { provide: OnboardingService, useValue: mockOnboardingService },
        { provide: Router, useValue: mockRouter }
      ]
    });

    dummyRoute = {} as ActivatedRouteSnapshot;
    dummyState = { url: '/dashboard' } as RouterStateSnapshot;
  });

  describe('onboardingGuard', () => {
    it('devrait autoriser l\'accÃ¨s si l\'onboarding est complÃ©tÃ©', () => {
      mockOnboardingService.getStatus.mockReturnValue(of({ onboardingCompleted: true, profileScore: 100 }));
      localStorage.setItem('nextstep_profile_unlocked', 'true');
      dummyState.url = '/profile';
      
      const result$ = TestBed.runInInjectionContext(() => onboardingGuard(dummyRoute, dummyState));
      
      // on s'abonne Ã  l'observable renvoyÃ© par le guard
      if (typeof result$ !== 'boolean' && 'subscribe' in result$) {
        result$.subscribe(res => {
          expect(res).toBe(true);
        });
      }
    });

    it('devrait rediriger vers /onboarding si l\'onboarding n\'est pas complÃ©tÃ©', () => {
      mockOnboardingService.getStatus.mockReturnValue(of({ onboardingCompleted: false, profileScore: 0 }));
      
      const result$ = TestBed.runInInjectionContext(() => onboardingGuard(dummyRoute, dummyState));
      
      if (typeof result$ !== 'boolean' && 'subscribe' in result$) {
        result$.subscribe(res => {
          expect(mockRouter.parseUrl).toHaveBeenCalledWith('/onboarding');
          expect(res).toBe(`UrlTree(/onboarding)`);
        });
      }
    });

    it('devrait rediriger vers /onboarding si l\'onboarding est partiel', () => {
      mockOnboardingService.getStatus.mockReturnValue(of({ onboardingCompleted: false, profileScore: 35 }));
      
      const result$ = TestBed.runInInjectionContext(() => onboardingGuard(dummyRoute, dummyState));
      
      if (typeof result$ !== 'boolean' && 'subscribe' in result$) {
        result$.subscribe(res => {
          expect(mockRouter.parseUrl).toHaveBeenCalledWith('/onboarding');
          expect(res).toBe(`UrlTree(/onboarding)`);
        });
      }
    });
  });

  describe('alreadyOnboardedGuard', () => {
    it('devrait autoriser l\'accÃ¨s (Ã  la page onboarding) si NON complÃ©tÃ©', () => {
      mockOnboardingService.getStatus.mockReturnValue(of({ onboardingCompleted: false, profileScore: 0 }));
      
      const result$ = TestBed.runInInjectionContext(() => alreadyOnboardedGuard(dummyRoute, dummyState));
      
      if (typeof result$ !== 'boolean' && 'subscribe' in result$) {
        result$.subscribe(res => {
          expect(res).toBe(true);
        });
      }
    });

    it('devrait rediriger vers /offers si DEJA complÃ©tÃ©', () => {
      mockOnboardingService.getStatus.mockReturnValue(of({ onboardingCompleted: true, profileScore: 100 }));
      
      const result$ = TestBed.runInInjectionContext(() => alreadyOnboardedGuard(dummyRoute, dummyState));
      
      if (typeof result$ !== 'boolean' && 'subscribe' in result$) {
        result$.subscribe(res => {
          expect(mockRouter.parseUrl).toHaveBeenCalledWith('/offers');
          expect(res).toBe(`UrlTree(/offers)`);
        });
      }
    });
  });
});

