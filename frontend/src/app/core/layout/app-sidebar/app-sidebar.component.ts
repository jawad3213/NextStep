import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, DestroyRef, computed, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterModule } from '@angular/router';
import { filter } from 'rxjs';
import { AuthService } from '@core/auth/auth.service';
import { ProfileService } from '@features/profile/data-access/profile.service';
import { PipelineStateService } from '@features/offers/data-access/pipeline-state.service';
import { SafeHtmlPipe } from '@shared/pipes/safe-html.pipe';
import { SidebarService } from '../sidebar.service';
import { LanguageService } from '@core/i18n/language.service';
import { TranslatePipe } from '@core/i18n/translate.pipe';

export type NavSubItem = {
  name: string;
  path: string;
};

export type NavItem = {
  name: string;
  icon: string;
  path?: string;
  subItems?: NavSubItem[];
};

export type NavGroup = {
  id: string;
  title?: string;
  items: NavItem[];
};

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [CommonModule, RouterModule, SafeHtmlPipe, TranslatePipe],
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
  readonly langService = inject(LanguageService);

  readonly profile = this.profileService.profile;
  readonly isExpanded$ = this.sidebarService.isExpanded$;
  readonly isMobileOpen$ = this.sidebarService.isMobileOpen$;
  readonly isHovered$ = this.sidebarService.isHovered$;

  openSubmenu: string | null = null;
  subMenuHeights: Record<string, number> = {};

  readonly navGroups: NavGroup[] = [
    // 1. CORE / OVERVIEW
    {
      id: 'core',
      title: 'nav.core',
      items: [
        {
          name: 'nav.dashboard',
          path: '/dashboard',
          icon: `<svg width="1em" height="1em" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"></path><polyline points="9 22 9 12 15 12 15 22"></polyline></svg>`
        },
        {
          name: 'nav.profile',
          path: '/profile',
          icon: `<svg width="1em" height="1em" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"></path><circle cx="12" cy="7" r="4"></circle></svg>`
        }
      ]
    },

    // 2. JOB DISCOVERY & ANALYSIS
    {
      id: 'discovery',
      title: 'nav.discovery',
      items: [
        {
          name: 'nav.jobScraper',
          path: '/offers-recent',
          icon: `<svg width="1em" height="1em" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="11" cy="11" r="8"></circle><line x1="21" y1="21" x2="16.65" y2="16.65"></line></svg>`
        },
        {
          name: 'nav.jobPipeline',
          path: '/offers',
          icon: `<svg width="1em" height="1em" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polygon points="22 3 2 3 10 12.46 10 19 14 21 14 12.46 22 3"></polygon></svg>`
        },
        {
          name: 'nav.companyAnalyzer',
          path: '/company-intel',
          icon: `<svg width="1em" height="1em" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="4" y="2" width="16" height="20" rx="2" ry="2"></rect><line x1="9" y1="6" x2="9.01" y2="6"></line><line x1="15" y1="6" x2="15.01" y2="6"></line><line x1="9" y1="10" x2="9.01" y2="10"></line><line x1="15" y1="10" x2="15.01" y2="10"></line><line x1="9" y1="14" x2="9.01" y2="14"></line><line x1="15" y1="14" x2="15.01" y2="14"></line><line x1="9" y1="18" x2="15" y2="18"></line></svg>`
        }
      ]
    },

    // 3. APPLICATION & TRACKING
    {
      id: 'tracking',
      title: 'nav.tracking',
      items: [
        {
          name: 'nav.applicationTracking',
          path: '/applications',
          icon: `<svg width="1em" height="1em" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="3" width="5" height="18" rx="1"></rect><rect x="10" y="3" width="5" height="12" rx="1"></rect><rect x="17" y="3" width="5" height="15" rx="1"></rect></svg>`
        },
        {
          name: 'nav.emailTracking',
          path: '/letters',
          icon: `<svg width="1em" height="1em" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z"></path><polyline points="22,6 12,13 2,6"></polyline></svg>`
        }
      ]
    },

    // 4. AI PREPARATION & DOCUMENTS
    {
      id: 'preparation',
      title: 'nav.preparation',
      items: [
        {
          name: 'nav.aiChatbot',
          path: '/chatbot',
          icon: `<svg width="1em" height="1em" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"></path></svg>`
        },
        {
          name: 'nav.cvHistory',
          path: '/cv',
          icon: `<svg width="1em" height="1em" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path><polyline points="14 2 14 8 20 8"></polyline><circle cx="12" cy="14" r="3"></circle><polyline points="12 12 12 14 13.5 14"></polyline></svg>`
        }
      ]
    }
  ];

  // 5. SYSTEM (Pinned to the bottom)
  readonly settingsItem: NavItem = {
    name: 'nav.settings',
    path: '/settings',
    icon: `<svg width="1em" height="1em" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="3"></circle><path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-2 2 2 2 0 0 1-2-2v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83 0 2 2 0 0 1 0-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1-2-2 2 2 0 0 1 2-2h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 0-2.83 2 2 0 0 1 2.83 0l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 2-2 2 2 0 0 1 2 2v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 0 2 2 0 0 1 0 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 2 2 2 2 0 0 1-2 2h-.09a1.65 1.65 0 0 0-1.51 1z"></path></svg>`
  };

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

  isActive(path: string | undefined): boolean {
    if (!path) {
      return false;
    }
    if (path === '/dashboard') {
      return this.router.url === '/dashboard';
    }
    if (path === '/settings') {
      return this.router.url.startsWith('/settings') || this.router.url.startsWith('/email/settings');
    }
    if (path === '/offers') {
      return (
        (this.router.url === '/offers' || this.router.url.startsWith('/offers/')) &&
        !this.router.url.startsWith('/offers-recent') &&
        !this.router.url.startsWith('/offers/company-analysis')
      );
    }
    if (path === '/offers-recent') {
      return this.router.url.startsWith('/offers-recent');
    }
    if (path === '/company-intel') {
      return this.router.url.startsWith('/company-intel') || this.router.url.startsWith('/offers/company-analysis');
    }
    return this.router.url === path || this.router.url.startsWith(path + '/');
  }

  isSubmenuActive(nav: NavItem): boolean {
    return !!nav.subItems?.some(subItem => this.isActive(subItem.path));
  }

  getBadge(path: string | undefined) {
    if (!path) {
      return null;
    }
    const route = path.replace('/', '');
    const idMap: Record<string, string> = {
      cv: 'cv-builder',
      letters: 'email',
      'company-intel': 'company-intel',
      'skill-gap': 'skill-gap',
      notifications: 'notifications',
      applications: 'applications',
      offers: 'offers',
      'offers-recent': 'offers'
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
    for (const group of this.navGroups) {
      for (let index = 0; index < group.items.length; index += 1) {
        const nav = group.items[index];
        if (nav.subItems?.some((subItem) => currentUrl === subItem.path || currentUrl.startsWith(subItem.path + '/'))) {
          const key = `${group.id}-${index}`;
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
