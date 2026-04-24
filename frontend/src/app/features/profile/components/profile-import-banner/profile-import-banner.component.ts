import { Component, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-profile-import-banner',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule],
  template: `
    <div class="bg-primary-light border border-primary-light rounded-xl p-5 mt-4 flex flex-col md:flex-row items-center gap-5 font-lato">
      <div class="w-12 h-12 bg-white rounded-lg flex items-center justify-center shadow-sm shrink-0">
        <svg class="w-6 h-6 text-primary" viewBox="0 0 24 24" fill="currentColor">
          <path d="M19 3a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h14m-.5 15.5v-5.3a3.26 3.26 0 0 0-3.26-3.26c-.85 0-1.84.52-2.32 1.3v-1.11h-2.79v8.37h2.79v-4.93c0-.77.62-1.4 1.39-1.4a1.4 1.4 0 0 1 1.4 1.4v4.93h2.79M6.88 8.56a1.68 1.68 0 0 0 1.68-1.68c0-.93-.75-1.69-1.68-1.69a1.69 1.69 0 0 0-1.69 1.69c0 .93.76 1.68 1.69 1.68m1.39 9.94v-8.37H5.5v8.37h2.77z"/>
        </svg>
      </div>

      <div class="flex-1 text-center md:text-left">
        <h4 class="text-[14px] font-bold text-slate-900 m-0">Gagnez du temps !</h4>
        <p class="text-[12px] text-slate-600 mt-1">Importez vos données LinkedIn ou votre CV existant pour remplir votre profil en un clic.</p>
      </div>

      <div class="flex items-center gap-3 w-full md:w-auto">
        <button class="btn btn-primary !text-[12px] !py-2 flex-1 md:flex-none" 
                (click)="onLinkedIn.emit()">
          LinkedIn Import
        </button>
        <button class="btn btn-secondary !text-[12px] !py-2 flex-1 md:flex-none"
                (click)="onCV.emit()">
          Importer mon CV
        </button>
      </div>
    </div>
  `,
  styles: [`
    :host { display: block; }
  `]
})
export class ProfileImportBannerComponent {
  onLinkedIn = output<void>();
  onCV = output<void>();
}
