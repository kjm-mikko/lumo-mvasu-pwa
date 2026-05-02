import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { WordmarkComponent } from '../../shared/wordmark/wordmark.component';

@Component({
  selector: 'app-home',
  imports: [RouterLink, WordmarkComponent],
  template: `
    <main class="home">
      <header>
        <lumo-wordmark size="sm" [showProduct]="true" />
      </header>

      <section class="greeting">
        <h1>Tervetuloa Lumo mVasuun</h1>
        <p>Aihio-vaihe 8/14 — frontend-runko valmis. Kirjautuminen kytketään vaiheessa 9.</p>
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
export class HomeComponent {}
