import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router, NavigationEnd } from '@angular/router';
import { AuthService } from '../../auth/services/auth.service';
import { filter } from 'rxjs';

@Component({
  selector: 'app-main-layout',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './main-layout.component.html',
  styleUrl: './main-layout.component.scss'
})
export class MainLayoutComponent {
  private readonly router = inject(Router);
  private readonly authService = inject(AuthService);

  isSidebarOpen = signal(false);
  isSidebarCollapsed = signal(false);

  constructor() {
    // Close sidebar on route change (for mobile)
    this.router.events.pipe(
      filter(event => event instanceof NavigationEnd)
    ).subscribe(() => {
      this.isSidebarOpen.set(false);
    });
  }

  menuSections = [
    {
      title: 'Core',
      items: [
        { path: '/dashboard', label: 'Dashboard', iconName: 'layout' },
        { path: '/profile', label: 'My Profile', iconName: 'user' },
        { path: '/offers', label: 'Jobs', iconName: 'briefcase' },
      ]
    },
    {
      title: 'Tools',
      items: [
        { path: '/cv', label: 'CV Builder', iconName: 'file-text' },
        { path: '/letters', label: 'Email & Letter', iconName: 'mail' },
        { path: '/applications', label: 'Applications', iconName: 'kanban' },
      ]
    },
    {
      title: 'AI Insights',
      items: [
        { path: '/company-intel', label: 'Company Intel', iconName: 'search-analytics' },
        { path: '/skill-gap', label: 'Skill Gap', iconName: 'target' },
        { path: '/chatbot', label: 'AI Chatbot', iconName: 'cpu' },
      ]
    },
    {
      title: 'System',
      items: [
        { path: '/notifications', label: 'Notifications', iconName: 'bell' },
        { path: '/settings', label: 'Settings', iconName: 'settings' },
      ]
    }
  ];

  isActive(path: string): boolean {
    return this.router.url === path;
  }

  toggleSidebar() {
    this.isSidebarOpen.update(v => !v);
  }

  toggleDesktopSidebar() {
    this.isSidebarCollapsed.update(v => !v);
  }

  logout() {
    this.authService.logout();
  }
}
