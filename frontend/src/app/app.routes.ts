import { Routes } from '@angular/router';
import { DashboardComponent } from './features/dashboard/dashboard.component';
import { ProfileComponent } from './features/profile/profile.component';
import { authGuard } from './core/guards/auth.guard';
import { OnboardingComponent } from './features/onboarding/onboarding.component';
import { onboardingGuard, alreadyOnboardedGuard } from './core/guards/onboarding.guard';

export const routes: Routes = [
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
  { 
    path: 'dashboard', 
    component: DashboardComponent,
    canActivate: [authGuard, onboardingGuard],
  },
  {
    path: 'profile',
    component: ProfileComponent,
    canActivate: [authGuard, onboardingGuard],
  },
  {
    path: 'onboarding',
    component: OnboardingComponent,
    canActivate: [authGuard, alreadyOnboardedGuard],
  },
];
