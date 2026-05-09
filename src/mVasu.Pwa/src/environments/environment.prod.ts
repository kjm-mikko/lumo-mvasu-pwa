export const environment = {
  production: true,
  apiBaseUrl: '/api',
  msal: {
    clientId: 'd2f69daf-210b-4f64-b402-7914c4c9d0b4',
    tenantId: 'bc727d7a-3368-4926-86f4-0972d3ac4637',
    authority: 'https://login.microsoftonline.com/bc727d7a-3368-4926-86f4-0972d3ac4637',
    redirectUri: 'https://lumo-mvasu.example.com/auth/callback',
    postLogoutRedirectUri: 'https://lumo-mvasu.example.com/auth/login',
    apiScope: 'api://d2f69daf-210b-4f64-b402-7914c4c9d0b4/access_as_user',
  },
  // Dev-only authentication bypass. Locked off in production — the API
  // refuses to register the matching scheme outside the Development env,
  // so flipping this to true would just break login on the PWA side.
  devAuth: {
    enabled: false,
    users: [] as ReadonlyArray<{ email: string; displayName: string }>,
  },
};
