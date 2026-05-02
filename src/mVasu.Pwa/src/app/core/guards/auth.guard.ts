import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';

/**
 * Phase 8 placeholder: lets every navigation through. Phase 9 wires this
 * to MSAL — guarded routes will then redirect unauthenticated users to
 * /auth/login while keeping the original target for post-login redirect.
 */
export const authGuard: CanActivateFn = (_route, _state) => {
  // Phase 9 will inject MsalService here and check IsAuthenticated.
  inject(Router); // keeps the import wired so phase 9 just adds the redirect.
  return true;
};
