import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

import { HomePreferenceService } from '../../core/services/home-preference.service';
import { WordmarkComponent } from '../../shared/wordmark/wordmark.component';

interface NavItem {
  readonly label: string;
  readonly route?: string;
  readonly disabled: boolean;
  readonly icon: 'tasks' | 'home' | 'list' | 'users' | 'more';
}

@Component({
  selector: 'app-main-layout',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, WordmarkComponent, NgTemplateOutlet],
  template: `
    <div class="layout">
      <aside class="sidebar" aria-label="Päänavigaatio">
        <a
          class="brand"
          routerLink="/"
          aria-label="Avaa aloitusnäkymä"
          title="Avaa aloitusnäkymä"
        >
          <lumo-wordmark size="sm" />
        </a>
        @for (item of navItems(); track item.label) {
          @if (item.route && !item.disabled) {
            <a
              class="nav-item"
              [routerLink]="item.route"
              routerLinkActive="active"
              [attr.aria-label]="item.label"
              [title]="item.label"
            >
              <ng-container [ngTemplateOutlet]="iconTemplate" [ngTemplateOutletContext]="{ $implicit: item.icon }" />
            </a>
          } @else {
            <span
              class="nav-item nav-item--disabled"
              [attr.aria-label]="item.label + ' (tulossa)'"
              [title]="item.label + ' — tulossa'"
              aria-disabled="true"
            >
              <ng-container [ngTemplateOutlet]="iconTemplate" [ngTemplateOutletContext]="{ $implicit: item.icon }" />
            </span>
          }
        }
      </aside>

      <main class="content">
        <router-outlet />
      </main>

      <nav class="tabbar" aria-label="Päänavigaatio (mobiili)">
        @for (item of navItems(); track item.label) {
          @if (item.route && !item.disabled) {
            <a
              class="tab"
              [routerLink]="item.route"
              routerLinkActive="active"
              [attr.aria-label]="item.label"
            >
              <ng-container [ngTemplateOutlet]="iconTemplate" [ngTemplateOutletContext]="{ $implicit: item.icon }" />
              <span class="tab-label">{{ item.label }}</span>
            </a>
          } @else {
            <span
              class="tab tab--disabled"
              [attr.aria-label]="item.label + ' (tulossa)'"
              aria-disabled="true"
            >
              <ng-container [ngTemplateOutlet]="iconTemplate" [ngTemplateOutletContext]="{ $implicit: item.icon }" />
              <span class="tab-label">{{ item.label }}</span>
            </span>
          }
        }
      </nav>
    </div>

    <ng-template #iconTemplate let-icon>
      <svg
        class="icon"
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        stroke-width="2"
        stroke-linecap="round"
        stroke-linejoin="round"
        aria-hidden="true"
      >
        @switch (icon) {
          @case ('tasks') {
            <path d="M9 11l3 3 7-7" />
            <path d="M21 12v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11" />
          }
          @case ('home') {
            <path d="M3 12 12 3l9 9" />
            <path d="M5 10v10h4v-6h6v6h4V10" />
          }
          @case ('list') {
            <path d="M8 6h13M8 12h13M8 18h13" />
            <circle cx="3.5" cy="6"  r="1" fill="currentColor" />
            <circle cx="3.5" cy="12" r="1" fill="currentColor" />
            <circle cx="3.5" cy="18" r="1" fill="currentColor" />
          }
          @case ('users') {
            <circle cx="9" cy="8" r="3.5" />
            <circle cx="17.5" cy="9.5" r="2.5" />
            <path d="M3 20c0-3 3-5 6-5s6 2 6 5" />
            <path d="M15 17c.5-1.8 2.4-3 4.5-3 1.2 0 2.5.5 2.5 3" />
          }
          @case ('more') {
            <circle cx="5"  cy="12" r="1.5" fill="currentColor" />
            <circle cx="12" cy="12" r="1.5" fill="currentColor" />
            <circle cx="19" cy="12" r="1.5" fill="currentColor" />
          }
        }
      </svg>
    </ng-template>
  `,
  styleUrl: './main-layout.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MainLayoutComponent {
  private readonly homePreference = inject(HomePreferenceService);

  /**
   * First tab swaps with the user's chosen Aloitusnäkymä:
   * - 'tasks' (default) → "Tehtävät" linking to /tasks
   * - 'hub'             → "Koti"     linking to /home-hub
   *
   * Tiskilista is now active (merged from feature/tiskilista). Asukkaat
   * stays disabled until the People view lands. The Lumo wordmark in
   * the sidebar is also a back-stop link to / so a user navigated away
   * from their preferred home always has at least two paths back: the
   * dynamic first tab and the brand mark.
   */
  protected readonly navItems = computed<ReadonlyArray<NavItem>>(() => {
    const isHub = this.homePreference.preference() === 'hub';
    const first: NavItem = isHub
      ? { label: 'Koti',     route: '/home-hub', disabled: false, icon: 'home' }
      : { label: 'Tehtävät', route: '/tasks',    disabled: false, icon: 'tasks' };
    return [
      first,
      { label: 'Tiskilista', route: '/tiskilista', disabled: false, icon: 'list' },
      { label: 'Asiakkaat',  route: '/customers',  disabled: false, icon: 'users' },
      { label: 'Lisää',      route: '/more',       disabled: false, icon: 'more' },
    ];
  });
}
