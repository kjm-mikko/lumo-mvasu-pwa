import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { toObservable } from '@angular/core/rxjs-interop';
import { filter, take } from 'rxjs/operators';
import { WordmarkComponent } from '../../shared/wordmark/wordmark.component';
import { AuthService } from '../../core/services/auth.service';
import { DevAuthService, DevAuthUser } from '../../core/services/dev-auth.service';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-login',
  imports: [WordmarkComponent],
  template: `
    <main class="login">
      <lumo-wordmark size="lg" [showProduct]="true" />
      <h1>Tervetuloa</h1>

      @if (devMode) {
        <p class="subhead">Dev-tila: valitse käyttäjä, jonka roolissa jatkat.</p>

        <ul class="dev-users">
          @for (user of devUsers; track user.email) {
            <li>
              <button type="button" class="dev-user" (click)="pickDevUser(user)">
                <span class="name">{{ user.displayName }}</span>
                <span class="email">{{ user.email }}</span>
              </button>
            </li>
          } @empty {
            <li class="empty">Ei dev-käyttäjiä konfiguroitu (environment.ts → devAuth.users).</li>
          }
        </ul>

        <p class="dev-note">
          MSAL on ohitettu, koska <code>environment.devAuth.enabled = true</code>.
          API:n on tultava käyntiin lipulla
          <code>Development:DevHeaderAuth:Enabled=true</code>.
        </p>
      } @else {
        <p class="subhead">Kirjaudu Microsoft-tilillä jatkaaksesi.</p>

        <button type="button" class="cta" (click)="login()">
          Kirjaudu Microsoft-tilillä
        </button>
      }

      <a class="help" href="mailto:team.vasu&#64;lumo.fi">Tarvitsetko apua?</a>

      <footer>© Lumo Kodit IT · v0.1.0</footer>
    </main>
  `,
  styleUrl: './login.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoginComponent {
  private readonly auth = inject(AuthService);
  private readonly devAuth = inject(DevAuthService);
  private readonly router = inject(Router);

  protected readonly devMode = environment.devAuth?.enabled === true;
  protected readonly devUsers = this.devAuth.availableUsers;

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

  pickDevUser(user: DevAuthUser): void {
    this.devAuth.setUser(user.email);
    this.router.navigateByUrl('/home');
  }
}
