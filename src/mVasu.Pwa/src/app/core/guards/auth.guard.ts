import { CanActivateFn } from '@angular/router';
import { MsalGuard } from '@azure/msal-angular';
import { inject } from '@angular/core';

/**
 * Delegates to MSAL's CanActivate guard, which redirects unauthenticated
 * users to /auth/login (configured in app.config.ts via
 * MsalGuardConfiguration.loginFailedRoute) and preserves the original
 * target URL for the post-login redirect.
 */
export const authGuard: CanActivateFn = (route, state) => {
  const guard = inject(MsalGuard);
  return guard.canActivate(route, state);
};
