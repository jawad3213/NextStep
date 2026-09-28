import { Routes } from '@angular/router';

/** /applications — application tracking board and application detail. */
export const APPLICATIONS_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./pages/applications/applications.component').then(m => m.ApplicationsComponent),
  },
  {
    path: ':candidatureId',
    pathMatch: 'full',
    loadComponent: () =>
      import('./pages/candidature-detail/candidature-detail.component').then(m => m.CandidatureDetailComponent),
  },
  // Former email URLs of an application now open the letters workspace
  { path: ':candidatureId/email', pathMatch: 'full', redirectTo: '/letters/:candidatureId' },
  { path: ':candidatureId/emails', pathMatch: 'full', redirectTo: '/letters/:candidatureId' },
];

/** /letters — application emails and letters. */
export const LETTERS_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./pages/letters/letters.component').then(m => m.LettersComponent),
  },
  {
    path: ':candidatureId',
    loadComponent: () =>
      import('./pages/email-workspace/email-workspace.component').then(m => m.EmailWorkspaceComponent),
  },
];
