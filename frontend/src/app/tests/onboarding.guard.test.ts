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
    dummyState = {} as RouterStateSnapshot;
  });

  describe('onboardingGuard', () => {
    it('devrait autoriser l\'accès si l\'onboarding est complété', () => {
      mockOnboardingService.getStatus.mockReturnValue(of({ onboardingCompleted: true, profileScore: 100 }));
      dummyState.url = '/profile';
      
      const result$ = TestBed.runInInjectionContext(() => onboardingGuard(dummyRoute, dummyState));
      
      // on s'abonne à l'observable renvoyé par le guard
      if (typeof result$ !== 'boolean' && 'subscribe' in result$) {
        result$.subscribe(res => {
          expect(res).toBe(true);
        });
      }
    });

    it('devrait rediriger vers /onboarding si l\'onboarding n\'est pas complété', () => {
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
    it('devrait autoriser l\'accès (à la page onboarding) si NON complété', () => {
      mockOnboardingService.getStatus.mockReturnValue(of({ onboardingCompleted: false, profileScore: 0 }));
      
      const result$ = TestBed.runInInjectionContext(() => alreadyOnboardedGuard(dummyRoute, dummyState));
      
      if (typeof result$ !== 'boolean' && 'subscribe' in result$) {
        result$.subscribe(res => {
          expect(res).toBe(true);
        });
      }
    });

    it('devrait rediriger vers /dashboard si DEJA complété', () => {
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
