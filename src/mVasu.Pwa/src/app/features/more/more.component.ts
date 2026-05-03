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
import { DxButtonModule } from 'devextreme-angular/ui/button';
import { DxListModule } from 'devextreme-angular/ui/list';
import { DxPopupModule } from 'devextreme-angular/ui/popup';
import { DxToastModule } from 'devextreme-angular/ui/toast';

import { AuthService } from '../../core/services/auth.service';
import { LocationService } from '../../core/services/location.service';
import { ThemeService, type LumoTheme } from '../../core/services/theme.service';
import { UserApiService } from '../../core/services/user-api.service';
import type { UserProfileDto } from '../../core/models/user-profile.dto';

interface MoreRow {
  readonly id: string;
  readonly label: string;
  readonly icon?: string;
  /**
   * Visible item count for this row. Named `count` (not `badge`) on
   * purpose: dx-list treats an item's `badge` property as a built-in
   * field and renders its own bright-blue badge in addition to the
   * itemTemplate, which produced a duplicate badge that drifted on
   * hover/click. Avoid the collision by using a custom field name.
   */
  readonly count?: string;
  readonly value?: string;
  readonly disabled?: boolean;
  readonly hint?: string;
}

type ToastType = 'info' | 'success' | 'warning' | 'error';
interface ToastState {
  readonly visible: boolean;
  readonly message: string;
  readonly type: ToastType;
}
const TOAST_HIDDEN: ToastState = { visible: false, message: '', type: 'info' };

interface ThemeOption {
  readonly id: LumoTheme;
  readonly label: string;
}
const THEME_OPTIONS: ThemeOption[] = [
  { id: 'light',  label: 'Vaalea' },
  { id: 'dark',   label: 'Tumma' },
  { id: 'system', label: 'Käytä järjestelmäasetusta' },
];

const APP_VERSION = 'Lumo mVasu · v0.2.0';

/**
 * "Lisää" tab — fourth tab of the A+C navigation. Aggregates the user
 * profile card, the XAF module menu (ASMA + KIRE per NAVIGATION.md §4b)
 * and the app-level settings into one long-tail view. ASMA/KIRE rows are
 * placeholders until the corresponding module screens land; Sovellus
 * rows either deep-link to existing settings or open dx-popup pickers.
 */
