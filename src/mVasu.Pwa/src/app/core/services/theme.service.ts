import { Injectable, signal, effect, inject } from '@angular/core';
import { DOCUMENT } from '@angular/common';

export type LumoTheme = 'light' | 'dark' | 'system';

const STORAGE_KEY = 'lumo-theme';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly document = inject(DOCUMENT);

  readonly theme = signal<LumoTheme>(this.readStoredTheme());

  constructor() {
    effect(() => this.applyTheme(this.theme()));
  }

  setTheme(value: LumoTheme): void {
    this.theme.set(value);
    try {
      localStorage.setItem(STORAGE_KEY, value);
    } catch {
      // sessionStorage unavailable (private mode etc.) — keep in-memory only
    }
  }

  private readStoredTheme(): LumoTheme {
    try {
      const stored = localStorage.getItem(STORAGE_KEY);
      if (stored === 'light' || stored === 'dark' || stored === 'system') {
        return stored;
      }
    } catch {
      // ignored
    }
    return 'system';
  }

  private applyTheme(value: LumoTheme): void {
    const body = this.document.body;
    body.classList.remove('theme-light', 'theme-dark');
    if (value === 'light') body.classList.add('theme-light');
    if (value === 'dark') body.classList.add('theme-dark');
    // 'system' leaves no class so prefers-color-scheme media query in
    // lumo-tokens.css decides.
  }
}
