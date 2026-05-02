import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ThemeService, type LumoTheme } from '../../core/services/theme.service';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-settings',
  imports: [RouterLink],
  template: `
    <main class="settings">
      <a class="back" routerLink="/home">← Takaisin</a>
      <h1>Asetukset</h1>

      <section>
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

      <p class="placeholder">
        Profiili, kieli ja sijaintipalvelut kytketään /api/me-haun yhteyteen vaiheissa 10–13.
      </p>

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

  protected readonly themeOptions: ReadonlyArray<{ value: LumoTheme; label: string }> = [
    { value: 'light', label: 'Vaalea' },
    { value: 'dark', label: 'Tumma' },
    { value: 'system', label: 'Järjestelmä' },
  ];

  logout(): void {
    this.auth.logoutRedirect();
  }
}
