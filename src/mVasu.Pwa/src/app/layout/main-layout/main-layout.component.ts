import { ChangeDetectionStrategy, Component } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

import { WordmarkComponent } from '../../shared/wordmark/wordmark.component';

interface NavItem {
  readonly label: string;
  readonly route?: string;
  readonly disabled: boolean;
  readonly icon: 'home' | 'users' | 'contract' | 'gear';
}

@Component({
  selector: 'app-main-layout',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, WordmarkComponent, NgTemplateOutlet],
  template: `
    <div class="layout">
      <aside class="sidebar" aria-label="Päänavigaatio">
        <div class="brand">
          <lumo-wordmark size="sm" />
        </div>
        @for (item of navItems; track item.label) {
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
        @for (item of navItems; track item.label) {
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
          @case ('home') {
            <path d="M3 12 12 3l9 9" />
            <path d="M5 10v10h4v-6h6v6h4V10" />
          }
          @case ('users') {
            <circle cx="9" cy="8" r="3.5" />
            <circle cx="17.5" cy="9.5" r="2.5" />
            <path d="M3 20c0-3 3-5 6-5s6 2 6 5" />
            <path d="M15 17c.5-1.8 2.4-3 4.5-3 1.2 0 2.5.5 2.5 3" />
          }
          @case ('contract') {
            <path d="M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8z" />
            <path d="M14 3v5h5" />
            <path d="M9 13h6M9 17h6" />
          }
          @case ('gear') {
            <circle cx="12" cy="12" r="3" />
            <path d="M12 3v2.5M12 18.5V21M3 12h2.5M18.5 12H21M5.6 5.6l1.8 1.8M16.6 16.6l1.8 1.8M5.6 18.4l1.8-1.8M16.6 7.4l1.8-1.8" />
          }
        }
      </svg>
    </ng-template>
  `,
  styleUrl: './main-layout.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MainLayoutComponent {
  protected readonly navItems: ReadonlyArray<NavItem> = [
    { label: 'Etusivu', route: '/home', disabled: false, icon: 'home' },
    { label: 'Asiakkaat', disabled: true, icon: 'users' },
    { label: 'Sopimukset', disabled: true, icon: 'contract' },
    { label: 'Asetukset', route: '/settings', disabled: false, icon: 'gear' },
  ];
}
