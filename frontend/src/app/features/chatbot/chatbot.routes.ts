import { Routes } from '@angular/router';
import { ChatbotComponent } from './chatbot.component';

export const CHATBOT_ROUTES: Routes = [
  {
    path: '',
    component: ChatbotComponent,
    children: [
      {
        path: '',
        loadComponent: () =>
          import('./pages/mode-selector/mode-selector.component').then(
            m => m.ModeSelectorComponent
          ),
      },
      {
        path: 'arena',
        loadComponent: () =>
          import('./pages/arena-session/arena-session.component').then(
            m => m.ArenaSessionComponent
          ),
      },
      {
        path: 'salary-coach',
        loadComponent: () =>
          import('./pages/salary-coach/salary-coach.component').then(
            m => m.SalaryCoachComponent
          ),
      },
      {
        path: 'interview',
        loadComponent: () =>
          import('./pages/interview-session/interview-session.component').then(
            m => m.InterviewSessionComponent
          ),
      },
    ]
  }
];