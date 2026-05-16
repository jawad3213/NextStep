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
        if (!profileUnlocked) {
          return router.parseUrl('/profile?step=coordonnees');
        }
      }

      return true;
    }),
    catchError(() => {
      const devBypass = localStorage.getItem(profileUnlockedKey) === 'true';
      if (devBypass) return of(true);
      const softOnboardingDone = localStorage.getItem('nextstep_soft_onboarding_done') === 'true';
      if (softOnboardingDone) {
        return of(router.parseUrl('/profile?step=coordonnees'));
      }
      return of(router.parseUrl('/onboarding'));
    })
  );
};

export const alreadyOnboardedGuard: CanActivateFn = () => {
  const onboardingService = inject(OnboardingService);
  const router = inject(Router);

  return onboardingService.getStatus().pipe(
    take(1),
    map(status => {
      if (status.onboardingCompleted) {
        return router.parseUrl('/offers');
      }
      return true;
    }),
    catchError(() => {
      const profileUnlocked = localStorage.getItem('nextstep_profile_unlocked') === 'true';
      const softOnboardingDone = localStorage.getItem('nextstep_soft_onboarding_done') === 'true';
      if (profileUnlocked || softOnboardingDone) {
        return of(router.parseUrl('/offers'));
      }
      return of(true);
    })
  );
};
