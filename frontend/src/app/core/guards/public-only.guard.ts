import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import Keycloak from 'keycloak-js';

/**
 * The landing page is the public face of the product: it is what a visitor sees before logging in.
 * An already authenticated user has no business there, so they are sent straight to the
 * application. The onboarding guard on the shell then routes them to onboarding, profile or the
 * dashboard depending on what they have already completed.
 */
export const publicOnlyGuard: CanActivateFn = () => {
  const keycloak = inject(Keycloak);
  const router = inject(Router);

  return keycloak.authenticated ? router.parseUrl('/dashboard') : true;
};
