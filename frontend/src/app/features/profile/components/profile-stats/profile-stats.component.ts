import { Component, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatIconModule } from '@angular/material/icon';

export interface ChecklistItem {
  id: string;
  label: string;
  completed: boolean;
}

@Component({
  selector: 'app-profile-stats',
  standalone: true,
  imports: [CommonModule, MatProgressBarModule, MatIconModule],
  template: `
    <div class="bg-white border border-slate-200 rounded-xl p-5 mt-4">
      <div class="flex justify-between items-center mb-3">
        <span class="text-[14px] font-semibold text-slate-700">Complétude du profil</span>
        <span class="text-[14px] font-bold text-indigo-600">{{ percentage() }}%</span>
      </div>

      <div class="h-1.5 w-full bg-slate-100 rounded-full overflow-hidden mb-5">
        <div class="h-full bg-indigo-600 rounded-full transition-all duration-500 ease-out" 
             [style.width.%]="percentage()"></div>
      </div>

      <!-- Checklist -->
      <div class="flex flex-wrap gap-y-4 gap-x-6 md:gap-x-10">
        @for (item of checklist(); track item.id) {
          <div class="flex items-center gap-2">
            @if (item.completed) {
              <div class="w-5 h-5 rounded-full bg-emerald-50 flex items-center justify-center">
                <mat-icon class="!text-[14px] w-[14px] h-[14px] text-emerald-600">check</mat-icon>
              </div>
            } @else {
              <div class="w-5 h-5 rounded-full border-2 border-slate-200 bg-white"></div>
            }
            <span class="text-[12px]" [ngClass]="item.completed ? 'text-slate-700 font-medium' : 'text-slate-400'">
              {{ item.label }}
            </span>
          </div>
        }
      </div>
    </div>
  `,
  styles: [`
    :host { display: block; }
  `]
})
export class ProfileStatsComponent {
  percentage = input.required<number>();
  checklist = input.required<ChecklistItem[]>();
}
