import { inject } from '@angular/core';
import { CanActivateFn } from '@angular/router';
import Keycloak from 'keycloak-js';

/**
 * Entry point of the authenticated application shell.
 *
 * Unauthenticated users are sent through the standard Keycloak login and come back to the URL
 * they asked for, except the marketing pages: the landing page is only for visitors, so coming
 * back to it after a successful login would strand the user in front of a "log in" button they
 * just used. Those pages redirect to the application instead, where the onboarding guard decides
 * whether the user lands on onboarding, profile or the dashboard.
 */
export const authGuard: CanActivateFn = async (route, state) => {
  const keycloak = inject(Keycloak);

  if (keycloak.authenticated) {
    return true;
  }

  // If user is not authenticated, redirect to login
  await keycloak.login({
    redirectUri: globalThis.location.origin + postLoginUrl(state.url),
  });

  return false;
};

const PUBLIC_PAGES = ['/', '/landing', '/signup'];

/** Keeps a visitor away from the marketing pages once they are authenticated. */
export function postLoginUrl(url: string): string {
  return PUBLIC_PAGES.includes(url) ? '/dashboard' : url;
}
