import { Component, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';

@Component({
  selector: 'app-profile-sidebar',
  standalone: true,
  imports: [CommonModule, MatIconModule, MatButtonModule],
  template: `
    <div class="flex flex-col gap-4 sticky top-6 font-lato">
      
      <!-- Card 1: Force du Profil -->
      <div class="bg-white border border-slate-200 rounded-xl p-4">
        <h3 class="text-[13px] font-bold text-slate-900 mb-4 flex items-center gap-2">
          <mat-icon class="!text-[18px] w-[18px] h-[18px] text-primary">bolt</mat-icon>
          Force du profil
        </h3>

        <div class="space-y-4">
          <div *ngFor="let metric of metrics()" class="space-y-1.5">
            <div class="flex justify-between text-[11px] font-medium">
              <span class="text-slate-500">{{ metric.label }}</span>
              <span class="text-slate-900 font-bold">{{ metric.value }}{{ metric.suffix }}</span>
            </div>
            <div class="h-1 w-full bg-slate-50 rounded-full overflow-hidden">
              <div class="h-full bg-primary rounded-full" [style.width.%]="metric.percent"></div>
            </div>
          </div>
        </div>
      </div>

      <!-- Card 2: Suggestions IA -->
      <div class="bg-primary-light border border-primary-light rounded-xl p-4">
        <div class="inline-flex px-2 py-0.5 bg-primary text-white text-[9px] font-bold rounded uppercase tracking-wider mb-3">
          Suggestion IA
        </div>
        <p class="text-[12px] text-primary-dark leading-relaxed font-medium">
          "Ajoutez vos projets pour augmenter votre score ATS de 15 points."
        </p>
        <button class="!text-primary !text-[12px] !p-0 !mt-2 !font-bold !min-h-0 bg-transparent border-none cursor-pointer hover:underline">
          Ajouter maintenant →
        </button>
      </div>

      <!-- Card 3: Aperçu CV -->
      <div class="bg-white border border-slate-200 rounded-xl p-4">
        <h3 class="text-[13px] font-bold text-slate-900 mb-3">Aperçu CV</h3>
        
        <!-- CV Skeleton -->
        <div class="aspect-[1/1.4] bg-slate-50 rounded-lg p-3 border border-slate-100 mb-3 relative group cursor-pointer overflow-hidden">
          <div class="space-y-2">
            <div class="w-1/3 h-2 bg-slate-200 rounded"></div>
            <div class="w-full h-1 bg-slate-100 rounded"></div>
            <div class="w-full h-1 bg-slate-100 rounded"></div>
            <div class="w-2/3 h-1 bg-slate-100 rounded"></div>
            <div class="pt-4 w-1/4 h-2 bg-slate-200 rounded"></div>
            <div class="w-full h-1 bg-slate-100 rounded"></div>
            <div class="w-full h-1 bg-slate-100 rounded"></div>
          </div>
          <div class="absolute inset-0 bg-primary/0 group-hover:bg-primary/5 transition-colors flex items-center justify-center">
            <mat-icon class="!text-primary opacity-0 group-hover:opacity-100 transition-opacity">zoom_in</mat-icon>
          </div>
        </div>

        <div class="flex justify-between items-center mb-4">
          <div>
            <p class="text-[11px] font-bold text-slate-900">Modern Template</p>
            <p class="text-[10px] text-slate-500">Score ATS: 78/100</p>
          </div>
          <div class="w-8 h-8 rounded-full bg-emerald-50 flex items-center justify-center">
            <mat-icon class="!text-[16px] text-emerald-600">check_circle</mat-icon>
          </div>
        </div>

        <button class="btn btn-primary !w-full !text-[12px] !py-2.5" (click)="generateCV.emit()">
          Générer le CV
        </button>
      </div>

    </div>
  `,
  styles: [`
    :host { display: block; }
  `]
})
export class ProfileSidebarComponent {
  metrics = input<any[]>([
    { label: 'Complétude', value: 75, suffix: '%', percent: 75 },
    { label: 'Mots-clés ATS', value: '14/20', suffix: '', percent: 70 },
    { label: 'Lisibilité', value: 82, suffix: '', percent: 82 },
  ]);

  generateCV = output<void>();
}
