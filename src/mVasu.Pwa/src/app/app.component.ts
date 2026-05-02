import { Component, inject, OnInit } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { MsalService } from '@azure/msal-angular';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
})
export class AppComponent implements OnInit {
  private readonly msal = inject(MsalService);

  ngOnInit(): void {
    // MSAL Browser must be explicitly initialised before any login or redirect
    // operation. Subscribing to handleRedirectObservable lets MSAL pick up the
    // response when the user lands back on the redirect URI even if /auth/callback
    // has not yet been navigated to (handleRedirectPromise cannot be called twice).
    this.msal.initialize().subscribe(() => {
      this.msal.handleRedirectObservable().subscribe({
        next: (result) => {
          if (result?.account) {
            this.msal.instance.setActiveAccount(result.account);
          }
        },
        error: (error) => console.error('[MSAL] redirect handling failed', error),
      });
    });
  }
}
