import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { MsalBroadcastService } from '@azure/msal-angular';
import { EventType, InteractionStatus } from '@azure/msal-browser';
import { filter, take } from 'rxjs/operators';

/**
 * Lands users on /home once MSAL finishes processing the redirect. The
 * actual handleRedirectObservable subscription lives in AppComponent so
 * the response is consumed even if the user never reaches this component
 * (handleRedirectPromise is single-shot). We just listen for the broadcast.
 */
@Component({
  selector: 'app-auth-callback',
  template: `<p class="status">Kirjaudutaan…</p>`,
  styles: [`
    .status {
      min-height: 100dvh;
      display: flex;
      align-items: center;
      justify-content: center;
      color: var(--lumo-text-secondary);
      font-size: var(--lumo-fs-body);
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuthCallbackComponent implements OnInit {
  private readonly broadcast = inject(MsalBroadcastService);
  private readonly router = inject(Router);

  ngOnInit(): void {
    this.broadcast.inProgress$
      .pipe(filter((status) => status === InteractionStatus.None), take(1))
      .subscribe(() => this.router.navigateByUrl('/home'));

    this.broadcast.msalSubject$
      .pipe(
        filter((m) => m.eventType === EventType.LOGIN_FAILURE),
        take(1),
      )
      .subscribe(() => this.router.navigateByUrl('/auth/login'));
  }
}
