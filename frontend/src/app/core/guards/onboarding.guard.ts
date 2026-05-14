import { inject } from '@angular/core';
import { Router, CanActivateFn } from '@angular/router';
import { OnboardingService } from '../../services/onboarding.service';
import { map, take } from 'rxjs';

export const onboardingGuard: CanActivateFn = (route, state) => {
  const onboardingService = inject(OnboardingService);
  const router = inject(Router);
  const profileUnlockedKey = 'nextstep_profile_unlocked';

  return onboardingService.getStatus().pipe(
    take(1),
    map(status => {
      // 1. Must always complete soft onboarding questions first!
      if (!status.onboardingCompleted) {
        return router.parseUrl('/onboarding');
      }

      // 2. Once questions are done, they must complete the profile stepper to access other sections
      const isProfileRoute = state.url.startsWith('/profile');
      if (!isProfileRoute) {
        const profileUnlocked = localStorage.getItem(profileUnlockedKey) === 'true';
        if (!profileUnlocked && status.profileScore < 30) {
          return router.parseUrl('/profile?step=coordonnees');
        }
      }

      return true;
    })
  );
};

export const alreadyOnboardedGuard: CanActivateFn = () => {
  const onboardingService = inject(OnboardingService);
  const router = inject(Router);

  return onboardingService.getStatus().pipe(
    take(1),
    map(status => {
      // If user HAS completed onboarding, don't let them back into onboarding page
      if (status.onboardingCompleted) {
        return router.parseUrl('/dashboard');
      }
      return true;
    })
  );
};
