import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ThemeService, type LumoTheme } from '../../core/services/theme.service';

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
        Profiili, kieli ja sijaintipalvelut kytketään vaiheissa 12–13.
      </p>
    </main>
  `,
  styleUrl: './settings.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SettingsComponent {
  protected readonly theme = inject(ThemeService);

  protected readonly themeOptions: ReadonlyArray<{ value: LumoTheme; label: string }> = [
    { value: 'light', label: 'Vaalea' },
    { value: 'dark', label: 'Tumma' },
    { value: 'system', label: 'Järjestelmä' },
  ];
}
