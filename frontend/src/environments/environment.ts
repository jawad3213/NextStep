/**
 * Configuration centralisée de l'environnement.
 * Toutes les URLs de l'API passent par ici pour éviter les chaînes en dur.
 */
export const environment = {
  production: false,
  authEnabled: false,
  apiBaseUrl: 'http://localhost:5000/api',
  agentsBaseUrl: 'http://localhost:8000',
  keycloakUrl: 'http://localhost:8080',
  frontendBaseUrl: 'http://localhost:4200',
};
