import { ApplicationConfig, EnvironmentProviders, Provider, provideZoneChangeDetection } from '@angular/core';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import Keycloak from 'keycloak-js';
import { routes } from './app.routes';
import { environment } from '../environments/environment';
import { AUTH_CONFIG } from './core/auth/auth-config.token';
import { MOCK_KEYCLOAK } from './core/auth/services/mock-keycloak';
import { apiErrorInterceptor } from './core/http/api-error.interceptor';
import {
  provideKeycloak,
  withAutoRefreshToken,
  AutoRefreshTokenService,
  UserActivityService,
  createInterceptorCondition,
  IncludeBearerTokenCondition,
  INCLUDE_BEARER_TOKEN_INTERCEPTOR_CONFIG,
  includeBearerTokenInterceptor,
} from 'keycloak-angular';

const escapedApiBaseUrl = environment.apiBaseUrl.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
const apiTokenCondition = createInterceptorCondition<IncludeBearerTokenCondition>({
  urlPattern: new RegExp(`^${escapedApiBaseUrl}/.*`, 'i'),
  bearerPrefix: 'Bearer',
});

function buildAuthProviders(): Array<Provider | EnvironmentProviders> {
  if (!environment.authEnabled) {
    // Local-development mode: no Keycloak server. Provide a fake instance that
    // satisfies the DI token so all consumers keep working unchanged. The
    // backend runs in Auth:Mode=Dev and authenticates every request as the
    // seeded "dev-user".
    return [{ provide: Keycloak, useValue: MOCK_KEYCLOAK as unknown as Keycloak }];
  }

  return [
    provideKeycloak({
      config: {
        url: environment.keycloakUrl,
        realm: 'Next-Step',
        clientId: 'nextstep-frontend',
      },
      initOptions: {
        onLoad: 'login-required',
        checkLoginIframe: false,
        pkceMethod: false,
        silentCheckSsoRedirectUri: globalThis.location.origin + '/silent-check-sso.html',
      },
      features: [
        withAutoRefreshToken({
          onInactivityTimeout: 'logout',
        }),
      ],
      providers: [
        AutoRefreshTokenService,
        UserActivityService,
        {
          provide: INCLUDE_BEARER_TOKEN_INTERCEPTOR_CONFIG,
          useValue: [apiTokenCondition],
        },
      ],
    }),
  ];
}

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideAnimationsAsync(),
    { provide: AUTH_CONFIG, useValue: { authEnabled: environment.authEnabled } },

    // Bearer interceptor is only needed when Keycloak is active.
    environment.authEnabled
      ? provideHttpClient(withInterceptors([includeBearerTokenInterceptor, apiErrorInterceptor]))
      : provideHttpClient(withInterceptors([apiErrorInterceptor])),

    ...buildAuthProviders(),
  ],
};