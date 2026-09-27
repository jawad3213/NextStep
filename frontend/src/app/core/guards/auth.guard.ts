import { inject } from '@angular/core';
import { CanActivateFn } from '@angular/router';
import Keycloak from 'keycloak-js';

export const authGuard: CanActivateFn = async (route, state) => {
  const keycloak = inject(Keycloak);

  if (keycloak.authenticated) {
    // Si des rôles sont requis pour cette route, on pourrait les vérifier ici :
    // const requiredRoles = route.data['roles'] as Array<string>;
    // if (requiredRoles && !requiredRoles.some(role => keycloak.hasRealmRole(role))) {
    //   const router = inject(Router);
    //   router.navigate(['/access-denied']);
    //   return false;
    // }
    return true;
  }

  // Si l'utilisateur n'est pas connecté, on le redirige vers la mire de connexion
  await keycloak.login({
    redirectUri: globalThis.location.origin + state.url,
  });
  
  return false;
};
