import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { onboardingGuard, alreadyOnboardedGuard } from './core/guards/onboarding.guard';
import { MainLayoutComponent } from './core/layout/main-layout/main-layout.component';

// Features (Shells)
import { DashboardComponent } from './features/dashboard/dashboard.component';
import { OnboardingComponent } from './features/onboarding/onboarding.component';
import { ApplicationsComponent } from './features/applications/applications.component';
import { OffersComponent } from './features/offers/offers.component';
import { EmailWorkspaceComponent } from './features/candidatures/email-workspace/email-workspace.component';
import { GmailSettingsComponent } from './features/settings/gmail-settings/gmail-settings.component';

export const routes: Routes = [
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
  {
    path: 'onboarding',
    component: OnboardingComponent,
    canActivate: [authGuard, alreadyOnboardedGuard],
  },
  {
    path: '',
    component: MainLayoutComponent,
    canActivate: [authGuard],
    children: [
      {
        path: 'profile',
        loadComponent: () => import('./features/profile/profile.component').then(m => m.UserProfileComponent)
      },
      { 
        path: 'dashboard', 
        component: DashboardComponent, 
        canActivate: [onboardingGuard] 
      },
      { 
        path: 'offers', 
        component: OffersComponent 
      },
      { 
        path: 'cv', 
        loadComponent: () => import('./features/cv-builder/cv-builder.component').then(m => m.CvBuilderComponent) 
      },
      { 
        path: 'letters', 
        loadComponent: () => import('./features/letters/letters.component').then(m => m.LettersComponent) 
      },
      {
        path: 'candidatures',
        component: ApplicationsComponent
      },
      {
        path: 'candidatures/:candidatureId/email',
        component: EmailWorkspaceComponent
      },
      { 
        path: 'company-intel', 
        loadComponent: () => import('./features/company-intel/company-intel.component').then(m => m.CompanyIntelComponent) 
      },
      { 
        path: 'skill-gap', 
        loadComponent: () => import('./features/skill-gap/skill-gap.component').then(m => m.SkillGapComponent) 
      },
      { 
        path: 'chatbot', 
        loadComponent: () => import('./features/chatbot/chatbot.component').then(m => m.ChatbotComponent) 
      },
      { 
        path: 'notifications', 
        loadComponent: () => import('./features/notifications/notifications.component').then(m => m.NotificationsComponent) 
      },
      { 
        path: 'settings', 
        loadComponent: () => import('./features/settings/settings.component').then(m => m.SettingsComponent) 
      },
      {
        path: 'email/settings',
        component: GmailSettingsComponent
      },
      {
        path: 'email-test',
        loadComponent: () => import('./features/email-test/email-test.component').then(m => m.EmailTestComponent)
      },
    ]
  },
  { path: '**', redirectTo: 'dashboard' }
];