@Component({
  selector: 'app-more',
  imports: [DxButtonModule, DxListModule, DxPopupModule, DxToastModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="more">
      <section class="topbar">
        <h1 class="title">Lisää</h1>
      </section>

      @if (profile(); as p) {
        <section class="me-card" aria-label="Käyttäjäprofiili">
          <span class="avatar" aria-hidden="true">{{ initialsOf(p) }}</span>
          <div class="info">
            <span class="name">{{ displayNameOf(p) }}</span>
            <span class="meta">{{ p.email }}</span>
          </div>
        </section>
      } @else if (profileError()) {
        <section class="me-card me-card--error" role="alert">
          <span class="avatar" aria-hidden="true">··</span>
          <div class="info">
            <span class="name">Profiilia ei voitu ladata</span>
            <span class="meta">Yritä päivittää sivu</span>
          </div>
        </section>
      }

      <section class="group">
        <h2 class="ds-eyebrow">ASMA</h2>
        <dx-list
          class="lumo-more-list"
          [dataSource]="asmaRows"
          keyExpr="id"
          itemTemplate="row"
          (onItemClick)="onModuleRow($event)"
        >
          <div *dxTemplate="let row of 'row'" class="more-row">
            <i class="row-icon dx-icon dx-icon-{{ row.icon }}" aria-hidden="true"></i>
            <span class="row-label">{{ row.label }}</span>
            @if (row.count) {
              <span class="row-count">{{ row.count }}</span>
            }
            <i class="row-chev dx-icon dx-icon-chevronright" aria-hidden="true"></i>
          </div>
        </dx-list>
      </section>

      <section class="group">
        <h2 class="ds-eyebrow">KIRE</h2>
        <dx-list
          class="lumo-more-list"
          [dataSource]="kireRows"
          keyExpr="id"
          itemTemplate="row"
          (onItemClick)="onModuleRow($event)"
        >
          <div *dxTemplate="let row of 'row'" class="more-row">
            <i class="row-icon dx-icon dx-icon-{{ row.icon }}" aria-hidden="true"></i>
            <span class="row-label">{{ row.label }}</span>
            @if (row.count) {
              <span class="row-count">{{ row.count }}</span>
            }
            <i class="row-chev dx-icon dx-icon-chevronright" aria-hidden="true"></i>
          </div>
        </dx-list>
      </section>

      <section class="group">
        <h2 class="ds-eyebrow">Sovellus</h2>
        <dx-list
          class="lumo-more-list"
          [dataSource]="sovellusRows()"
          keyExpr="id"
          itemTemplate="row"
          (onItemClick)="onSovellusRow($event)"
        >
          <div
            *dxTemplate="let row of 'row'"
            class="more-row"
            [class.more-row--disabled]="row.disabled"
          >
            <span class="row-label">{{ row.label }}</span>
            @if (row.value) {
              <span class="row-value">{{ row.value }}</span>
            }
            @if (!row.disabled) {
              <i class="row-chev dx-icon dx-icon-chevronright" aria-hidden="true"></i>
            } @else if (row.hint) {
              <span class="row-hint">{{ row.hint }}</span>
            }
          </div>
        </dx-list>
      </section>

      <dx-button
        class="logout"
        text="Kirjaudu ulos"
        type="danger"
        stylingMode="outlined"
        (onClick)="logout()"
      ></dx-button>

      <p class="version">{{ appVersion }}</p>
    </main>

    <dx-popup
      [visible]="themePickerVisible()"
      title="Teema"
      [width]="320"
      [height]="'auto'"
      [showCloseButton]="true"
      [hideOnOutsideClick]="true"
      (onHiding)="closeThemePicker()"
    >
      <div *dxTemplate="let _ of 'content'" class="picker-body">
        @for (opt of themeOptions; track opt.id) {
          <button
            type="button"
            class="picker-option"
            [class.picker-option--active]="opt.id === theme()"
            (click)="selectTheme(opt.id)"
          >
            <span>{{ opt.label }}</span>
            @if (opt.id === theme()) {
              <i class="dx-icon dx-icon-check" aria-hidden="true"></i>
            }
          </button>
        }
      </div>
    </dx-popup>

    <dx-toast
      [visible]="toast().visible"
      [message]="toast().message"
      [type]="toast().type"
      [displayTime]="2400"
      (onHiding)="onToastHide()"
    ></dx-toast>
  `,
  styleUrl: './more.component.scss',
})
export class MoreComponent {
  private readonly api = inject(UserApiService);
  private readonly auth = inject(AuthService);
  private readonly themeService = inject(ThemeService);
  private readonly location = inject(LocationService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly appVersion = APP_VERSION;
  protected readonly themeOptions = THEME_OPTIONS;

  protected readonly profile = signal<UserProfileDto | null>(null);
  protected readonly profileError = signal<boolean>(false);
  protected readonly toast = signal<ToastState>(TOAST_HIDDEN);
  protected readonly themePickerVisible = signal<boolean>(false);

  protected readonly theme = this.themeService.theme;

  /** ASMA module rows mirror NAVIGATION.md §2a (badges are mock for now). */
  protected readonly asmaRows: MoreRow[] = [
    { id: 'tarjoukset',         label: 'Tarjoukset',              icon: 'doc',     count: '12' },
    { id: 'tutustumiskaynnit',  label: 'Tutustumiskäynnit',       icon: 'event' },
    { id: 'yleisesittelyt',     label: 'Yleisesittelyt',          icon: 'event' },
    { id: 'varausesittelyt',    label: 'Varausesittelyt',         icon: 'event' },
    { id: 'valokuvaukset',      label: 'Valokuvaukset',           icon: 'photo' },
    { id: 'liidit',             label: 'Liidit',                  icon: 'group',   count: '14' },
    { id: 'allekirjoitettavat', label: 'Allekirjoitettavat',      icon: 'edit',    count: '3' },
    { id: 'irtisanomiset',      label: 'Saapuneet irtisanomiset', icon: 'mention', count: '1' },
  ];

  /** KIRE module rows mirror NAVIGATION.md §2b. */
  protected readonly kireRows: MoreRow[] = [
    { id: 'asuinhuoneisto', label: 'Asuinhuoneisto', icon: 'home' },
    { id: 'talousyksikko',  label: 'Talousyksikkö',  icon: 'globe' },
    { id: 'remontit',       label: 'Remontit',       icon: 'wrench', count: '7' },
  ];

  /** Sovellus rows reflect live state — theme + location consent + landing-view stub. */
  protected readonly sovellusRows = computed<MoreRow[]>(() => [
    { id: 'asetukset',     label: 'Asetukset',         value: 'Profiili, ilmoitukset' },
    { id: 'teema',         label: 'Teema',             value: this.themeLabel() },
    { id: 'kieli',         label: 'Kieli',             value: 'Suomi' },
    { id: 'sijainti',      label: 'Sijaintipalvelut',  value: this.locationLabel() },
    {
      id: 'aloitusnakyma', label: 'Aloitusnäkymä',     value: 'Tehtävät',
      disabled: true,      hint: 'Tulossa',
    },
  ]);

  constructor() {
    this.api.getProfile()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (p) => this.profile.set(p),
        error: (err) => {
          console.error('[More] profile load failed', err);
          this.profileError.set(true);
        },
      });
  }

  protected initialsOf(p: UserProfileDto): string {
    const name = p.preferredName?.trim() || p.displayName;
    if (!name) return '··';
    const parts = name.split(/\s+/).filter(Boolean);
    if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
    return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
  }

  protected displayNameOf(p: UserProfileDto): string {
    return p.preferredName?.trim() || p.displayName || 'Lumolainen';
  }

  protected onModuleRow(event: { itemData?: MoreRow }): void {
    const row = event.itemData;
    if (!row) return;
    this.flash(`Moduuli tulossa: ${row.label}`, 'info');
  }

  protected onSovellusRow(event: { itemData?: MoreRow }): void {
    const row = event.itemData;
    if (!row || row.disabled) return;

    switch (row.id) {
      case 'asetukset':
        this.router.navigate(['/settings']);
        break;
      case 'teema':
        this.themePickerVisible.set(true);
        break;
      case 'kieli':
        this.flash('Vain suomi tällä erää', 'info');
        break;
      case 'sijainti':
        this.location.requestPermission().catch(() => {
          this.flash('Sijaintilupa hylätty', 'warning');
        });
        break;
      default:
        this.flash(`Tulossa: ${row.label}`, 'info');
    }
  }

  protected selectTheme(id: LumoTheme): void {
    this.themeService.setTheme(id);
    this.themePickerVisible.set(false);
    this.flash('Teema tallennettu', 'success');
  }

  protected closeThemePicker(): void {
    this.themePickerVisible.set(false);
  }

  protected logout(): void {
    this.auth.logoutRedirect();
  }

  protected onToastHide(): void {
    this.toast.set(TOAST_HIDDEN);
  }

  private themeLabel(): string {
    return THEME_OPTIONS.find(o => o.id === this.theme())?.label ?? 'Vaalea';
  }

  private locationLabel(): string {
    switch (this.location.permissionState()) {
      case 'granted':     return 'Sallittu';
      case 'denied':      return 'Estetty';
      case 'unsupported': return 'Ei tuettu';
      default:            return 'Ei vahvistettu';
    }
  }

  private flash(message: string, type: ToastType): void {
    this.toast.set({ visible: true, message, type });
  }
}
