/**
 * Centralized environment configuration.
 * All API URLs go through here to avoid hardcoded strings.
 */
export const environment = {
  production: false,
  apiBaseUrl: 'http://localhost:5000/api',
  keycloakUrl: 'http://localhost:8080',
  frontendBaseUrl: 'http://localhost:4200',
};
