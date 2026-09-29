import { CommonModule } from '@angular/common';
import { Component, HostListener, computed, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterModule } from '@angular/router';
import { catchError, filter, map, of, startWith, switchMap } from 'rxjs';
import { AuthService } from '@core/auth/auth.service';
import { ProfileService } from '@features/profile/data-access/profile.service';
import { PipelineStateService } from '@features/offers/data-access/pipeline-state.service';
import { OnboardingService } from '@core/auth/onboarding.service';
import { AppSidebarComponent } from '../app-sidebar/app-sidebar.component';
import { SidebarService } from '../sidebar.service';
import { OfferStepId } from '@features/offers/data-access/offers.models';
import { OffersStepperComponent } from '@features/offers/components/offers-stepper/offers-stepper.component';
import { SnDrawerComponent } from '@features/sn-copilot/components/sn-drawer/sn-drawer.component';
import { SnCopilotService } from '@features/sn-copilot/data-access/sn-copilot.service';

type HeaderState = {
  eyebrow: string;
  title: string;
};

@Component({
  selector: 'app-main-layout',
  standalone: true,
  imports: [CommonModule, RouterModule, AppSidebarComponent, OffersStepperComponent, SnDrawerComponent],
  templateUrl: './main-layout.component.html',
  styleUrl: './main-layout.component.scss'
})
export class MainLayoutComponent {
  readonly sidebarService = inject(SidebarService);
  readonly snService = inject(SnCopilotService);
  readonly router = inject(Router);
  readonly authService = inject(AuthService);
  readonly profileService = inject(ProfileService);
  readonly onboardingService = inject(OnboardingService);
  readonly pipelineState = inject(PipelineStateService);

