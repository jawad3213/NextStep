/**
 * Configuration centralisée de l'environnement.
 * Toutes les URLs de l'API passent par ici pour éviter les chaînes en dur.
 */
const browserOrigin = typeof window !== 'undefined' ? window.location.origin : 'http://localhost:4200';

export const environment = {
  production: false,
  authEnabled: false,
  apiBaseUrl: `${browserOrigin}/api`,
  agentsBaseUrl: `${browserOrigin}/agents`,
  keycloakUrl: 'http://localhost:8080',
  frontendBaseUrl: browserOrigin,
};
