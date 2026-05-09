import { Injectable, signal } from '@angular/core';

import { environment } from '../../../environments/environment';

/**
 * One entry from environment.devAuth.users — the developer-curated list
 * of mVasu users available in the local xVasu instance.
 */
export interface DevAuthUser {
  readonly email: string;
  readonly displayName: string;
}

const STORAGE_KEY = 'lumo.devAuth.user';

/**
 * Tracks the currently selected dev user when environment.devAuth.enabled
 * is true. The selected email is persisted in localStorage so a refresh
 * keeps the impersonation in place — matches the MSAL session-cache feel
 * for the bypass flow. Never used in production builds (the prod
 * environment file forces enabled=false).
 */
@Injectable({ providedIn: 'root' })
export class DevAuthService {
  private readonly _user = signal<DevAuthUser | null>(this.loadInitial());

  /** Reactive selector for the currently selected dev user, or null. */
  readonly currentUser = this._user.asReadonly();

  readonly availableUsers: ReadonlyArray<DevAuthUser> = environment.devAuth?.users ?? [];

  setUser(email: string): DevAuthUser | null {
    const match = this.availableUsers.find(u => u.email.toLowerCase() === email.toLowerCase());
    if (!match) {
      return null;
    }
    this._user.set(match);
    this.persist(match);
    return match;
  }

  clear(): void {
    this._user.set(null);
    this.persist(null);
  }

  private loadInitial(): DevAuthUser | null {
    if (!environment.devAuth?.enabled) {
      return null;
    }
    if (typeof localStorage === 'undefined') {
      return null;
    }
    const stored = localStorage.getItem(STORAGE_KEY);
    if (!stored) {
      return null;
    }
    return this.availableUsers.find(u => u.email.toLowerCase() === stored.toLowerCase()) ?? null;
  }

  private persist(user: DevAuthUser | null): void {
    if (typeof localStorage === 'undefined') {
      return;
    }
    if (user) {
      localStorage.setItem(STORAGE_KEY, user.email.toLowerCase());
    } else {
      localStorage.removeItem(STORAGE_KEY);
    }
  }
}
