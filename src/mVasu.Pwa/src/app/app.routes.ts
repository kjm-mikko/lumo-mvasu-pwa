import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'tasks',
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
    redirectTo: 'tasks',
  },
];
