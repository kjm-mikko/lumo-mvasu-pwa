import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  signal,
} from '@angular/core';
import { Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DxAccordionModule } from 'devextreme-angular/ui/accordion';
import { DxButtonModule } from 'devextreme-angular/ui/button';
import { DxToastModule } from 'devextreme-angular/ui/toast';

import { greetingFor } from '../../core/services/greeting';
import { UserApiService } from '../../core/services/user-api.service';
import type { UserProfileDto } from '../../core/models/user-profile.dto';

type TileAccent = 'navy' | 'cta' | 'warn' | 'info';

interface HubTile {
  readonly id: string;
  readonly icon: string;
  readonly label: string;
  readonly count?: number;
  readonly accent?: TileAccent;
  /** Internal route to navigate to. When omitted, a "Tulossa" toast fires. */
  readonly route?: string;
}

interface HubPanel {
  readonly id: string;
  readonly title: string;
  readonly subtitle?: string;
  readonly count?: number;
  readonly tiles: ReadonlyArray<HubTile>;
}

type ToastType = 'info' | 'success' | 'warning' | 'error';
interface ToastState {
  readonly visible: boolean;
  readonly message: string;
  readonly type: ToastType;
}
const TOAST_HIDDEN: ToastState = { visible: false, message: '', type: 'info' };

/**
 * HomeHubScreen — the alternate landing view for users who prefer a
 * module hub over the C-side task queue (SCREENS.md §06, prototype
 * screens.jsx HomeHubScreen lines 577–647). Activated as the default
 * landing screen via Lisää → Sovellus → Aloitusnäkymä, persisted in
 * HomePreferenceService.
 *
 * Three accordion panels — Päätoiminnot, ASMA, KIRE — each containing
 * a responsive tile grid. Mock counts only; the real numbers come once
 * BACKEND.md aggregator + module endpoints are in place.
 */
