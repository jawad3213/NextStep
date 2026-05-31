import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { onboardingGuard, alreadyOnboardedGuard }from './core/guards/onboarding.guard';
import { MainLayoutComponent } from './core/layout/main-layout/main-layout.component';

// Features (Shells)
import { DashboardComponent } from './features/dashboard/dashboard.component';
import { OnboardingComponent } from './features/onboarding/onboarding.component';
import { CvBuilderComponent } from './features/cv-builder/cv-builder.component';

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
        component: CvBuilderComponent
      },
      { 
        path: 'letters', 
        pathMatch: 'full',
        canActivate: [onboardingGuard],
        loadComponent: () => import('./features/letters/letters.component').then(m => m.LettersComponent) 
      },
      {
        path: 'letters/:candidatureId',
        canActivate: [onboardingGuard],
        loadComponent: () => import('./features/candidatures/email-workspace/email-workspace.component').then(m => m.EmailWorkspaceComponent)
      },
      { 
        path: 'applications', 
        canActivate: [onboardingGuard],
        loadComponent: () => import('./features/applications/applications.component').then(m => m.ApplicationsComponent) 
      },
      {
        path: 'applications/:candidatureId/email',
        pathMatch: 'full',
        redirectTo: '/letters/:candidatureId'
      },
      {
        path: 'applications/:candidatureId/emails',
        pathMatch: 'full',
        redirectTo: '/letters/:candidatureId'
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
        loadChildren: () => import('./features/chatbot/chatbot.routes').then(m => m.CHATBOT_ROUTES) 
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
      { 
        path: 'email/settings', 
        canActivate: [onboardingGuard],
        loadComponent: () => import('./features/settings/gmail-settings/gmail-settings.component').then(m => m.GmailSettingsComponent) 
      },
    ]
  },
];
