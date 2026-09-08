import { InjectionToken } from '@angular/core';

export interface AuthConfig {
  /** Whether Keycloak authentication is active (true in prod, false in local dev). */
  authEnabled: boolean;
}

/**
 * Central auth toggle. Provided from the environment in app.config.ts and
 * injectable anywhere so guards/business code are decoupled from the static
 * environment import (and remain unit-testable).
 */
export const AUTH_CONFIG = new InjectionToken<AuthConfig>('auth-config');