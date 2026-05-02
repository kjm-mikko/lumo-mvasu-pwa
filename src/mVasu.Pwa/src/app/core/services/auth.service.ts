import { Injectable, inject, signal, DestroyRef } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
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

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly msal = inject(MsalService);
  private readonly broadcast = inject(MsalBroadcastService);
  private readonly destroyRef = inject(DestroyRef);

  readonly account = signal<AccountInfo | null>(null);
  readonly isAuthenticated = signal<boolean>(false);
  readonly inProgress = signal<InteractionStatus>(InteractionStatus.Startup);

  constructor() {
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
    this.msal.loginRedirect({
      scopes: ['User.Read', environment.msal.apiScope],
    });
  }

  logoutRedirect(): void {
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
