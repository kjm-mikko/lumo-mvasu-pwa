import { CanActivateFn, Router } from '@angular/router';
import { MsalGuard } from '@azure/msal-angular';
import { inject } from '@angular/core';

import { environment } from '../../../environments/environment';
import { DevAuthService } from '../services/dev-auth.service';

/**
 * Delegates to MSAL's CanActivate guard, which redirects unauthenticated
 * users to /auth/login (configured in app.config.ts via
 * MsalGuardConfiguration.loginFailedRoute) and preserves the original
 * target URL for the post-login redirect.
 *
 * When `environment.devAuth.enabled` is true, MSAL is bypassed and access
 * is gated on a DevAuthService user being selected.
 */
export const authGuard: CanActivateFn = (route, state) => {
  if (environment.devAuth?.enabled) {
    const dev = inject(DevAuthService);
    if (dev.currentUser() !== null) {
      return true;
    }
    return inject(Router).parseUrl('/auth/login');
  }
  const guard = inject(MsalGuard);
  return guard.canActivate(route, state);
};
