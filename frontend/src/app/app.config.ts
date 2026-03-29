import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http'; 
import { routes } from './app.routes';
import { 
  provideKeycloak, 
  withAutoRefreshToken, 
  AutoRefreshTokenService, 
  UserActivityService, 
  createInterceptorCondition, 
  IncludeBearerTokenCondition, 
  INCLUDE_BEARER_TOKEN_INTERCEPTOR_CONFIG, 
  includeBearerTokenInterceptor,   
// ← L'intercepteur qui envoie le JWT 
} from 'keycloak-angular'; 

const apiTokenCondition = createInterceptorCondition<IncludeBearerTokenCondition>({  urlPattern: /^http:\/\/localhost:5000\/api\/.*/i, 
  bearerPrefix: 'Bearer', 
}); 
export const appConfig: ApplicationConfig = { 
  providers: [ 
    provideZoneChangeDetection({ eventCoalescing: true }), 
    provideRouter(routes), 

    provideHttpClient(withInterceptors([includeBearerTokenInterceptor])), 
    provideKeycloak({ 
      config: { 
        url: 'http://localhost:8080',       
        realm: 'Next-Step',                  
        clientId: 'nextstep-frontend',       
      }, 
      initOptions: { 
        onLoad: 'login-required', 
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
  ], 
}; 
