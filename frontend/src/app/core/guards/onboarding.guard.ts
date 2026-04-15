import { inject } from '@angular/core';
import { Router, CanActivateFn } from '@angular/router';
import { OnboardingService } from '../../services/onboarding.service';
import { map, take } from 'rxjs';

export const onboardingGuard: CanActivateFn = () => {
  const onboardingService = inject(OnboardingService);
  const router = inject(Router);

  return onboardingService.getStatus().pipe(
    take(1),
    map(status => {
      // If user has not completed onboarding, redirect to onboarding page
      if (!status.onboardingCompleted) {
        return router.parseUrl('/onboarding');
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
