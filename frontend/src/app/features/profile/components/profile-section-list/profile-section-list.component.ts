import { Component, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';

@Component({
  selector: 'app-profile-section-list',
  standalone: true,
  imports: [CommonModule, MatIconModule, MatButtonModule],
  template: `
    <div class="bg-white border border-slate-200 rounded-xl overflow-hidden mt-6 font-lato">
      <!-- Header -->
      <div class="px-5 py-4 border-bottom border-slate-100 flex justify-between items-center bg-slate-50/50">
        <div class="flex items-center gap-3">
          <div class="w-8 h-8 rounded-lg bg-white border border-slate-200 flex items-center justify-center shadow-sm">
            <mat-icon class="!text-[18px] w-[18px] h-[18px] text-primary">{{ icon() }}</mat-icon>
          </div>
          <div>
            <h3 class="text-[14px] font-bold text-slate-900 m-0">{{ title() }}</h3>
            <span class="text-[10px] text-slate-500 uppercase font-bold tracking-wider">{{ count() }} items</span>
          </div>
        </div>
        <button class="btn btn-secondary !h-8 !px-3 !text-[12px] flex items-center" (click)="add.emit()">
          <mat-icon class="!text-[16px] w-[16px] h-[16px] mr-1">add</mat-icon>
          Ajouter
        </button>
      </div>

      <!-- Content -->
      <div class="p-5">
        @if (items().length > 0) {
          <div class="space-y-6">
            <ng-content></ng-content>
          </div>
        } @else {
          <!-- Empty State -->
          <div class="py-10 flex flex-col items-center text-center">
            <div class="w-16 h-16 bg-slate-50 rounded-full flex items-center justify-center mb-4">
              <mat-icon class="!text-[32px] w-[32px] h-[32px] text-slate-300">{{ icon() }}</mat-icon>
            </div>
            <h4 class="text-[14px] font-semibold text-slate-900 m-0">Aucun(e) {{ title() | lowercase }}</h4>
            <p class="text-[12px] text-slate-500 mt-1 max-w-[240px]">Commencez par ajouter votre première entrée pour booster votre profil.</p>
            <button class="btn btn-primary !rounded-lg !text-[12px] !mt-4" (click)="add.emit()">
              Ajouter un(e) {{ title() | lowercase }}
            </button>
          </div>
        }
      </div>
    </div>
  `,
  styles: [`
    :host { display: block; }
    .border-bottom { border-bottom-width: 1px; }
  `]
})
export class ProfileSectionListComponent {
  title = input.required<string>();
  icon = input.required<string>();
  count = input<number>(0);
  items = input<any[]>([]);

  add = output<void>();
}
