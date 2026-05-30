import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { onboardingGuard, alreadyOnboardedGuard }from './core/guards/onboarding.guard';
import { MainLayoutComponent } from './core/layout/main-layout/main-layout.component';

// Features (Shells)
import { DashboardComponent } from './features/dashboard/dashboard.component';
import { OnboardingComponent } from './features/onboarding/onboarding.component';

export const routes: Routes = [
  { path: '', redirectTo: 'offers', pathMatch: 'full' },
  {
    path: 'signup',
    loadComponent: () => import('./features/signup/signup.component').then(m => m.SignupComponent),
  },
  {
    path: '',
    component: MainLayoutComponent,
    canActivate: [authGuard],
    children: [
      {
        path: 'onboarding',
        component: OnboardingComponent,
        canActivate: [alreadyOnboardedGuard],
      },
      {
        path: 'profile',
        canActivate: [onboardingGuard],
        loadComponent: () => import('./features/profile/profile.component').then(m => m.UserProfileComponent)
      },
      { path: 'dashboard', component: DashboardComponent, canActivate: [onboardingGuard] },
      {
        path: 'offers/analyze',
        canActivate: [onboardingGuard],
        loadComponent: () => import('./features/offers/offer-pipeline.component').then(m => m.OfferPipelineComponent)
      },
      {
        path: 'offers/company-analysis',
        canActivate: [onboardingGuard],
        loadComponent: () => import('./features/company-intel/company-intel.component').then(m => m.CompanyIntelComponent)
      },
      {
        path: 'offers/:id',
        canActivate: [onboardingGuard],
        loadComponent: () => import('./features/offers/offer-detail.component').then(m => m.OfferDetailComponent)
      },
      { 
        path: 'offers', 
        pathMatch: 'full',
        canActivate: [onboardingGuard],
        loadComponent: () => import('./features/offers/offers.component').then(m => m.OffersComponent) 
      },
      {
        path: 'offers/analyze',
        canActivate: [onboardingGuard],
        loadComponent: () => import('./features/offers/offer-pipeline.component').then(m => m.OfferPipelineComponent)
      },
      {
        path: 'offers/company-analysis',
        canActivate: [onboardingGuard],
        loadComponent: () => import('./features/company-intel/company-intel.component').then(m => m.CompanyIntelComponent)
      },
      {
        path: 'offers/:id',
        canActivate: [onboardingGuard],
        loadComponent: () => import('./features/offers/offer-detail.component').then(m => m.OfferDetailComponent)
      },
      { 
        path: 'offers-recent', 
        canActivate: [onboardingGuard],
        loadComponent: () => import('./features/offers/offers-recent.component').then(m => m.OffersRecentComponent) 
      },
      {
        path: 'offers-recent/:id',
        canActivate: [onboardingGuard],
        loadComponent: () => import('./features/offers/sourced-offer-detail.component').then(m => m.SourcedOfferDetailComponent)
      },
      { 
        path: 'cv', 
        canActivate: [onboardingGuard],
        loadComponent: () => import('./features/cv-builder/cv-builder.component').then(m => m.CvBuilderComponent) 
      },
      { 
        path: 'letters', 
        canActivate: [onboardingGuard],
        loadComponent: () => import('./features/letters/letters.component').then(m => m.LettersComponent) 
      },
      { 
        path: 'applications', 
        canActivate: [onboardingGuard],
        loadComponent: () => import('./features/applications/applications.component').then(m => m.ApplicationsComponent) 
      },
      {
        path: 'applications/:candidatureId/email',
        canActivate: [onboardingGuard],
        loadComponent: () => import('./features/candidatures/email-workspace/email-workspace.component').then(m => m.EmailWorkspaceComponent)
      },
      {
        path: 'applications/:candidatureId/emails',
        pathMatch: 'full',
        redirectTo: '/applications/:candidatureId/email'
      },
      { 
        path: 'company-intel', 
        canActivate: [onboardingGuard],
        loadComponent: () => import('./features/offers/offers-company.component').then(m => m.OffersCompanyComponent) 
      },
      { 
        path: 'skill-gap', 
        canActivate: [onboardingGuard],
        loadComponent: () => import('./features/skill-gap/skill-gap.component').then(m => m.SkillGapComponent) 
      },
      { 
        path: 'chatbot', 
        canActivate: [onboardingGuard],
        loadComponent: () => import('./features/chatbot/chatbot.component').then(m => m.ChatbotComponent) 
      },
      { 
        path: 'notifications', 
        canActivate: [onboardingGuard],
        loadComponent: () => import('./features/notifications/notifications.component').then(m => m.NotificationsComponent) 
      },
      { 
        path: 'settings', 
        canActivate: [onboardingGuard],
        loadComponent: () => import('./features/settings/settings.component').then(m => m.SettingsComponent) 
      },
    ]
  },
];
