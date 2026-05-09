import { Injectable, inject, signal, DestroyRef, effect } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { MsalBroadcastService, MsalService } from '@azure/msal-angular';
import {
  AccountInfo,
  AuthenticationResult,
  EventMessage,
  EventType,
  InteractionStatus,
} from '@azure/msal-browser';
import { filter } from 'rxjs/operators';

import { environment } from '../../../environments/environment';
import { DevAuthService } from './dev-auth.service';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly msal = inject(MsalService);
  private readonly broadcast = inject(MsalBroadcastService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly devAuth = inject(DevAuthService);
  private readonly router = inject(Router);

  readonly account = signal<AccountInfo | null>(null);
  readonly isAuthenticated = signal<boolean>(false);
  readonly inProgress = signal<InteractionStatus>(InteractionStatus.Startup);

  private readonly devMode = environment.devAuth?.enabled === true;

  constructor() {
    if (this.devMode) {
      // Dev bypass: source the auth state from DevAuthService and skip
      // every MSAL subscription. MSAL providers stay registered so types
      // line up, but we never call loginRedirect()/handleRedirectObservable
      // in this mode.
      effect(() => {
        const user = this.devAuth.currentUser();
        if (user) {
          this.account.set({
            homeAccountId: `dev::${user.email}`,
            environment: 'dev',
            tenantId: 'dev',
            username: user.email,
            localAccountId: user.email,
            name: user.displayName,
          } as AccountInfo);
          this.isAuthenticated.set(true);
        } else {
          this.account.set(null);
          this.isAuthenticated.set(false);
        }
        this.inProgress.set(InteractionStatus.None);
      });
      return;
    }

    this.broadcast.msalSubject$
      .pipe(
        filter((m: EventMessage) => m.eventType === EventType.LOGIN_SUCCESS
          || m.eventType === EventType.ACQUIRE_TOKEN_SUCCESS),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((m: EventMessage) => {
        const result = m.payload as AuthenticationResult | null;
        if (result?.account) {
          this.msal.instance.setActiveAccount(result.account);
        }
        this.refreshAccountState();
      });

    this.broadcast.inProgress$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((status) => {
        this.inProgress.set(status);
        if (status === InteractionStatus.None) {
          this.refreshAccountState();
        }
      });
  }

  loginRedirect(): void {
    if (this.devMode) {
      // Login UI handles dev-user selection itself; nothing to do here.
      this.router.navigateByUrl('/auth/login');
      return;
    }
    this.msal.loginRedirect({
      scopes: ['User.Read', environment.msal.apiScope],
    });
  }

  logoutRedirect(): void {
    if (this.devMode) {
      this.devAuth.clear();
      this.router.navigateByUrl('/auth/login');
      return;
    }
    this.msal.logoutRedirect({
      postLogoutRedirectUri: environment.msal.postLogoutRedirectUri,
    });
  }

  private refreshAccountState(): void {
    const active = this.msal.instance.getActiveAccount()
      ?? this.msal.instance.getAllAccounts()[0]
      ?? null;
    if (active && this.msal.instance.getActiveAccount() !== active) {
      this.msal.instance.setActiveAccount(active);
    }
    this.account.set(active);
    this.isAuthenticated.set(active !== null);
  }
}