  @HostListener('window:keydown', ['$event'])
  onGlobalKeyDown(event: KeyboardEvent): void {
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
      event.preventDefault();
      this.snService.toggleDrawer();
    }
  }
  readonly isExpanded$ = this.sidebarService.isExpanded$;
  readonly isMobileOpen$ = this.sidebarService.isMobileOpen$;
  readonly isHovered$ = this.sidebarService.isHovered$;
  readonly isEditorFocusMode$ = this.sidebarService.isEditorFocusMode$;
  readonly isFullBleedRoute$ = this.router.events.pipe(
    filter((event): event is NavigationEnd => event instanceof NavigationEnd),
    startWith(null),
    map(() => {
      const url = this.router.url;
      return url.startsWith('/profile') || url.startsWith('/offers') || url.startsWith('/onboarding');
    })
  );
  readonly headerState$ = this.router.events.pipe(
    filter((event): event is NavigationEnd => event instanceof NavigationEnd),
    startWith(null),
    map(() => this.buildHeaderState(this.router.url))
  );
  readonly isOfferPipelineRoute$ = this.router.events.pipe(
    filter((event): event is NavigationEnd => event instanceof NavigationEnd),
    startWith(null),
    map(() => this.router.url.startsWith('/offers/analyze'))
  );
  readonly showSidebar$ = this.router.events.pipe(
    filter((event): event is NavigationEnd => event instanceof NavigationEnd),
    startWith(null),
    switchMap(() =>
      this.onboardingService.getStatus().pipe(
        // The sidebar (links to the whole app) appears only once the backend says the
        // onboarding questions AND the profile are both completed for this user.
        map((status) =>
          !this.router.url.startsWith('/onboarding') && status.onboardingCompleted && status.profileCompleted
        ),
        catchError(() => of(false))
      )
    )
  );
  readonly isProfileMenuOpen = signal(false);
  readonly isNotificationsMenuOpen = signal(false);
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
  readonly offerPipelineSteps: { id: OfferStepId; label: string; icon: string }[] = [
    { id: 'submit', label: 'Offre', icon: 'description' },
    { id: 'analysis', label: 'Skill Gap', icon: 'analytics' },
    { id: 'template', label: 'Template', icon: 'palette' },
    { id: 'generation', label: 'Edition CV', icon: 'edit_note' },
    { id: 'results', label: 'Résultats', icon: 'verified' },
  ];
  private readonly stepToPipeline: Record<OfferStepId, 1 | 2 | 3 | 4 | 5> = {
    submit: 1, analysis: 2, template: 3, generation: 4, results: 5,
  };
  private readonly pipelineToStep: Record<1 | 2 | 3 | 4 | 5, OfferStepId> = {
    1: 'submit', 2: 'analysis', 3: 'template', 4: 'generation', 5: 'results',
  };
  readonly currentOfferPipelineStep = computed<OfferStepId>(() =>
    this.pipelineToStep[this.pipelineState.currentStep() as 1 | 2 | 3 | 4 | 5] ?? 'submit'
  );
  readonly offerPipelineStepStates = computed<Record<OfferStepId, 'idle' | 'active' | 'done' | 'error'>>(() => {
    const states: Record<OfferStepId, 'idle' | 'active' | 'done' | 'error'> = {
      submit: 'idle',
      analysis: 'idle',
      template: 'idle',
      generation: 'idle',
      results: 'idle',
    };
    const pipelineSteps = this.pipelineState.steps();
    for (const [offerStep, pipelineStep] of Object.entries(this.stepToPipeline) as [OfferStepId, 1 | 2 | 3 | 4 | 5][]) {
      const step = pipelineSteps[pipelineStep - 1];
      states[offerStep] = step ? step.status : 'idle';
    }
    return states;
  });

  handleSidebarToggle(): void {
    if (window.innerWidth >= 1280) {
      this.sidebarService.toggleExpanded();
      return;
    }

    this.sidebarService.toggleMobileOpen();
  }

  toggleProfileMenu(): void {
    this.isNotificationsMenuOpen.set(false);
    this.isProfileMenuOpen.update(isOpen => !isOpen);
  }

  toggleNotificationsMenu(): void {
    this.isProfileMenuOpen.set(false);
    this.isNotificationsMenuOpen.update(isOpen => !isOpen);
  }

  goToNotifications(): void {
    this.isNotificationsMenuOpen.set(false);
    this.router.navigate(['/notifications']);
  }

  goToEditProfile(): void {
    this.isProfileMenuOpen.set(false);
    this.router.navigate(['/profile'], {
      queryParams: { step: 'coordonnees' }
    });
  }

  isActive(path: string): boolean {
    return this.router.url === path || this.router.url.startsWith(path + '/');
  }

  goToSettings(): void {
    this.isProfileMenuOpen.set(false);
    this.router.navigate(['/settings']);
  }

  exportProfile(): void {
    this.isProfileMenuOpen.set(false);
    this.profileService.downloadProfileJson();
  }

  goToSupport(): void {
    this.isProfileMenuOpen.set(false);
    this.router.navigate(['/chatbot']);
  }

  goToOfferPipelineStep(id: OfferStepId): void {
    if (this.pipelineState.isLoading()) return;
    const pipelineStep = this.stepToPipeline[id];
    const targetIdx = pipelineStep - 1;
    const isDone = this.pipelineState.steps()[targetIdx]?.status === 'done';
    if (pipelineStep <= this.pipelineState.currentStep() || isDone) {
      this.pipelineState.goToStep(pipelineStep);
    }
  }

  logout(): void {
    this.isProfileMenuOpen.set(false);
    this.authService.logout();
  }

  @HostListener('document:click', ['$event'])
  closeMenusOnOutsideClick(event: Event): void {
    const target = event.target as HTMLElement | null;
    if (!target?.closest('[data-user-menu]')) {
      this.isProfileMenuOpen.set(false);
    }
    if (!target?.closest('[data-notifications-menu]')) {
      this.isNotificationsMenuOpen.set(false);
    }
  }

  @HostListener('document:keydown.escape')
  closeMenusOnEscape(): void {
    this.isProfileMenuOpen.set(false);
    this.isNotificationsMenuOpen.set(false);
  }

  private buildHeaderState(url: string): HeaderState {
    const parsedUrl = this.router.parseUrl(url);
    const primaryPath = parsedUrl.root.children['primary']?.segments.map((segment) => segment.path) ?? [];
    const page = primaryPath[0] ?? 'dashboard';

    if (page === 'onboarding') {
      return { eyebrow: 'Welcome / Getting Started', title: 'Tell Us About Yourself' };
    }

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
