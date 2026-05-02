import { ChangeDetectionStrategy, Component } from '@angular/core';

/**
 * Phase 8 placeholder. Phase 9 will host the MSAL redirect handler here:
 * the route is registered now so MSAL configuration in phase 9 has a
 * stable redirect URI to use.
 */
@Component({
  selector: 'app-auth-callback',
  template: `<p>Kirjaudutaan…</p>`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuthCallbackComponent {}
