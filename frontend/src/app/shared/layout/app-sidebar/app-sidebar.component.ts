import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, DestroyRef, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterModule } from '@angular/router';
import { filter } from 'rxjs';
import { AuthService } from '../../../core/auth/services/auth.service';
import { ProfileService } from '../../../features/profile/profile.service';
import { PipelineStateService } from '../../../services/pipeline-state.service';
import { SafeHtmlPipe } from '../../pipe/safe-html.pipe';
import { SidebarService } from '../../services/sidebar.service';
import { SidebarWidgetComponent } from './app-sidebar-widget.component';

type NavSubItem = {
  name: string;
  path: string;
};

type NavItem = {
  name: string;
  icon: string;
  path?: string;
  subItems?: NavSubItem[];
};

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [CommonModule, RouterModule, SafeHtmlPipe, SidebarWidgetComponent],
  templateUrl: './app-sidebar.component.html'
})
export class AppSidebarComponent {
  private readonly router = inject(Router);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);

  readonly authService = inject(AuthService);
  readonly sidebarService = inject(SidebarService);
  readonly profileService = inject(ProfileService);
  readonly pipelineState = inject(PipelineStateService);

  readonly profile = this.profileService.profile;
  readonly isExpanded$ = this.sidebarService.isExpanded$;
  readonly isMobileOpen$ = this.sidebarService.isMobileOpen$;
  readonly isHovered$ = this.sidebarService.isHovered$;

  openSubmenu: string | null = null;
  subMenuHeights: Record<string, number> = {};

  readonly navItems: NavItem[] = [
    {
      name: 'Dashboard',
      path: '/dashboard',
      icon: `<svg width="1em" height="1em" viewBox="0 0 24 24" fill="none"><path d="M4.75 9.75L12 4L19.25 9.75V18C19.25 19.2426 18.2426 20.25 17 20.25H7C5.75736 20.25 4.75 19.2426 4.75 18V9.75Z" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/><path d="M9.25 20.25V13.75H14.75V20.25" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/></svg>`
    },
    {
      name: 'Profile',
      path: '/profile',
      icon: `<svg width="1em" height="1em" viewBox="0 0 24 24" fill="none"><path d="M12 12.25C14.3472 12.25 16.25 10.3472 16.25 8C16.25 5.65279 14.3472 3.75 12 3.75C9.65279 3.75 7.75 5.65279 7.75 8C7.75 10.3472 9.65279 12.25 12 12.25Z" stroke="currentColor" stroke-width="1.8"/><path d="M5 19.25C5.91875 16.4393 8.67639 14.5 12 14.5C15.3236 14.5 18.0813 16.4393 19 19.25" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"/><path d="M12 22C17.5228 22 22 17.5228 22 12C22 6.47715 17.5228 2 12 2C6.47715 2 2 6.47715 2 12C2 17.5228 6.47715 22 12 22Z" stroke="currentColor" stroke-width="1.4"/></svg>`
    },
    {
      name: 'Jobs',
      icon: `<svg width="1em" height="1em" viewBox="0 0 24 24" fill="none"><path d="M8 6.25H16C17.5188 6.25 18.75 7.48122 18.75 9V17C18.75 18.5188 17.5188 19.75 16 19.75H8C6.48122 19.75 5.25 18.5188 5.25 17V9C5.25 7.48122 6.48122 6.25 8 6.25Z" stroke="currentColor" stroke-width="1.8"/><path d="M9 6.25V5.5C9 4.25736 10.0074 3.25 11.25 3.25H12.75C13.9926 3.25 15 4.25736 15 5.5V6.25" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"/><path d="M5.25 11.25H18.75" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"/></svg>`,
      subItems: [
        { name: 'Offers', path: '/offers' },
        { name: 'Offres récentes', path: '/offers-recent' },
        { name: 'Applications', path: '/applications' }
      ]
    },
    {
      name: 'Career Tools',
      icon: `<svg width="1em" height="1em" viewBox="0 0 24 24" fill="none"><path d="M7.25 4.75H16.75C18.1307 4.75 19.25 5.86929 19.25 7.25V16.75C19.25 18.1307 18.1307 19.25 16.75 19.25H7.25C5.86929 19.25 4.75 18.1307 4.75 16.75V7.25C4.75 5.86929 5.86929 4.75 7.25 4.75Z" stroke="currentColor" stroke-width="1.8"/><path d="M8.5 9H15.5" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"/><path d="M8.5 12H15.5" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"/><path d="M8.5 15H12.5" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"/></svg>`,
      subItems: [
        { name: 'CV Builder', path: '/cv' },
        { name: 'Email & Letter', path: '/letters' }
      ]
    }
  ];

  readonly othersItems: NavItem[] = [
    {
      name: 'AI Insights',
      icon: `<svg width="1em" height="1em" viewBox="0 0 24 24" fill="none"><path d="M12 3L14.4 8.1L20 8.9L16 12.8L17 18.5L12 15.7L7 18.5L8 12.8L4 8.9L9.6 8.1L12 3Z" stroke="currentColor" stroke-width="1.7" stroke-linejoin="round"/></svg>`,
      subItems: [
        { name: 'Company Intel', path: '/company-intel' },
        { name: 'Skill Gap', path: '/skill-gap' },
        { name: 'AI Chatbot', path: '/chatbot' }
      ]
    },
    {
      name: 'System',
      icon: `<svg width="1em" height="1em" viewBox="0 0 24 24" fill="none"><path d="M12 15.25C13.7949 15.25 15.25 13.7949 15.25 12C15.25 10.2051 13.7949 8.75 12 8.75C10.2051 8.75 8.75 10.2051 8.75 12C8.75 13.7949 10.2051 15.25 12 15.25Z" stroke="currentColor" stroke-width="1.8"/><path d="M19.4 13.5L20.75 12L19.4 10.5L19.56 8.49L17.6 7.89L16.5 6.2L14.5 6.6L12.75 5.5L11 6.6L9 6.2L7.9 7.89L5.94 8.49L6.1 10.5L4.75 12L6.1 13.5L5.94 15.51L7.9 16.11L9 17.8L11 17.4L12.75 18.5L14.5 17.4L16.5 17.8L17.6 16.11L19.56 15.51L19.4 13.5Z" stroke="currentColor" stroke-width="1.5" stroke-linejoin="round"/></svg>`,
      subItems: [
        { name: 'Notifications', path: '/notifications' },
        { name: 'Settings', path: '/settings' }
      ]
    }
  ];

  constructor() {
    this.router.events
      .pipe(
        filter((event): event is NavigationEnd => event instanceof NavigationEnd),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(() => {
        this.setActiveMenuFromRoute(this.router.url);
        this.sidebarService.setMobileOpen(false);
      });

    this.setActiveMenuFromRoute(this.router.url);
  }

  isActive(path: string): boolean {
    return this.router.url === path;
  }

  getBadge(path: string) {
    const route = path.replace('/', '');
    const idMap: Record<string, string> = {
      cv: 'cv-builder',
      letters: 'email',
      'company-intel': 'company-intel',
      'skill-gap': 'skill-gap',
      notifications: 'notifications'
    };
    const badgeId = idMap[route];
    if (!badgeId) {
      return null;
    }

    return this.pipelineState.sidebarBadges().find((badge) => badge.page === badgeId && badge.visible) ?? null;
  }

  toggleSubmenu(section: string, index: number): void {
    const key = `${section}-${index}`;

    if (this.openSubmenu === key) {
      this.openSubmenu = null;
      this.subMenuHeights[key] = 0;
      return;
    }

    this.openSubmenu = key;
    this.measureSubmenuHeight(key);
  }

  closeMobileSidebar(): void {
    this.sidebarService.setMobileOpen(false);
  }

  onSidebarMouseEnter(): void {
    if (!this.sidebarService.expandedValue) {
      this.sidebarService.setHovered(true);
    }
  }

  private setActiveMenuFromRoute(currentUrl: string): void {
    const menuGroups = [
      { items: this.navItems, prefix: 'main' },
      { items: this.othersItems, prefix: 'others' }
    ];

    for (const group of menuGroups) {
      for (let index = 0; index < group.items.length; index += 1) {
        const nav = group.items[index];
        if (nav.subItems?.some((subItem) => currentUrl === subItem.path)) {
          const key = `${group.prefix}-${index}`;
          this.openSubmenu = key;
          this.measureSubmenuHeight(key);
          return;
        }
      }
    }
  }

  logout(): void {
    this.authService.logout();
  }

  private measureSubmenuHeight(key: string): void {
    setTimeout(() => {
      const element = document.getElementById(key);
      if (!element) {
        return;
      }

      this.subMenuHeights[key] = element.scrollHeight;
      this.cdr.detectChanges();
    });
  }
}
