import {
  ApplicationConfig,
  provideZoneChangeDetection,
  isDevMode,
} from '@angular/core';
import {
  provideRouter,
  withComponentInputBinding,
  withInMemoryScrolling,
  withRouterConfig,
  RouteReuseStrategy,
} from '@angular/router';
import { LumoRouteReuseStrategy } from './core/routing/lumo-route-reuse-strategy';
import { provideServiceWorker } from '@angular/service-worker';
import {
  HTTP_INTERCEPTORS,
  provideHttpClient,
  withInterceptorsFromDi,
} from '@angular/common/http';
import {
  IPublicClientApplication,
  PublicClientApplication,
  InteractionType,
  BrowserCacheLocation,
  LogLevel,
} from '@azure/msal-browser';
import {
  MSAL_INSTANCE,
  MSAL_GUARD_CONFIG,
  MSAL_INTERCEPTOR_CONFIG,
  MsalBroadcastService,
  MsalGuard,
  MsalGuardConfiguration,
  MsalInterceptor,
  MsalInterceptorConfiguration,
  MsalService,
} from '@azure/msal-angular';

import { routes } from './app.routes';
import { environment } from '../environments/environment';

function msalInstanceFactory(): IPublicClientApplication {
  return new PublicClientApplication({
    auth: {
      clientId: environment.msal.clientId,
      authority: environment.msal.authority,
      redirectUri: environment.msal.redirectUri,
      postLogoutRedirectUri: environment.msal.postLogoutRedirectUri,
      navigateToLoginRequestUrl: true,
    },
    cache: {
      cacheLocation: BrowserCacheLocation.SessionStorage,
      storeAuthStateInCookie: false,
    },
    system: {
      loggerOptions: {
        loggerCallback: (level, message) => {
          if (level === LogLevel.Error) {
            console.error('[MSAL]', message);
          }
        },
        logLevel: isDevMode() ? LogLevel.Warning : LogLevel.Error,
        piiLoggingEnabled: false,
      },
    },
  });
}

function msalGuardConfigFactory(): MsalGuardConfiguration {
  return {
    interactionType: InteractionType.Redirect,
    authRequest: {
      scopes: ['User.Read', environment.msal.apiScope],
    },
    loginFailedRoute: '/auth/login',
  };
}

function msalInterceptorConfigFactory(): MsalInterceptorConfiguration {
  const protectedResourceMap = new Map<string, Array<string>>([
    [`${environment.apiBaseUrl}/api/me`, [environment.msal.apiScope]],
    [`${environment.apiBaseUrl}/api/me/`, [environment.msal.apiScope]],
    [`${environment.apiBaseUrl}/api/tiskilista`, [environment.msal.apiScope]],
    [`${environment.apiBaseUrl}/api/tiskilista/`, [environment.msal.apiScope]],
    [`${environment.apiBaseUrl}/api/tasks`, [environment.msal.apiScope]],
    [`${environment.apiBaseUrl}/api/tasks/`, [environment.msal.apiScope]],
    [`${environment.apiBaseUrl}/api/customers`, [environment.msal.apiScope]],
    [`${environment.apiBaseUrl}/api/customers/`, [environment.msal.apiScope]],
  ]);
  return {
    interactionType: InteractionType.Redirect,
    protectedResourceMap,
  };
}

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(
      routes,
      withInMemoryScrolling({ scrollPositionRestoration: 'enabled', anchorScrolling: 'enabled' }),
      withRouterConfig({ paramsInheritanceStrategy: 'always' }),
      withComponentInputBinding(),
    ),
    { provide: RouteReuseStrategy, useClass: LumoRouteReuseStrategy },
    provideServiceWorker('ngsw-worker.js', {
      enabled: !isDevMode(),
      registrationStrategy: 'registerWhenStable:30000',
    }),
    provideHttpClient(withInterceptorsFromDi()),
    {
      provide: HTTP_INTERCEPTORS,
      useClass: MsalInterceptor,
      multi: true,
    },
    { provide: MSAL_INSTANCE, useFactory: msalInstanceFactory },
    { provide: MSAL_GUARD_CONFIG, useFactory: msalGuardConfigFactory },
    { provide: MSAL_INTERCEPTOR_CONFIG, useFactory: msalInterceptorConfigFactory },
    MsalService,
    MsalGuard,
    MsalBroadcastService,
  ],
};
