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
};
