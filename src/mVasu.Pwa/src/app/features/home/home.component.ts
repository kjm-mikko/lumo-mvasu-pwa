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

import { WordmarkComponent } from '../../shared/wordmark/wordmark.component';
import { UserApiService } from '../../core/services/user-api.service';
import { greetingFor } from '../../core/services/greeting';
import type { UserProfileDto } from '../../core/models/user-profile.dto';

@Component({
  selector: 'app-home',
  imports: [RouterLink, WordmarkComponent],
  template: `
    <main class="home">
      <header>
        <lumo-wordmark size="sm" [showProduct]="true" />
        <span class="user">{{ headerName() }}</span>
      </header>

      <section class="greeting">
        <h1>{{ greeting() }}, {{ greetingName() }}</h1>
        <p>Aihio-vaihe 10/14 — profiili haetaan /api/me-endpointista, asetukset tallennetaan kantaan.</p>
      </section>

      <section class="cards">
        <a class="card card--available" routerLink="/settings">
          <h2>Asetukset</h2>
          <p>Mieluisin nimi, teema, kieli ja sijaintipalvelut.</p>
        </a>

        <article class="card card--coming">
          <span class="badge">Tulossa</span>
          <h2>Asiakkaat</h2>
        </article>

        <article class="card card--coming">
          <span class="badge">Tulossa</span>
          <h2>Sopimukset</h2>
        </article>

        <article class="card card--coming">
          <span class="badge">Tulossa</span>
          <h2>Kohteet</h2>
        </article>
      </section>
    </main>
  `,
  styleUrl: './home.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HomeComponent {
  private readonly api = inject(UserApiService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly profile = signal<UserProfileDto | null>(null);
  protected readonly greeting = signal(greetingFor());

  protected readonly headerName = computed(() => {
    const p = this.profile();
    return p?.preferredName?.trim() || p?.displayName || '';
  });

  protected readonly greetingName = computed(() => this.headerName() || 'Lumolainen');

  constructor() {
    this.api.getProfile()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (p) => this.profile.set(p),
        error: (err) => console.error('[Home] profile load failed', err),
      });
  }
}
