import { CommonModule } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '@core/auth/auth.service';
import { ProfileService } from '@features/profile/data-access/profile.service';

@Component({
  selector: 'app-sidebar-widget',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="mx-auto w-full max-w-60 rounded-2xl border border-gray-200 bg-gray-50 px-4 py-4 shadow-sm">
      <!-- User Info & Logout Header -->
      <div class="mb-3.5 flex items-center justify-between gap-2">
        <div class="flex items-center gap-2.5 min-w-0">
          <div class="flex h-10 w-10 shrink-0 items-center justify-center overflow-hidden rounded-full bg-brand-500 text-sm font-semibold text-white">
            @if (profile().personal.photoUrl) {
              <img
                [src]="profile().personal.photoUrl"
                [alt]="fullName()"
                class="h-full w-full object-cover"
              />
            } @else {
              {{ initial() }}
            }
          </div>

          <div class="min-w-0">
            <p class="truncate text-sm font-semibold text-gray-900">{{ fullName() }}</p>
            <p class="truncate text-xs text-gray-500">
              {{ profile().personal.jobTitle || 'NextStep member' }}
            </p>
          </div>
        </div>

        <button
          type="button"
          (click)="logout()"
          title="Sign out"
          class="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg text-gray-400 transition hover:bg-red-50 hover:text-red-600 focus:outline-none"
          aria-label="Sign out"
        >
          <svg class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M15.75 9V5.25A2.25 2.25 0 0 0 13.5 3h-6a2.25 2.25 0 0 0-2.25 2.25v13.5A2.25 2.25 0 0 0 7.5 21h6a2.25 2.25 0 0 0 2.25-2.25V15m3 0 3-3m0 0-3-3m3 3H9" />
          </svg>
        </button>
      </div>

      <!-- State 1: < 85% (Profile in progress) -->
      @if (!isJobReady()) {
        <div class="mb-3">
          <div class="mb-1 flex items-center justify-between text-xs font-medium text-gray-600">
            <span>Profile completion</span>
            <span class="font-semibold text-brand-600">{{ completion() }}%</span>
          </div>
          <div class="h-2 overflow-hidden rounded-full bg-gray-200">
            <div
              class="h-full rounded-full bg-brand-500 transition-all duration-300"
              [style.width.%]="completion()"
            ></div>
          </div>
          <p class="mt-2 text-[11px] leading-relaxed text-gray-500">
            Reach 85% to unlock AI CV generation & Job Matching
          </p>
        </div>

        <a
          routerLink="/profile"
          class="flex w-full items-center justify-center gap-1.5 rounded-lg bg-brand-500 px-3 py-2 text-xs font-semibold text-white shadow-sm transition hover:bg-brand-600 active:scale-[0.99]"
        >
          <span>Complete Profile</span>
          <svg class="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M13.5 4.5 21 12m0 0-7.5 7.5M21 12H3" />
          </svg>
        </a>
      }

      <!-- State 2: >= 85% (Unlocked / Ready to Apply!) -->
      @else {
        <div class="mb-3 space-y-2">
          <div class="flex items-center justify-between">
            <span class="inline-flex items-center gap-1.5 rounded-full bg-emerald-50 px-2.5 py-0.5 text-xs font-semibold text-emerald-700 ring-1 ring-inset ring-emerald-600/20">
              <svg class="h-3.5 w-3.5 text-emerald-600" viewBox="0 0 20 20" fill="currentColor">
                <path fill-rule="evenodd" d="M10 18a8 8 0 1 0 0-16 8 8 0 0 0 0 16Zm3.857-9.809a.75.75 0 0 0-1.214-.882l-3.483 4.79-1.88-1.88a.75.75 0 1 0-1.06 1.061l2.5 2.5a.75.75 0 0 0 1.137-.089l4-5.5Z" clip-rule="evenodd" />
              </svg>
              Job Ready
            </span>
            <span class="text-xs font-bold text-emerald-600">{{ completion() }}%</span>
          </div>

          <div class="h-1.5 overflow-hidden rounded-full bg-gray-200">
            <div
              class="h-full rounded-full bg-emerald-500 transition-all duration-300"
              [style.width.%]="completion()"
            ></div>
          </div>

          <p class="text-[11px] leading-relaxed text-gray-500">
            Your profile is ATS-optimized & ready for matching.
          </p>
        </div>

        <a
          routerLink="/offers"
          class="flex w-full items-center justify-center gap-1.5 rounded-lg bg-brand-500 px-3 py-2 text-xs font-semibold text-white shadow-sm transition hover:bg-brand-600 active:scale-[0.99]"
        >
          <span>Find Matching Jobs</span>
          <svg class="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M13.5 4.5 21 12m0 0-7.5 7.5M21 12H3" />
          </svg>
        </a>
      }
    </div>
  `
})
export class SidebarWidgetComponent {
  private readonly authService = inject(AuthService);
  private readonly profileService = inject(ProfileService);

  readonly profile = this.profileService.profile;
  readonly completion = this.profileService.completionPercentage;
  readonly isJobReady = computed(() => this.completion() >= 85);

  readonly fullName = computed(() => {
    const personal = this.profile().personal;
    const fullName = `${personal.firstName} ${personal.lastName}`.trim();
    return fullName || 'NextStep User';
  });

  readonly initial = computed(() => this.fullName().charAt(0).toUpperCase() || 'N');

  logout(): void {
    this.authService.logout();
  }
}
