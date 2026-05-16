import { Component, signal, LOCALE_ID } from '@angular/core';
import { CommonModule, registerLocaleData } from '@angular/common';
import { Router } from '@angular/router';
import localeFr from '@angular/common/locales/fr';

registerLocaleData(localeFr);

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
  providers: [{ provide: LOCALE_ID, useValue: 'fr-FR' }],
  template: `
    <div class="history-container animate-fade-in">
      <div class="history-header">
        <h2 class="history-title">Historique des Analyses</h2>
        <p class="history-subtitle">Retrouvez toutes les entreprises que vous avez analysées précédemment.</p>
      </div>

      @if (history().length === 0) {
        <div class="empty-state">
          <span class="material-symbols-outlined empty-icon">analytics</span>
          <h3>Aucune analyse disponible</h3>
          <p>Commencez par analyser une entreprise depuis le détail d'une offre.</p>
        </div>
      } @else {
        <div class="history-grid">
          @for (item of history(); track item.id) {
            <div class="history-card">
              <div class="card-body">
                <div class="company-brand">
                  <div class="company-logo">{{ getInitials(item.companyName) }}</div>
                  <div class="company-meta">
                    <h3 class="company-name">{{ item.companyName }}</h3>
                    <div class="job-badge">
                      <span class="material-symbols-outlined text-[14px]">work</span>
                      {{ item.jobTitle || 'Poste non spécifié' }}
                    </div>
                  </div>
                </div>

                <div class="card-details">
                  <p class="summary-text">{{ item.data?.summary || 'Aucun résumé disponible.' }}</p>
                  
                  <div class="footer-meta">
                    <div class="date-info">
                      <span class="material-symbols-outlined text-[14px]">calendar_today</span>
                      Analysé le {{ item.analyzedAt | date:'d MMMM yyyy, HH:mm' }}
                    </div>
                    
                    <div class="actions">
                      <div class="score-pill" [class.high]="(item.data?.compatibilityScore ?? 0) >= 70">
                        <span class="score-dot"></span>
                        {{ item.data?.compatibilityScore ?? 0 }}% Match
                      </div>
                      <button class="btn-details" (click)="openDetails(item)">
                        Détails
                        <span class="material-symbols-outlined text-[16px]">arrow_forward</span>
                      </button>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .history-container {
      padding: 32px 40px;
      max-width: 1100px;
      margin: 0 auto;
    }

    .history-header {
      margin-bottom: 32px;
    }

    .history-title {
      font-size: 24px;
      font-weight: 800;
      color: #1e293b;
      margin: 0 0 4px 0;
      font-family: 'Lato', sans-serif;
    }

    .history-subtitle {
      font-size: 14px;
      color: #64748b;
      margin: 0;
    }

    .history-grid {
      display: flex;
      flex-direction: column;
      gap: 16px;
    }

    .history-card {
      background: white;
      border: 1px solid #e2e8f0;
      border-radius: 16px;
      transition: all 0.2s ease;
      overflow: hidden;
    }

    .history-card:hover {
      border-color: #cbd5e1;
      box-shadow: 0 4px 12px rgba(0, 0, 0, 0.03);
      transform: translateY(-2px);
    }

    .card-body {
      padding: 24px;
    }

    .company-brand {
      display: flex;
      align-items: flex-start;
      gap: 16px;
      margin-bottom: 20px;
    }

    .company-logo {
      width: 48px;
      height: 48px;
      background: #f1f5f9;
      color: #475569;
      display: flex;
      align-items: center;
      justify-content: center;
      border-radius: 12px;
      font-weight: 800;
      font-size: 16px;
      border: 1px solid #e2e8f0;
      flex-shrink: 0;
    }

    .company-name {
      font-size: 18px;
      font-weight: 700;
      color: #0f172a;
      margin: 0 0 6px 0;
    }

    .job-badge {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      padding: 4px 10px;
      background: #eff6ff;
      color: #2563eb;
      border-radius: 8px;
      font-size: 12px;
      font-weight: 600;
    }

    .summary-text {
      font-size: 13.5px;
      color: #475569;
      line-height: 1.6;
      margin: 0 0 20px 0;
      display: -webkit-box;
      -webkit-line-clamp: 2;
      -webkit-box-orient: vertical;
      overflow: hidden;
    }

    .footer-meta {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding-top: 16px;
      border-top: 1px solid #f1f5f9;
    }

    .date-info {
      display: flex;
      align-items: center;
      gap: 6px;
      font-size: 12px;
      color: #94a3b8;
      font-weight: 500;
    }

    .actions {
      display: flex;
      align-items: center;
      gap: 12px;
    }

    .score-pill {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 6px 12px;
      background: #fef2f2;
      color: #dc2626;
      border-radius: 100px;
      font-size: 12px;
      font-weight: 700;
    }

    .score-pill.high {
      background: #f0fdf4;
      color: #16a34a;
    }

    .score-dot {
      width: 6px;
      height: 6px;
      background: currentColor;
      border-radius: 50%;
    }

    .btn-details {
      display: flex;
      align-items: center;
      gap: 6px;
      padding: 8px 16px;
      background: #1a91f0;
      color: white;
      border: none;
      border-radius: 10px;
      font-size: 12px;
      font-weight: 700;
      cursor: pointer;
      transition: all 0.2s;
    }

    .btn-details:hover {
      background: #0c1986;
      box-shadow: 0 4px 12px rgba(26, 145, 240, 0.3);
    }

    .empty-state {
      background: white;
      border: 1px dashed #cbd5e1;
      border-radius: 24px;
      padding: 60px 40px;
      text-align: center;
    }

    .empty-icon {
      font-size: 48px;
      color: #94a3b8;
      margin-bottom: 16px;
    }

    .empty-state h3 {
      font-size: 18px;
      color: #1e293b;
      margin: 0 0 8px 0;
    }

    .empty-state p {
      font-size: 14px;
      color: #64748b;
      margin: 0;
    }

    .animate-fade-in {
      animation: fadeIn 0.4s ease-out;
    }

    @keyframes fadeIn {
      from { opacity: 0; transform: translateY(10px); }
      to { opacity: 1; transform: translateY(0); }
    }
  `]
})
export class OffersCompanyComponent {
  readonly history = signal<CompanyHistoryItem[]>([]);

  constructor(private readonly router: Router) {
    this.loadHistory();
  }

  getInitials(name: string): string {
    if (!name) return '??';
    return name.split(' ').map(n => n[0]).join('').toUpperCase().substring(0, 2);
  }

  openDetails(item: CompanyHistoryItem): void {
    // CRITICAL: Prioritize rawPayload to ensure all details (culture, salaries, etc.) are available
    const payload = item.rawPayload ?? { 
      intelligence: {
        nom: item.companyName,
        summary: item.data?.summary ?? '',
        // Fallback with limited data if rawPayload was never saved
      }, 
      score: item.data?.compatibilityScore ?? 0 
    };

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
