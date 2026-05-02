import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';

import { UserApiService } from '../../core/services/user-api.service';
import { greetingFor } from '../../core/services/greeting';
import type { UserProfileDto } from '../../core/models/user-profile.dto';

@Component({
  selector: 'app-home',
  imports: [RouterLink],
  template: `
    <div class="home">
      <section class="greeting">
        <h1>{{ greeting() }}, {{ greetingName() }}</h1>
        <p>Aihio-vaihe 11/14 — bottom tab bar mobiilissa, sidebar desktopissa.</p>
      </section>

      <section>
        <h2 class="section-h">Saatavilla nyt</h2>
        <div class="cards">
          <a class="card card--available" routerLink="/settings">
            <h3>Asetukset</h3>
            <p>Mieluisin nimi, teema, kieli ja sijaintipalvelut.</p>
          </a>
        </div>
      </section>

      <section>
        <h2 class="section-h">Tulossa</h2>
        <div class="cards">
          <article class="card card--coming">
            <span class="badge">Tulossa</span>
            <h3>Asiakkaat</h3>
            <p>Asiakkaiden haku, kortit ja yhteystiedot.</p>
          </article>

          <article class="card card--coming">
            <span class="badge">Tulossa</span>
            <h3>Sopimukset</h3>
            <p>Vuokrasopimukset, tilanne ja päättymisaikataulut.</p>
          </article>

          <article class="card card--coming">
            <span class="badge">Tulossa</span>
            <h3>Kohteet</h3>
            <p>Huoneistot, esittelyt ja tarkastukset.</p>
          </article>
        </div>
      </section>
    </div>
  `,
  styleUrl: './home.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HomeComponent {
  private readonly api = inject(UserApiService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly profile = signal<UserProfileDto | null>(null);
  protected readonly greeting = signal(greetingFor());

  protected readonly greetingName = computed(() => {
    const p = this.profile();
    return p?.preferredName?.trim() || p?.displayName || 'Lumolainen';
  });

  constructor() {
    this.api.getProfile()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (p) => this.profile.set(p),
        error: (err) => console.error('[Home] profile load failed', err),
      });
  }
}
