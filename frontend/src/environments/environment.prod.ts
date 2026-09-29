const browserOrigin = globalThis.location?.origin ?? 'http://localhost';

export const environment = {
  production: true,
  apiBaseUrl: `${browserOrigin}/api`,
  keycloakUrl: `${browserOrigin}/auth`,
  frontendBaseUrl: browserOrigin,
};

