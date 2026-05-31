import { Component, inject, signal, computed, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ResumeStateService } from './resume-state.service';
import { TemplateChronoComponent } from './template-chrono.component';
import { TemplateCircularComponent } from './template-circular.component';
import { ResumeData } from './resume.model';

type TemplateComponent = typeof TemplateChronoComponent | typeof TemplateCircularComponent;

@Component({
  selector: 'app-cv-preview',
  standalone: true,
  imports: [CommonModule, TemplateChronoComponent, TemplateCircularComponent],
  template: `
    <div class="flex flex-col h-full bg-slate-100">
      <!-- Toolbar -->
      <div class="flex items-center justify-between px-4 py-2 bg-white border-b border-slate-200">
        <div class="flex items-center gap-4">
          <!-- Zoom Controls -->
          <div class="flex items-center gap-2">
            <button
              (click)="zoomOut()"
              class="p-1.5 rounded-lg hover:bg-slate-100 transition-colors"
              [disabled]="zoom() <= 50"
              [class.opacity-50]="zoom() <= 50"
            >
              <svg class="w-4 h-4 text-slate-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0zM13 10H7"/>
              </svg>
            </button>
            <span class="text-sm font-medium text-slate-600 w-12 text-center">
              {{ zoom() }}%
            </span>
            <button
              (click)="zoomIn()"
              class="p-1.5 rounded-lg hover:bg-slate-100 transition-colors"
              [disabled]="zoom() >= 150"
              [class.opacity-50]="zoom() >= 150"
            >
              <svg class="w-4 h-4 text-slate-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0zM10 7v6m3-3H7"/>
              </svg>
            </button>
          </div>

          <!-- Zoom Slider -->
          <input
            type="range"
            min="50"
            max="150"
            [value]="zoom()"
            (input)="setZoom($event)"
            class="w-32 accent-brand-500"
          />

          <!-- Reset Zoom -->
          <button
            (click)="resetZoom()"
            class="text-xs px-2 py-1 rounded-lg hover:bg-slate-100 text-slate-500 transition-colors"
          >
            Reset
          </button>
        </div>

        <!-- Actions -->
        <div class="flex items-center gap-2">
          <button
            (click)="printCv()"
            class="flex items-center gap-2 px-3 py-1.5 rounded-lg bg-brand-500 text-white text-sm font-medium hover:bg-brand-600 transition-colors"
          >
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 17h2a2 2 0 002-2v-4a2 2 0 00-2-2H5a2 2 0 00-2 2v4a2 2 0 002 2h2m2 4h6a2 2 0 002-2v-4a2 2 0 00-2-2H9a2 2 0 00-2 2v4a2 2 0 002 2zm8-12V5a2 2 0 00-2-2H9a2 2 0 00-2 2v4h10z"/>
            </svg>
            Télécharger PDF
          </button>
        </div>
      </div>

      <!-- Preview Area -->
      <div class="flex-1 overflow-auto p-8 flex justify-center" #previewContainer>
        <div
          class="relative transition-transform duration-200 ease-out origin-top"
          [style.transform]="'scale(' + zoom() / 100 + ')'"
        >
          <!-- A4 Page -->
          <div
            class="bg-white shadow-lg print:shadow-none"
            style="width: 210mm; min-height: 297mm; aspect-ratio: 21 / 29.7;"
          >
            @switch (resumeState.selectedTemplate()) {
              @case ('chrono') {
                <app-template-chrono [data]="resumeData()" />
              }
              @case ('circular') {
                <app-template-circular [data]="resumeData()" />
              }
              @default {
                <app-template-chrono [data]="resumeData()" />
              }
            }
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    :host {
      display: block;
      height: 100%;
    }

    @media print {
      body * {
        visibility: hidden;
      }

      .print-target,
      .print-target * {
        visibility: visible;
      }

      .print-target {
        position: absolute;
        left: 0;
        top: 0;
        width: 100%;
        transform: none !important;
      }
    }
  `],
})
export class CvPreviewComponent {
  protected readonly resumeState = inject(ResumeStateService);

  @ViewChild('previewContainer') previewContainer!: ElementRef<HTMLDivElement>;

  protected readonly resumeData = this.resumeState.resumeData;

  zoom = signal<number>(90);

  protected readonly zoomClass = computed(() => {
    const z = this.zoom();
    if (z <= 60) return 'scale-50';
    if (z <= 70) return 'scale-75';
    if (z <= 80) return 'scale-90';
    if (z <= 100) return 'scale-100';
    if (z <= 125) return 'scale-125';
    return 'scale-150';
  });

  protected zoomIn(): void {
    this.zoom.update(z => Math.min(z + 10, 150));
  }

  protected zoomOut(): void {
    this.zoom.update(z => Math.max(z - 10, 50));
  }

  protected resetZoom(): void {
    this.zoom.set(90);
  }

  protected setZoom(event: Event): void {
    const target = event.target as HTMLInputElement;
    this.zoom.set(parseInt(target.value, 10));
  }

