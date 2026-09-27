import { describe, it, expect, vi, beforeEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { Router, ActivatedRouteSnapshot, RouterStateSnapshot, UrlTree } from '@angular/router';
import { Observable, firstValueFrom, of, throwError } from 'rxjs';
import { onboardingGuard, alreadyOnboardedGuard } from '../core/guards/onboarding.guard';
import { OnboardingService, OnboardingStatus } from '../services/onboarding.service';

describe('OnboardingGuards', () => {
  let mockOnboardingService: { getStatus: ReturnType<typeof vi.fn> };
  let mockRouter: { parseUrl: ReturnType<typeof vi.fn> };
  const route = {} as ActivatedRouteSnapshot;

  const status = (onboardingCompleted: boolean, profileCompleted: boolean): OnboardingStatus =>
    ({ onboardingCompleted, profileCompleted, profileScore: 0, completionPercent: 0 });

  const run = (guard: typeof onboardingGuard, url: string) => {
    const result = TestBed.runInInjectionContext(() => guard(route, { url } as RouterStateSnapshot));
    return firstValueFrom(result as Observable<boolean | UrlTree>);
  };

  beforeEach(() => {
    localStorage.clear();
    mockOnboardingService = { getStatus: vi.fn() };
    mockRouter = { parseUrl: vi.fn((url: string) => `UrlTree(${url})` as unknown as UrlTree) };

    TestBed.configureTestingModule({
      providers: [
        { provide: OnboardingService, useValue: mockOnboardingService },
        { provide: Router, useValue: mockRouter },
      ],
    });
  });

  describe('onboardingGuard', () => {
    it('sends a new user to /onboarding', async () => {
      mockOnboardingService.getStatus.mockReturnValue(of(status(false, false)));
      expect(await run(onboardingGuard, '/dashboard')).toBe('UrlTree(/onboarding)');
    });

    it('forces the profile stepper until the server marks the profile completed', async () => {
      mockOnboardingService.getStatus.mockReturnValue(of(status(true, false)));
      expect(await run(onboardingGuard, '/offers')).toBe('UrlTree(/profile?step=coordonnees)');
    });

    it('allows the profile page while the profile is being completed', async () => {
      mockOnboardingService.getStatus.mockReturnValue(of(status(true, false)));
      expect(await run(onboardingGuard, '/profile?step=experience')).toBe(true);
    });

    it('ignores the legacy browser unlock flag', async () => {
      localStorage.setItem('nextstep_profile_unlocked', 'true');
      mockOnboardingService.getStatus.mockReturnValue(of(status(true, false)));
      expect(await run(onboardingGuard, '/dashboard')).toBe('UrlTree(/profile?step=coordonnees)');
    });

    it('opens the application once onboarding and profile are completed', async () => {
      mockOnboardingService.getStatus.mockReturnValue(of(status(true, true)));
      expect(await run(onboardingGuard, '/dashboard')).toBe(true);
    });

    it('fails closed when the status cannot be loaded', async () => {
      localStorage.setItem('nextstep_profile_unlocked', 'true');
      mockOnboardingService.getStatus.mockReturnValue(throwError(() => new Error('network')));
      expect(await run(onboardingGuard, '/dashboard')).toBe('UrlTree(/onboarding)');
    });
  });

  describe('alreadyOnboardedGuard', () => {
    it('shows the onboarding page to a new user', async () => {
      mockOnboardingService.getStatus.mockReturnValue(of(status(false, false)));
      expect(await run(alreadyOnboardedGuard, '/onboarding')).toBe(true);
    });

    it('sends an onboarded user with an incomplete profile to the stepper', async () => {
      mockOnboardingService.getStatus.mockReturnValue(of(status(true, false)));
      expect(await run(alreadyOnboardedGuard, '/onboarding')).toBe('UrlTree(/profile?step=coordonnees)');
    });

    it('sends a fully completed user to /offers', async () => {
      mockOnboardingService.getStatus.mockReturnValue(of(status(true, true)));
      expect(await run(alreadyOnboardedGuard, '/onboarding')).toBe('UrlTree(/offers)');
    });
  });
});
