import { inject } from '@angular/core';
import { Router, CanActivateFn } from '@angular/router';
import { OnboardingService } from '../../services/onboarding.service';
import { catchError, map, of, take } from 'rxjs';

export const onboardingGuard: CanActivateFn = (route, state) => {
  const onboardingService = inject(OnboardingService);
  const router = inject(Router);
  const profileUnlockedKey = 'nextstep_profile_unlocked';

  return onboardingService.getStatus().pipe(
    take(1),
    map(status => {
      if (!status.onboardingCompleted) {
        return router.parseUrl('/onboarding');
      }

      const isProfileRoute = state.url.startsWith('/profile');
      if (!isProfileRoute) {
        const profileUnlocked = localStorage.getItem(profileUnlockedKey) === 'true';
        if (!profileUnlocked && status.profileScore < 30) {
          return router.parseUrl('/profile?step=coordonnees');
        }
      }

      return true;
    }),
    catchError(() => {
      const devBypass = localStorage.getItem(profileUnlockedKey) === 'true';
      if (devBypass) return of(true);
      localStorage.setItem(profileUnlockedKey, 'true');
      return of(true);
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
        return router.parseUrl('/offers');
      }
      return true;
    })
  );
};
