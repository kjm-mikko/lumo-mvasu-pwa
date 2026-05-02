import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { WordmarkComponent } from '../../shared/wordmark/wordmark.component';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-home',
  imports: [RouterLink, WordmarkComponent],
  template: `
    <main class="home">
      <header>
        <lumo-wordmark size="sm" [showProduct]="true" />
        <span class="user">{{ displayName() }}</span>
      </header>

      <section class="greeting">
        <h1>Tervetuloa Lumo mVasuun</h1>
        <p>Aihio-vaihe 9/14 — Entra ID -kirjautuminen toimii. Profiilin haku /api/me-endpointista lisätään vaiheessa 10.</p>
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
  private readonly auth = inject(AuthService);

  readonly displayName = computed(() => {
    const account = this.auth.account();
    return account?.name ?? account?.username ?? '';
  });
}
