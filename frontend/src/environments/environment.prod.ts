const browserOrigin = globalThis.location?.origin ?? 'http://localhost';

export const environment = {
  production: true,
  authEnabled: true,
  apiBaseUrl: `${browserOrigin}/api`,
  agentsBaseUrl: `${browserOrigin}/agents`,
  keycloakUrl: `${browserOrigin}/auth`,
  frontendBaseUrl: browserOrigin,
};