@Component({
  selector: 'app-home-hub',
  imports: [DxAccordionModule, DxButtonModule, DxToastModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="home-hub">
      <section class="topbar">
        <div class="topbar-text">
          <span class="eyebrow">{{ greeting() }}{{ greetingName() ? ', ' + greetingName() : '' }}</span>
          <h1 class="title">Koti</h1>
        </div>
        <dx-button
          class="topbar-search"
          icon="search"
          stylingMode="text"
          [elementAttr]="{ 'aria-label': 'Avaa pikahaku' }"
          (onClick)="openSearch()"
        ></dx-button>
      </section>

      <dx-accordion
        class="lumo-hub-accordion"
        [dataSource]="panels"
        keyExpr="id"
        [collapsible]="true"
        [multiple]="true"
        [selectedItems]="defaultOpen"
        itemTitleTemplate="hubTitle"
        itemTemplate="hubBody"
      >
        <div *dxTemplate="let panel of 'hubTitle'" class="hub-title-row">
          <div class="hub-title-text">
            <span class="hub-title-label">{{ panel.title }}</span>
            @if (panel.subtitle) {
              <span class="hub-title-sub">{{ panel.subtitle }}</span>
            }
          </div>
          @if (panel.count != null) {
            <span class="hub-title-count">{{ panel.count }}</span>
          }
        </div>

        <div *dxTemplate="let panel of 'hubBody'" class="hub-grid">
          @for (tile of panel.tiles; track tile.id) {
            <button
              type="button"
              class="hub-tile"
              [attr.data-accent]="tile.accent ?? 'navy'"
              (click)="onTile(tile)"
            >
              <i class="hub-tile-icon dx-icon dx-icon-{{ tile.icon }}" aria-hidden="true"></i>
              <span class="hub-tile-label">{{ tile.label }}</span>
              @if (tile.count != null) {
                <span class="hub-tile-count">{{ tile.count }}</span>
              }
            </button>
          }
        </div>
      </dx-accordion>
    </main>

    <dx-toast
      [visible]="toast().visible"
      [message]="toast().message"
      [type]="toast().type"
      [displayTime]="2400"
      (onHiding)="onToastHide()"
    ></dx-toast>
  `,
  styleUrl: './home-hub.component.scss',
})
export class HomeHubComponent {
  private readonly api = inject(UserApiService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly greeting = signal(greetingFor());
  protected readonly profile = signal<UserProfileDto | null>(null);
  protected readonly toast = signal<ToastState>(TOAST_HIDDEN);

  protected readonly greetingName = computed(() => {
    const p = this.profile();
    if (!p) return '';
    return (p.preferredName?.trim() || p.displayName || '').split(/\s+/)[0] ?? '';
  });

  /** Mock counts mirror EXISTING_ENTITIES.md mappings; replace once /api/* exists. */
  protected readonly panels: HubPanel[] = [
    {
      id: 'paatoiminnot',
      title: 'Päätoiminnot',
      subtitle: 'Päivittäinen työ',
      tiles: [
        { id: 'tasks',          icon: 'check', label: 'Käyttäjän tehtävälista', count: 5, accent: 'cta',  route: '/tasks' },
        { id: 'desklist',       icon: 'menu',  label: 'Tiskilista',             count: 120 },
        { id: 'offers',         icon: 'doc',   label: 'Tarjoukset',             count: 12, accent: 'warn' },
        { id: 'reservations',   icon: 'event', label: 'Varausesittelyt',        count: 3 },
        { id: 'visits',         icon: 'event', label: 'Tutustumiskäynnit',      count: 4 },
        { id: 'open-houses',    icon: 'event', label: 'Yleisesittelyt',         count: 2 },
        { id: 'parking',        icon: 'key',   label: 'Autopaikka' },
      ],
    },
    {
      id: 'asma',
      title: 'ASMA',
      subtitle: 'Asiakkuus & sopimukset',
      count: 26,
      tiles: [
        { id: 'photos',         icon: 'photo',   label: 'Valokuvaukset',           count: 8 },
        { id: 'leads',          icon: 'group',   label: 'Liidit',                  count: 14 },
        { id: 'persons',        icon: 'user',    label: 'Henkilöt' },
        { id: 'sign-pending',   icon: 'edit',    label: 'Allekirjoitettavat',      count: 3, accent: 'cta' },
        { id: 'contracts',      icon: 'doc',     label: 'Sopimukset' },
        { id: 'e-signatures',   icon: 'edit',    label: 'Sähköiset allekirjoitukset' },
        { id: 'webshop',        icon: 'cart',    label: 'Lumo Verkkokauppa' },
        { id: 'inbox-term',     icon: 'mention', label: 'Saapuneet irtisanomiset', count: 1, accent: 'cta' },
        { id: 'contact-person', icon: 'user',    label: 'Yhteyshenkilö' },
        { id: 'company',        icon: 'home',    label: 'Yritys' },
      ],
    },
    {
      id: 'kire',
      title: 'KIRE',
      subtitle: 'Kiinteistöt & remontit',
      tiles: [
        { id: 'apartment',      icon: 'home',   label: 'Asuinhuoneisto' },
        { id: 'cost-unit',      icon: 'home',   label: 'Talousyksikkö' },
        { id: 'renovations',    icon: 'edit',   label: 'Remontit', count: 7, accent: 'warn' },
      ],
    },
  ];

  /** Päätoiminnot is open on first load; ASMA / KIRE collapsed (per spec). */
  protected readonly defaultOpen: HubPanel[] = [this.panels[0]];

  constructor() {
    this.api.getProfile()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (p) => this.profile.set(p),
        error: () => { /* greeting falls back to no name */ },
      });
  }

  protected openSearch(): void {
    this.router.navigate(['/search']);
  }

  protected onTile(tile: HubTile): void {
    if (tile.route) {
      this.router.navigate([tile.route]);
      return;
    }
    this.flash(`Moduuli tulossa: ${tile.label}`, 'info');
  }

  protected onToastHide(): void {
    this.toast.set(TOAST_HIDDEN);
  }

  private flash(message: string, type: ToastType): void {
    this.toast.set({ visible: true, message, type });
  }
}
