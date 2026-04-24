import { Component, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-profile-hero',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule],
  template: `
    <div class="bg-white border border-slate-200 rounded-xl p-5 flex flex-col md:flex-row items-start md:items-center gap-6 font-lato">
      <!-- Avatar -->
      <div class="w-14 h-14 bg-gradient-to-br from-blue-600 to-indigo-700 rounded-[14px] flex items-center justify-center text-white font-bold text-xl shadow-sm shrink-0">
        {{ initials() }}
      </div>

      <!-- Info Block -->
      <div class="flex-1 min-w-0">
        <div class="flex items-center gap-2 flex-wrap">
          <h2 class="text-[16px] font-semibold text-slate-900 m-0">{{ name() }}</h2>
          <span class="px-2 py-0.5 bg-blue-50 text-blue-600 text-[10px] font-bold rounded-full uppercase tracking-wider">Pro Account</span>
        </div>
        
        <p class="text-[12px] text-slate-500 mt-1 flex items-center gap-2">
          <span>{{ title() }}</span>
          <span class="w-1 h-1 bg-slate-300 rounded-full"></span>
          <span>{{ location() }}</span>
        </p>

        <!-- Tags Pills -->
        <div class="flex flex-wrap gap-2 mt-3">
          @for (tag of tags(); track tag) {
            <span class="px-2.5 py-1 bg-slate-50 border border-slate-100 text-slate-600 text-[11px] font-medium rounded-md">
              {{ tag }}
            </span>
          }
          <span class="px-2.5 py-1 bg-emerald-50 border border-emerald-100 text-emerald-600 text-[11px] font-medium rounded-md">
            Disponible immédiatement
          </span>
        </div>
      </div>

      <!-- Actions -->
      <div class="flex items-center gap-3 shrink-0 mt-4 md:mt-0 w-full md:w-auto">
        <button class="btn btn-secondary !text-[13px] !px-4 !h-10 flex-1 md:flex-none flex items-center justify-center">
          <mat-icon class="!text-[18px] w-[18px] h-[18px] mr-1">visibility</mat-icon>
          Prévisualiser CV
        </button>
        <button class="btn btn-primary !text-[13px] !px-4 !h-10 flex-1 md:flex-none flex items-center justify-center">
          <mat-icon class="!text-[18px] w-[18px] h-[18px] mr-1">edit</mat-icon>
          Modifier
        </button>
      </div>
    </div>
  `,
  styles: [`
    :host { display: block; }
  `]
})
export class ProfileHeroComponent {
  name = input.required<string>();
  title = input.required<string>();
  location = input.required<string>();
  tags = input<string[]>([]);
  
  initials = input<string>('SN');
}
