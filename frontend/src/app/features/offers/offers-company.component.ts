import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';

interface CompanyIntelData {
  nom: string;
  summary: string;
  compatibilityScore: number;
}

interface CompanyHistoryItem {
  id: string;
  companyName: string;
  jobTitle: string;
  analyzedAt: string;
  data: CompanyIntelData;
  rawPayload?: any;
}

@Component({
  selector: 'app-offers-company',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="p-6 flex flex-col gap-5">
      <div class="bg-white border border-slate-200 rounded-2xl p-5">
        <div class="flex items-center justify-between mb-3">
          <h2 class="text-2xl font-bold text-slate-900 m-0">Historique Company Intelligence</h2>
          <button
            class="px-4 py-2 rounded-lg border border-slate-200 bg-white text-slate-700 font-semibold hover:bg-slate-50"
            (click)="backToOfferAnalysis()">
            Retour à l'analyse offre
          </button>
        </div>
        <p class="text-sm text-slate-500 m-0">Toutes les entreprises déjà analysées. Cliquez sur "Show details" pour ouvrir le détail complet.</p>
      </div>

      @if (history().length === 0) {
        <div class="bg-white border border-slate-200 rounded-2xl p-8 text-center text-slate-500">
          Aucune analyse entreprise disponible.
        </div>
      } @else {
        <div class="flex flex-col gap-4">
          @for (item of history(); track item.id) {
            <div class="bg-white border border-slate-200 rounded-2xl p-5 flex items-center justify-between gap-4">
              <div class="min-w-0">
                <div class="text-xl font-bold text-slate-900">{{ item.companyName }}</div>
                <div class="text-sm text-slate-500 mt-1 truncate">{{ item.jobTitle || 'Poste non spécifié' }}</div>
                <div class="text-xs text-slate-400 mt-2">
                  Analysé le {{ item.analyzedAt | date:'short' }}
                </div>
                @if (item.data?.summary) {
                  <div class="text-xs text-slate-600 mt-2 line-clamp-2">{{ item.data.summary }}</div>
                }
              </div>
              <div class="flex items-center gap-3 shrink-0">
                <div class="px-4 py-2 rounded-full bg-emerald-50 text-emerald-700 font-bold text-sm">
                  {{ item.data?.compatibilityScore ?? 0 }}%
                </div>
                <button
                  class="px-4 py-2 rounded-lg bg-indigo-700 text-white font-semibold hover:bg-indigo-800"
                  (click)="openDetails(item)">
                  Show details
                </button>
              </div>
            </div>
          }
        </div>
      }
    </div>
  `
})
export class OffersCompanyComponent {
  readonly history = signal<CompanyHistoryItem[]>([]);

  constructor(private readonly router: Router) {
    this.loadHistory();
  }

  backToOfferAnalysis(): void {
    this.router.navigate(['/offers/analyze']);
  }

  openDetails(item: CompanyHistoryItem): void {
    const payload = item.rawPayload ?? { intelligence: item.data, score: item.data?.compatibilityScore ?? 0 };
    this.router.navigate(['/offers/company-analysis'], {
      state: {
        companyIntelPayload: payload,
        companyName: item.companyName,
        jobTitle: item.jobTitle
      }
    });
  }

  private loadHistory(): void {
    try {
      const raw = localStorage.getItem('nextstep.company.history');
      if (!raw) {
        this.history.set([]);
        return;
      }
      const parsed = JSON.parse(raw);
      if (!Array.isArray(parsed)) {
        this.history.set([]);
        return;
      }
      const normalized = parsed.map((h: any) => ({
        id: String(h.id ?? Date.now()),
        companyName: String(h.companyName ?? ''),
        jobTitle: String(h.jobTitle ?? ''),
        analyzedAt: String(h.analyzedAt ?? new Date().toISOString()),
        data: {
          nom: h?.data?.nom ?? h?.companyName ?? '',
          summary: h?.data?.summary ?? '',
          compatibilityScore: h?.data?.compatibilityScore ?? 0
        },
        rawPayload: h?.rawPayload
      }));
      this.history.set(normalized);
    } catch {
      this.history.set([]);
    }
  }
}
