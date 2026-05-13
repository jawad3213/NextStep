import { CommonModule } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { AuthService } from '../../../core/auth/services/auth.service';
import { ProfileService } from '../../../features/profile/profile.service';

@Component({
  selector: 'app-sidebar-widget',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="mx-auto w-full max-w-60 rounded-2xl border border-gray-200 bg-gray-50 px-4 py-5">
      <div class="mb-4 flex items-center gap-3">
        <div class="flex h-11 w-11 items-center justify-center overflow-hidden rounded-full bg-brand-500 text-sm font-semibold text-white">
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

      <div class="mb-4">
        <div class="mb-1 flex items-center justify-between text-xs font-medium text-gray-500">
          <span>Profile completion</span>
          <span>{{ completion() }}%</span>
        </div>
        <div class="h-2 rounded-full bg-gray-200">
          <div
            class="h-2 rounded-full bg-brand-500 transition-all duration-300"
            [style.width.%]="completion()"
          ></div>
        </div>
      </div>

      <button
        type="button"
        (click)="logout()"
        class="flex w-full items-center justify-center rounded-lg bg-brand-500 px-4 py-3 text-sm font-medium text-white transition hover:bg-brand-600"
      >
        Sign out
      </button>
    </div>
  `
})
export class SidebarWidgetComponent {
  private readonly authService = inject(AuthService);
  private readonly profileService = inject(ProfileService);

  readonly profile = this.profileService.profile;
  readonly completion = this.profileService.completionPercentage;

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
