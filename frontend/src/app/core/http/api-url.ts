import { environment } from '@env/environment';

/** Base URL of the backend API, e.g. `http://localhost:5000/api`. */
export const API_BASE_URL = environment.apiBaseUrl;

/** Absolute URL of a backend API path: `apiUrl('/profile')` → `…/api/profile`. */
export function apiUrl(path = ''): string {
  return `${API_BASE_URL}${path}`;
}

/** Origin of the backend (without `/api`), for files and hubs it serves. */
export function backendOrigin(): string {
  return new URL(API_BASE_URL).origin;
}
