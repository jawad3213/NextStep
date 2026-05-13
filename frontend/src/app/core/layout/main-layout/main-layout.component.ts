import { CommonModule } from '@angular/common';
import { Component, HostListener, computed, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterModule } from '@angular/router';
import { filter, map, startWith } from 'rxjs';
import { AuthService } from '../../auth/services/auth.service';
import { ProfileService } from '../../../features/profile/profile.service';
import { PipelineStateService } from '../../../services/pipeline-state.service';
import { AppSidebarComponent } from '../../../shared/layout/app-sidebar/app-sidebar.component';
import { SidebarService } from '../../../shared/services/sidebar.service';
import { ThemeService } from '../../../shared/services/theme.service';

type HeaderState = {
  eyebrow: string;
  title: string;
};

@Component({
  selector: 'app-main-layout',
  standalone: true,
  imports: [CommonModule, RouterModule, AppSidebarComponent],
  templateUrl: './main-layout.component.html',
  styleUrl: './main-layout.component.scss'
})
export class MainLayoutComponent {
  readonly sidebarService = inject(SidebarService);
  readonly router = inject(Router);
  readonly authService = inject(AuthService);
  readonly profileService = inject(ProfileService);
  readonly pipelineState = inject(PipelineStateService);
  readonly themeService = inject(ThemeService);
  readonly isExpanded$ = this.sidebarService.isExpanded$;
  readonly isMobileOpen$ = this.sidebarService.isMobileOpen$;
  readonly isHovered$ = this.sidebarService.isHovered$;
  readonly isProfileRoute$ = this.router.events.pipe(
    filter((event): event is NavigationEnd => event instanceof NavigationEnd),
    startWith(null),
    map(() => this.router.url.startsWith('/profile'))
  );
  readonly headerState$ = this.router.events.pipe(
    filter((event): event is NavigationEnd => event instanceof NavigationEnd),
    startWith(null),
    map(() => this.buildHeaderState(this.router.url))
  );
  readonly isProfileMenuOpen = signal(false);
  readonly theme = this.themeService.theme;
  readonly profile = this.profileService.profile;
  readonly userFullName = computed(() => {
    const personal = this.profile().personal;
    const profileName = `${personal.firstName} ${personal.lastName}`.trim();
    const authUser = this.authService.user();
    const authName = `${authUser?.firstName ?? ''} ${authUser?.lastName ?? ''}`.trim();
    return profileName || authName || 'NextStep User';
  });
  readonly userShortName = computed(() => this.userFullName().split(' ')[0] || 'User');
  readonly userEmail = computed(() => {
    const personalEmail = this.profile().personal.email?.trim();
    const authEmail = this.authService.user()?.email?.trim();
    return personalEmail || authEmail || 'your-account@nextstep.app';
  });
  readonly userPhoto = computed(() => this.profile().personal.photoUrl);
  readonly userInitial = computed(() => this.userFullName().charAt(0).toUpperCase() || 'N');
  readonly hasNotifications = computed(() =>
    this.pipelineState.sidebarBadges().some(
      badge => badge.page === 'notifications' && badge.visible && badge.label !== '0'
    )
  );

  handleSidebarToggle(): void {
    if (window.innerWidth >= 1280) {
      this.sidebarService.toggleExpanded();
      return;
    }

    this.sidebarService.toggleMobileOpen();
  }

  toggleTheme(): void {
    this.themeService.toggleTheme();
  }

  toggleProfileMenu(): void {
    this.isProfileMenuOpen.update(isOpen => !isOpen);
  }

  goToNotifications(): void {
    this.router.navigate(['/notifications']);
  }

  goToEditProfile(): void {
    this.isProfileMenuOpen.set(false);
    this.router.navigate(['/profile'], {
      queryParams: { step: 'coordonnees' }
    });
  }

  goToSettings(): void {
    this.isProfileMenuOpen.set(false);
    this.router.navigate(['/settings']);
  }

  goToSupport(): void {
    this.isProfileMenuOpen.set(false);
    this.router.navigate(['/chatbot']);
  }

  logout(): void {
    this.isProfileMenuOpen.set(false);
    this.authService.logout();
  }

  @HostListener('document:click', ['$event'])
  closeProfileMenuOnOutsideClick(event: Event): void {
    const target = event.target as HTMLElement | null;
    if (!target?.closest('[data-user-menu]')) {
      this.isProfileMenuOpen.set(false);
    }
  }

  @HostListener('document:keydown.escape')
  closeProfileMenuOnEscape(): void {
    this.isProfileMenuOpen.set(false);
  }

  private buildHeaderState(url: string): HeaderState {
    const parsedUrl = this.router.parseUrl(url);
    const primaryPath = parsedUrl.root.children['primary']?.segments.map((segment) => segment.path) ?? [];
    const page = primaryPath[0] ?? 'dashboard';

    if (page === 'profile') {
      const step = parsedUrl.queryParams['step'] ?? 'coordonnees';
      const profileSteps: Record<string, HeaderState> = {
        coordonnees: { eyebrow: 'Profile / Contact Info', title: 'Personal Details' },
        experience: { eyebrow: 'Profile / Experience', title: 'Work Experience' },
        formation: { eyebrow: 'Profile / Education', title: 'Your Education' },
        competences: { eyebrow: 'Profile / Skills', title: 'Skills & Languages' },
        resume: { eyebrow: 'Profile / Summary', title: 'Profile Summary' },
        projets: { eyebrow: 'Profile / Projects', title: 'Completed Projects' },
        certifications: { eyebrow: 'Profile / Certifications', title: 'Certifications' }
      };

      return profileSteps[step] ?? profileSteps['coordonnees'];
    }

    const defaultPages: Record<string, HeaderState> = {
      dashboard: { eyebrow: 'Workspace / Dashboard', title: 'Dashboard' },
      offers: { eyebrow: 'Jobs / Offers', title: 'Offers' },
      applications: { eyebrow: 'Jobs / Applications', title: 'Applications' },
      cv: { eyebrow: 'Career Tools / CV Builder', title: 'CV Builder' },
      letters: { eyebrow: 'Career Tools / Email & Letter', title: 'Email & Letter' },
      'company-intel': { eyebrow: 'AI Insights / Company Intel', title: 'Company Intel' },
      'skill-gap': { eyebrow: 'AI Insights / Skill Gap', title: 'Skill Gap Analysis' },
      chatbot: { eyebrow: 'AI Insights / AI Chatbot', title: 'AI Chatbot' },
      notifications: { eyebrow: 'System / Notifications', title: 'Notifications' },
      settings: { eyebrow: 'System / Settings', title: 'Settings' }
    };

    return defaultPages[page] ?? { eyebrow: 'NextStep', title: 'Workspace' };
  }
}
