import { ChangeDetectionStrategy, Component } from '@angular/core';
import { WordmarkComponent } from '../../shared/wordmark/wordmark.component';

@Component({
  selector: 'app-login',
  imports: [WordmarkComponent],
  template: `
    <main class="login">
      <lumo-wordmark size="lg" [showProduct]="true" />
      <h1>Tervetuloa</h1>
      <p class="subhead">Kirjaudu Microsoft-tilillä jatkaaksesi.</p>

      <button type="button" class="cta" disabled>
        Kirjaudu Microsoft-tilillä
      </button>

      <a class="help" href="mailto:team.vasu&#64;lumo.fi">Tarvitsetko apua?</a>

      <footer>© Lumo Kodit IT · v0.1.0</footer>
    </main>
  `,
  styleUrl: './login.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoginComponent {}
