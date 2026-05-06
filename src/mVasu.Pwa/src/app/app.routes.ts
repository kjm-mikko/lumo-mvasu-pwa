import { inject } from '@angular/core';
import { Routes } from '@angular/router';

import { authGuard } from './core/guards/auth.guard';
import { HomePreferenceService } from './core/services/home-preference.service';

/**
 * Resolves the root path to whichever landing screen the user picked
 * via Lisää → Sovellus → Aloitusnäkymä. Defaults to '/tasks' (the C-side
 * task queue); flips to '/home-hub' when the user prefers the B-side
 * module hub.
 */
const homeRedirect = () => inject(HomePreferenceService).homeRoutePath();

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    redirectTo: homeRedirect,
  },
  {
    path: 'auth/login',
    loadComponent: () =>
      import('./features/login/login.component').then(m => m.LoginComponent),
  },
  {
    path: 'auth/callback',
    loadComponent: () =>
      import('./features/auth-callback/auth-callback.component').then(m => m.AuthCallbackComponent),
  },
  {
    path: 'search',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/search/quick-search.component').then(m => m.QuickSearchComponent),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./layout/main-layout/main-layout.component').then(m => m.MainLayoutComponent),
    children: [
      {
        path: 'tasks',
        data: { reuse: true },
        loadComponent: () =>
          import('./features/tasks/tasks-tab.component').then(m => m.TasksTabComponent),
      },
      {
        path: 'tasks/:id',
        loadComponent: () =>
          import('./features/tasks/task-detail.component').then(m => m.TaskDetailComponent),
      },
      {
        path: 'tiskilista',
        data: { reuse: true },
        loadComponent: () =>
          import('./features/tiskilista/tiskilista-list.component').then(m => m.TiskilistaListComponent),
      },
      {
        path: 'tiskilista/:id',
        loadComponent: () =>
          import('./features/tiskilista/tiskilista-detail.component').then(m => m.TiskilistaDetailComponent),
      },
      {
        path: 'customers',
        data: { reuse: true },
        loadComponent: () =>
          import('./features/customers/customers.component').then(m => m.CustomersComponent),
      },
      {
        path: 'customers/:id',
        loadComponent: () =>
          import('./features/customers/customer-detail.component').then(m => m.CustomerDetailComponent),
      },
      {
        path: 'home-hub',
        data: { reuse: true },
        loadComponent: () =>
          import('./features/home-hub/home-hub.component').then(m => m.HomeHubComponent),
      },
      {
        path: 'more',
        loadComponent: () =>
          import('./features/more/more.component').then(m => m.MoreComponent),
      },
      {
        path: 'settings',
        loadComponent: () =>
          import('./features/settings/settings.component').then(m => m.SettingsComponent),
      },
    ],
  },
  {
    path: '**',
    redirectTo: homeRedirect,
  },
];
