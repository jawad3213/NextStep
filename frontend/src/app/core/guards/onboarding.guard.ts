import { inject } from '@angular/core';
import { Router, CanActivateFn } from '@angular/router';
import { OnboardingService } from '../../services/onboarding.service';
import { catchError, map, of, take } from 'rxjs';

/**
 * Access to the application is decided only by the backend (per user, stored in
 * the database), never by browser storage:
 *   1. onboardingCompleted = false -> /onboarding (the 3 onboarding questions)
 *   2. profileCompleted    = false -> /profile (stepper; "Finish" is validated server-side)
 *   3. both true                   -> full application
 */
export const onboardingGuard: CanActivateFn = (route, state) => {
  const onboardingService = inject(OnboardingService);
  const router = inject(Router);

  return onboardingService.getStatus().pipe(
    take(1),
    map(status => {
      if (!status.onboardingCompleted) {
        return router.parseUrl('/onboarding');
      }
      if (!status.profileCompleted && !state.url.startsWith('/profile')) {
        return router.parseUrl('/profile?step=coordonnees');
      }
      return true;
    }),
    // Fail closed: if the status cannot be read, do not open the application.
    catchError(() => of(router.parseUrl('/onboarding')))
  );
};

export const alreadyOnboardedGuard: CanActivateFn = () => {
  const onboardingService = inject(OnboardingService);
  const router = inject(Router);

  return onboardingService.getStatus().pipe(
    take(1),
    map(status => {
      if (!status.onboardingCompleted) {
        return true;
      }
      return router.parseUrl(status.profileCompleted ? '/offers' : '/profile?step=coordonnees');
    }),
    // The onboarding page itself stays reachable if the status cannot be read.
    catchError(() => of(true))
  );
};
