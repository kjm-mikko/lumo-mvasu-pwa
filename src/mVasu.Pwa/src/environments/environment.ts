export const environment = {
  production: false,
  apiBaseUrl: 'https://localhost:7216',
  msal: {
    clientId: 'd2f69daf-210b-4f64-b402-7914c4c9d0b4',
    tenantId: 'bc727d7a-3368-4926-86f4-0972d3ac4637',
    authority: 'https://login.microsoftonline.com/bc727d7a-3368-4926-86f4-0972d3ac4637',
    redirectUri: 'https://localhost:4200/auth/callback',
    postLogoutRedirectUri: 'https://localhost:4200/auth/login',
    apiScope: 'api://d2f69daf-210b-4f64-b402-7914c4c9d0b4/access_as_user',
  },
  // Dev-only authentication bypass. When `enabled` is true:
  //   - the login screen shows a user picker instead of the Microsoft button
  //   - HTTP requests carry `X-Dev-User: <email>` instead of a Bearer token
  // Backend must run with `Development:DevHeaderAuth:Enabled=true` to honor
  // the header (set via dotnet user-secrets or the env var
  // `Development__DevHeaderAuth__Enabled=true`). Locked off in production
  // (see environment.prod.ts).
  devAuth: {
    enabled: false,
    users: [
      { email: 'mikko.nieminen@kojamo.fi', displayName: 'Mikko Nieminen (admin)' },
    ] as ReadonlyArray<{ email: string; displayName: string }>,
  },
};
