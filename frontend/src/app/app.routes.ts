import { Routes } from '@angular/router';
import { authGuard } from '@core/guards/auth.guard';
import { publicOnlyGuard } from '@core/guards/public-only.guard';
import { onboardingGuard, alreadyOnboardedGuard } from '@core/guards/onboarding.guard';
import { MainLayoutComponent } from '@core/layout/main-layout/main-layout.component';

/**
 * Every page is lazy-loaded. Features with several pages own their routes
 * (offers, applications, chatbot); single-page features are loaded directly.
 */
export const routes: Routes = [
  // ── Public ────────────────────────────────────────────────────────────────
  // Marketing: visitors only. An authenticated user is forwarded to the application.
  {
    path: '',
    pathMatch: 'full',
    canActivate: [publicOnlyGuard],
    loadComponent: () => import('@features/landing/pages/landing/landing.component').then(m => m.LandingComponent),
  },
  {
    path: 'landing',
    canActivate: [publicOnlyGuard],
    loadComponent: () => import('@features/landing/pages/landing/landing.component').then(m => m.LandingComponent),
  },
  {
    path: 'signup',
    canActivate: [publicOnlyGuard],
    loadComponent: () => import('@features/signup/pages/signup/signup.component').then(m => m.SignupComponent),
  },

  // ── Authenticated app shell ───────────────────────────────────────────────
  {
    path: '',
    component: MainLayoutComponent,
    canActivate: [authGuard],
    children: [
      {
        path: 'onboarding',
        canActivate: [alreadyOnboardedGuard],
        loadComponent: () =>
          import('@features/onboarding/pages/onboarding/onboarding.component').then(m => m.OnboardingComponent),
      },
      {
        path: 'profile',
        canActivate: [onboardingGuard],
        loadComponent: () => import('@features/profile/pages/profile/profile.component').then(m => m.UserProfileComponent),
      },
      {
        path: 'dashboard',
        canActivate: [onboardingGuard],
        loadComponent: () =>
          import('@features/dashboard/pages/dashboard/dashboard.component').then(m => m.DashboardComponent),
      },
      // Must stay before "offers" so it is not captured by offers/:id
      {
        path: 'offers/company-analysis',
        canActivate: [onboardingGuard],
        loadComponent: () =>
          import('@features/company-intel/pages/company-analysis/company-intel.component').then(m => m.CompanyIntelComponent),
      },
      {
        path: 'offers',
        canActivate: [onboardingGuard],
        loadChildren: () => import('@features/offers/offers.routes').then(m => m.OFFERS_ROUTES),
      },
      {
        path: 'offers-recent',
        canActivate: [onboardingGuard],
        loadChildren: () => import('@features/offers/offers.routes').then(m => m.SOURCED_OFFERS_ROUTES),
      },
      {
        path: 'cv',
        canActivate: [onboardingGuard],
        loadComponent: () =>
          import('@features/cv-builder/pages/cv-builder/cv-builder.component').then(m => m.CvBuilderComponent),
      },
      {
        path: 'letters',
        canActivate: [onboardingGuard],
        loadChildren: () => import('@features/applications/applications.routes').then(m => m.LETTERS_ROUTES),
      },
      {
        path: 'applications',
        canActivate: [onboardingGuard],
        loadChildren: () => import('@features/applications/applications.routes').then(m => m.APPLICATIONS_ROUTES),
      },
      {
        path: 'company-intel',
        canActivate: [onboardingGuard],
        loadComponent: () =>
          import('@features/company-intel/pages/company-overview/offers-company.component').then(m => m.OffersCompanyComponent),
      },
      {
        path: 'skill-gap',
        canActivate: [onboardingGuard],
        loadComponent: () => import('@features/skill-gap/pages/skill-gap/skill-gap.component').then(m => m.SkillGapComponent),
      },
      {
        path: 'chatbot',
        canActivate: [onboardingGuard],
        loadChildren: () => import('@features/chatbot/chatbot.routes').then(m => m.CHATBOT_ROUTES),
      },
      {
        path: 'notifications',
        canActivate: [onboardingGuard],
        loadComponent: () =>
          import('@features/notifications/pages/notifications/notifications.component').then(m => m.NotificationsComponent),
      },
      {
        path: 'settings',
        canActivate: [onboardingGuard],
        loadComponent: () => import('@features/settings/pages/settings/settings.component').then(m => m.SettingsComponent),
      },
      {
        path: 'email/settings',
        canActivate: [onboardingGuard],
        loadComponent: () =>
          import('@features/settings/pages/gmail-settings/gmail-settings.component').then(m => m.GmailSettingsComponent),
      },
    ],
  },
];
