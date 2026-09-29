import { inject } from '@angular/core';
import { CanActivateFn } from '@angular/router';
import Keycloak from 'keycloak-js';

export const authGuard: CanActivateFn = async (route, state) => {
  const keycloak = inject(Keycloak);

  if (keycloak.authenticated) {
    // If specific roles are required for this route, they can be checked here:
    // const requiredRoles = route.data['roles'] as Array<string>;
    // if (requiredRoles && !requiredRoles.some(role => keycloak.hasRealmRole(role))) {
    //   const router = inject(Router);
    //   router.navigate(['/access-denied']);
    //   return false;
    // }
    return true;
  }

  // If user is not authenticated, redirect to login
  await keycloak.login({
    redirectUri: globalThis.location.origin + state.url,
  });
  
  return false;
};
