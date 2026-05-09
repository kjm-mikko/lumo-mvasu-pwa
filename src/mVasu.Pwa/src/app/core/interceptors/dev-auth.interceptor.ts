import { Injectable, inject } from '@angular/core';
import {
  HttpEvent,
  HttpHandler,
  HttpInterceptor,
  HttpRequest,
} from '@angular/common/http';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import { DevAuthService } from '../services/dev-auth.service';

/**
 * Replaces MsalInterceptor when `environment.devAuth.enabled` is true:
 * stamps an `X-Dev-User: <email>` header on every API request that lands
 * on `apiBaseUrl`. Backend must run with
 * `Development:DevHeaderAuth:Enabled=true` for the header to be honored.
 *
 * Never registered in production — see app.config.ts conditional provider.
 */
@Injectable()
export class DevAuthInterceptor implements HttpInterceptor {
  private readonly devAuth = inject(DevAuthService);

  intercept(req: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    if (!environment.devAuth?.enabled) {
      return next.handle(req);
    }
    if (!req.url.startsWith(environment.apiBaseUrl)) {
      return next.handle(req);
    }
    const user = this.devAuth.currentUser();
    if (!user) {
      return next.handle(req);
    }
    return next.handle(req.clone({ setHeaders: { 'X-Dev-User': user.email } }));
  }
}