  protected printCv(): void {
    const printWindow = window.open('', '_blank');
    if (!printWindow) {
      window.print();
      return;
    }

    const previewElement = this.previewContainer?.nativeElement;
    if (!previewElement) {
      window.print();
      return;
    }

    const cvContent = previewElement.querySelector('[style*="210mm"]');
    if (!cvContent) {
      window.print();
      return;
    }

    const data = this.resumeState.resumeData();
    const primaryColor = data.primaryColor;

    printWindow.document.write(`
      <!DOCTYPE html>
      <html>
      <head>
        <title>CV - ${data.personalDetails.fullName || 'Document'}</title>
        <style>
          @page {
            size: A4;
            margin: 0;
          }
          * {
            margin: 0;
            padding: 0;
            box-sizing: border-box;
          }
          body {
            font-family: 'Outfit', 'Lato', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
            -webkit-print-color-adjust: exact;
            print-color-adjust: exact;
          }
          .cv-page {
            width: 210mm;
            min-height: 297mm;
            margin: 0 auto;
          }
          .text-xs { font-size: 10px; }
          .text-sm { font-size: 12px; }
          .text-base { font-size: 14px; }
          .text-xl { font-size: 20px; }
          .text-2xl { font-size: 24px; }
          .font-bold { font-weight: 700; }
          .font-semibold { font-weight: 600; }
          .font-medium { font-weight: 500; }
          .uppercase { text-transform: uppercase; }
          .tracking-wide { letter-spacing: 0.025em; }
          .tracking-wider { letter-spacing: 0.05em; }
          .text-slate-800 { color: #1e293b; }
          .text-slate-600 { color: #475569; }
          .text-slate-500 { color: #64748b; }
          .text-slate-400 { color: #94a3b8; }
          .text-white { color: #ffffff; }
          .bg-white { background-color: #ffffff; }
          .opacity-90 { opacity: 0.9; }
          .opacity-80 { opacity: 0.8; }
          .opacity-70 { opacity: 0.7; }
          .flex { display: flex; }
          .flex-col { flex-direction: column; }
          .items-start { align-items: flex-start; }
          .items-center { align-items: center; }
          .justify-between { justify-content: space-between; }
          .justify-center { justify-content: center; }
          .flex-wrap { flex-wrap: wrap; }
          .flex-shrink-0 { flex-shrink: 0; }
          .flex-1 { flex: 1; }
          .w-full { width: 100%; }
          .w-1\\/3 { width: 33.333333%; }
          .w-2\\/3 { width: 66.666667%; }
          .w-4 { width: 1rem; }
          .w-20 { width: 5rem; }
          .w-24 { width: 6rem; }
          .w-3 { width: 0.75rem; }
          .w-1 { width: 0.25rem; }
          .w-1\\.5 { width: 0.375rem; }
          .h-full { height: 100%; }
          .h-4 { height: 1rem; }
          .h-20 { height: 5rem; }
          .h-24 { height: 6rem; }
          .h-3 { height: 0.75rem; }
          .h-1\\.5 { height: 0.375rem; }
          .h-1 { height: 0.25rem; }
          .p-8 { padding: 2rem; }
          .p-6 { padding: 1.5rem; }
          .px-3 { padding-left: 0.75rem; padding-right: 0.75rem; }
          .py-1 { padding-top: 0.25rem; padding-bottom: 0.25rem; }
          .pb-4 { padding-bottom: 1rem; }
          .pb-1 { padding-bottom: 0.25rem; }
          .mb-6 { margin-bottom: 1.5rem; }
          .mb-4 { margin-bottom: 1rem; }
          .mb-3 { margin-bottom: 0.75rem; }
          .mb-2 { margin-bottom: 0.5rem; }
          .mb-1 { margin-bottom: 0.25rem; }
          .mt-1 { margin-top: 0.25rem; }
          .mt-2 { margin-top: 0.5rem; }
          .mt-3 { margin-top: 0.75rem; }
          .mt-1\\.5 { margin-top: 0.375rem; }
          .ml-2 { margin-left: 0.5rem; }
          .ml-4 { margin-left: 1rem; }
          .gap-1 { gap: 0.25rem; }
          .gap-2 { gap: 0.5rem; }
          .gap-4 { gap: 1rem; }
          .space-y-1 > * + * { margin-top: 0.25rem; }
          .space-y-2 > * + * { margin-top: 0.5rem; }
          .space-y-3 > * + * { margin-top: 0.75rem; }
          .space-y-4 > * + * { margin-top: 1rem; }
          .border-b { border-bottom-width: 1px; }
          .border-b-2 { border-bottom-width: 2px; }
          .border-2 { border-width: 2px; }
          .border-4 { border-width: 4px; }
          .border-white\\/30 { border-color: rgba(255,255,255,0.3); }
          .rounded-full { border-radius: 9999px; }
          .rounded-lg { border-radius: 0.5rem; }
          .overflow-hidden { overflow: hidden; }
          .truncate { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
          .whitespace-nowrap { white-space: nowrap; }
          .leading-relaxed { line-height: 1.625; }
          .relative { position: relative; }
          .absolute { position: absolute; }
          .left-0 { left: 0; }
          .-left-6 { left: -1.5rem; }
          .top-1\\.5 { top: 0.375rem; }
          .top-2 { top: 0.5rem; }
          .bottom-2 { bottom: 0.5rem; }
          .w-0\\.5 { width: 0.125rem; }
          .pl-6 { padding-left: 1.5rem; }
          .pl-4 { padding-left: 1rem; }
          .last\\:mb-0:last-child { margin-bottom: 0; }
        </style>
      </head>
      <body>
        <div class="cv-page">
          ${cvContent.outerHTML}
        </div>
      </body>
      </html>
    `);

    printWindow.document.close();

    setTimeout(() => {
      printWindow.print();
      printWindow.close();
    }, 500);
  }
}
