import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { toObservable } from '@angular/core/rxjs-interop';
import { filter, take } from 'rxjs/operators';
import { WordmarkComponent } from '../../shared/wordmark/wordmark.component';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-login',
  imports: [WordmarkComponent],
  template: `
    <main class="login">
      <lumo-wordmark size="lg" [showProduct]="true" />
      <h1>Tervetuloa</h1>
      <p class="subhead">Kirjaudu Microsoft-tilillä jatkaaksesi.</p>

      <button type="button" class="cta" (click)="login()">
        Kirjaudu Microsoft-tilillä
      </button>

      <a class="help" href="mailto:team.vasu&#64;lumo.fi">Tarvitsetko apua?</a>

      <footer>© Lumo Kodit IT · v0.1.0</footer>
    </main>
  `,
  styleUrl: './login.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoginComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  constructor() {
    // If the user is already authenticated (e.g. revisits /auth/login), bounce
    // them straight to /home rather than showing the login button again.
    toObservable(this.auth.isAuthenticated)
      .pipe(filter(Boolean), take(1))
      .subscribe(() => this.router.navigateByUrl('/home'));
  }

  login(): void {
    this.auth.loginRedirect();
  }
}
