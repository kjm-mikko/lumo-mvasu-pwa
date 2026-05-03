import { Injectable, effect, signal } from '@angular/core';

/**
 * Two valid landing screens for the A+C navigation:
 * - 'tasks' (default) drops the user into the daily task queue (C-side)
 * - 'hub'             drops the user into the HomeHubScreen accordion of
 *                     module tiles (B-side, "hub & spoke")
 */
export type HomePreference = 'tasks' | 'hub';

const STORAGE_KEY = 'lumo-home-preference';
const DEFAULT_PREFERENCE: HomePreference = 'tasks';

@Injectable({ providedIn: 'root' })
export class HomePreferenceService {
  /**
   * Reactive signal holding the user's chosen landing screen. The root
   * route redirect reads this on every navigation so flipping the
   * preference takes effect on the next "/" hit without a reload.
   */
  readonly preference = signal<HomePreference>(this.readStored());

  constructor() {
    effect(() => this.persist(this.preference()));
  }

  setPreference(value: HomePreference): void {
    this.preference.set(value);
  }

  /** Path to redirect to when the user lands on "" or "**". */
  homeRoutePath(): string {
    return this.preference() === 'hub' ? '/home-hub' : '/tasks';
  }

  private readStored(): HomePreference {
    if (typeof localStorage === 'undefined') return DEFAULT_PREFERENCE;
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw === 'hub' || raw === 'tasks' ? raw : DEFAULT_PREFERENCE;
  }

  private persist(value: HomePreference): void {
    if (typeof localStorage === 'undefined') return;
    try {
      localStorage.setItem(STORAGE_KEY, value);
    } catch {
      // Quota / privacy mode — fall back to in-memory only.
    }
  }
}
