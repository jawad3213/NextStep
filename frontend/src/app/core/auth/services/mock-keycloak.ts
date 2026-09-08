/**
 * Fake Keycloak instance used in local-development mode
 * (environment.authEnabled === false). It mimics the minimal surface the
 * application consumes (AuthService, signalr.service.ts, auth.guard.ts) so the
 * whole app runs without a Keycloak server. The backend is in Auth:Mode=Dev
 * and ignores the token entirely; this object just needs to not crash.
 */
export const MOCK_KEYCLOAK = {
  authenticated: true,
  token: 'dev-token',
  tokenParsed: {
    sub: 'dev-user',
    email: 'dev@nextstep.local',
    preferred_username: 'dev-user',
  } as Record<string, unknown>,
  loadUserProfile: () =>
    Promise.resolve({
      id: 'dev-user',
      username: 'dev-user',
      firstName: 'Dev',
      lastName: 'User',
      email: 'dev@nextstep.local',
    }),
  login: () => Promise.resolve(),
  register: () => Promise.resolve(),
  logout: () => Promise.resolve(),
  hasRealmRole: () => false,
  hasResourceRole: () => false,
  isTokenExpired: () => false,
} as const;