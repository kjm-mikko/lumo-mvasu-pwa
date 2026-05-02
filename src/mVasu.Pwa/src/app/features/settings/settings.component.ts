import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormBuilder, FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { firstValueFrom } from 'rxjs';

import { ThemeService, type LumoTheme } from '../../core/services/theme.service';
import { AuthService } from '../../core/services/auth.service';
import { UserApiService } from '../../core/services/user-api.service';
import { LocationService, type LocationPermission } from '../../core/services/location.service';
import type { UserProfileDto } from '../../core/models/user-profile.dto';
import { AvatarComponent } from '../../shared/avatar/avatar.component';

interface SettingsForm {
  preferredName: FormControl<string>;
  language: FormControl<string>;
}

interface LocationStatusView {
  readonly text: string;
  readonly tone: 'granted' | 'denied' | 'unsupported' | 'neutral';
  readonly hint?: string;
}

@Component({
  selector: 'app-settings',
  imports: [ReactiveFormsModule, AvatarComponent],
  template: `
    <main class="settings">
      <h1>Asetukset</h1>

      @if (profile(); as p) {
        <section class="profile">
          <lumo-avatar [name]="p.displayName" size="lg" />
          <div class="profile-fields">
            <p class="profile-name">{{ p.displayName }}</p>
            <p class="profile-email">{{ p.email }}</p>
          </div>
        </section>

        <form [formGroup]="form" (ngSubmit)="save()" class="form">
          <label class="field">
            <span>Mieluisin nimi</span>
            <input
              type="text"
              formControlName="preferredName"
              maxlength="100"
              placeholder="{{ p.displayName }}"
            />
          </label>

          <label class="field">
            <span>Kieli</span>
            <select formControlName="language">
              <option value="fi">Suomi</option>
              <option value="en">English</option>
            </select>
          </label>

          <section class="locations">
            <h2>Sijaintipalvelut</h2>
            <label class="toggle">
              <input
                type="checkbox"
                [checked]="p.locationConsent"
                [disabled]="locationBusy() || locationStatus().tone === 'unsupported'"
                (change)="onConsentChange($event)"
              />
              <span>Salli sijainnin käyttö</span>
            </label>
            <p
              class="status-line"
              [class.status-line--granted]="locationStatus().tone === 'granted'"
              [class.status-line--denied]="locationStatus().tone === 'denied'"
              [class.status-line--unsupported]="locationStatus().tone === 'unsupported'"
              aria-live="polite"
            >
              <span class="dot" aria-hidden="true"></span>
              {{ locationStatus().text }}
            </p>
            @if (locationStatus().hint; as hint) {
              <p class="hint">{{ hint }}</p>
            }
            <p class="helper">
              mVasu käyttää sijaintiasi näyttääkseen lähimmät kohteet ja ohjatakseen kartalla.
              Tietoa ei jaeta kolmansille osapuolille. Voit muuttaa lupaa milloin tahansa.
            </p>
          </section>

          <section class="theme">
            <h2>Teema</h2>
            @for (option of themeOptions; track option.value) {
              <label class="radio">
                <input
                  type="radio"
                  name="theme"
                  [value]="option.value"
                  [checked]="theme.theme() === option.value"
                  (change)="theme.setTheme(option.value)"
                />
                {{ option.label }}
              </label>
            }
          </section>

          @if (errorMessage(); as err) {
            <p class="error">{{ err }}</p>
          }

          <div class="actions">
            <button type="submit" class="primary" [disabled]="form.invalid || saving()">
              {{ saving() ? 'Tallennetaan…' : 'Tallenna muutokset' }}
            </button>
            <button type="button" class="secondary" (click)="reset(p)">Peruuta</button>
          </div>
        </form>
      } @else if (loadError()) {
        <p class="error">Profiilin lataus epäonnistui. Yritä uudelleen.</p>
      } @else {
        <p class="status">Ladataan profiilia…</p>
      }

      <button type="button" class="logout" (click)="logout()">
        Kirjaudu ulos
      </button>
    </main>
  `,
  styleUrl: './settings.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SettingsComponent {
  protected readonly theme = inject(ThemeService);
  private readonly auth = inject(AuthService);
  private readonly api = inject(UserApiService);
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly location = inject(LocationService);

  protected readonly profile = signal<UserProfileDto | null>(null);
  protected readonly saving = signal(false);
  protected readonly locationBusy = signal(false);
  protected readonly loadError = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly locationStatus = computed<LocationStatusView>(() => {
    const consent = this.profile()?.locationConsent ?? false;
    const permission = this.location.permissionState();
    return mapLocationStatus(consent, permission);
  });

  protected readonly themeOptions: ReadonlyArray<{ value: LumoTheme; label: string }> = [
    { value: 'light', label: 'Vaalea' },
    { value: 'dark', label: 'Tumma' },
    { value: 'system', label: 'Järjestelmä' },
  ];

  protected readonly form = this.fb.nonNullable.group<SettingsForm>({
    preferredName: this.fb.nonNullable.control('', [Validators.maxLength(100)]),
    language: this.fb.nonNullable.control('fi', [Validators.required]),
  });

  constructor() {
    this.api.getProfile()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (p) => this.applyProfile(p),
        error: (err) => {
          console.error('[Settings] profile load failed', err);
          this.loadError.set(true);
        },
      });
  }

  protected save(): void {
    if (this.form.invalid || this.saving()) return;
    this.saving.set(true);
    this.errorMessage.set(null);

    const raw = this.form.getRawValue();
    this.api.updateSettings({
      preferredName: raw.preferredName.trim() || null,
      theme: this.theme.theme(),
      language: raw.language,
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (p) => {
          this.applyProfile(p);
          this.saving.set(false);
        },
        error: (err) => {
          console.error('[Settings] save failed', err);
          this.errorMessage.set('Tallennus epäonnistui. Yritä uudelleen.');
          this.saving.set(false);
        },
      });
  }

  protected reset(p: UserProfileDto): void {
    this.form.reset({
      preferredName: p.preferredName ?? '',
      language: p.language || 'fi',
    });
    this.errorMessage.set(null);
  }

  protected async onConsentChange(event: Event): Promise<void> {
    const target = event.target as HTMLInputElement;
    const consent = target.checked;
    this.locationBusy.set(true);
    this.errorMessage.set(null);

    try {
      // Persist the consent flag first — server is the source of truth.
      const updated = await firstValueFrom(this.api.updateLocationConsent(consent));
      this.applyProfile(updated);

      if (consent) {
        // Trigger the browser permission prompt and, on success, push the
        // first reading to the API so the dev DB sees real coordinates.
        await this.captureLocation();
      }
    } catch (err) {
      console.error('[Settings] consent toggle failed', err);
      target.checked = !consent;
      this.errorMessage.set('Sijaintiluvan tallentaminen epäonnistui.');
    } finally {
      this.locationBusy.set(false);
    }
  }

  private async captureLocation(): Promise<void> {
    try {
      const pos = await this.location.getCurrent();
      await firstValueFrom(this.api.recordLocation({
        latitude: pos.coords.latitude,
        longitude: pos.coords.longitude,
        accuracy: pos.coords.accuracy ?? null,
        recordedAt: new Date(pos.timestamp).toISOString(),
      }));
    } catch (err) {
      const code = (err as GeolocationPositionError | undefined)?.code;
      // PERMISSION_DENIED (1) is reflected via the permission signal; no
      // dedicated error message needed — the status line already updates.
      if (code !== 1) {
        console.warn('[Settings] location capture failed', err);
        this.errorMessage.set('Sijainnin haku epäonnistui. Voit yrittää uudestaan myöhemmin.');
      }
    }
  }

  logout(): void {
    this.auth.logoutRedirect();
  }

  private applyProfile(p: UserProfileDto): void {
    this.profile.set(p);
    this.form.reset({
      preferredName: p.preferredName ?? '',
      language: p.language || 'fi',
    });

    const incomingTheme = p.theme as LumoTheme;
    if (incomingTheme === 'light' || incomingTheme === 'dark' || incomingTheme === 'system') {
      if (this.theme.theme() !== incomingTheme) {
        this.theme.setTheme(incomingTheme);
      }
    }
  }
}

function mapLocationStatus(consent: boolean, permission: LocationPermission): LocationStatusView {
  if (permission === 'unsupported') {
    return { text: 'Selain ei tue sijaintipalvelua', tone: 'unsupported' };
  }
  if (permission === 'denied') {
    return {
      text: 'Estetty selaimessa',
      tone: 'denied',
      hint: 'Avaa selaimen sijaintiasetukset salliaksesi sijainnin käytön tällä sivustolla.',
    };
  }
  if (consent && permission === 'granted') {
    return { text: 'Sallittu', tone: 'granted' };
  }
  return { text: 'Ei pyydetty', tone: 'neutral' };
}
