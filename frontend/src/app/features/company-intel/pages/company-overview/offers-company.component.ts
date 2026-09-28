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
                  <p class="summary-text">{{ item.data.summary || 'Aucun résumé disponible.' }}</p>
                  
                  <div class="card-footer">
                    <div class="date-info">
                      <span class="material-symbols-outlined text-[14px]">calendar_today</span>
                      Analysé le {{ item.analyzedAt | date:'d MMMM yyyy, HH:mm' }}
                    </div>
                    
                    <div class="footer-actions">
                      <div class="score-pill" [class.high]="item.data.compatibilityScore >= 70">
                        <span class="score-dot"></span>
                        {{ item.data.compatibilityScore }}% Match
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
      max-width: 1200px;
      margin: 0 auto;
    }

    .history-header {
      margin-bottom: 32px;
    }

    .history-title {
      font-size: 24px;
      font-weight: 800;
      color: #0f172a;
      margin: 0 0 4px 0;
      font-family: 'Lato', sans-serif;
    }

    .history-subtitle {
      font-size: 14px;
      color: #64748b;
      margin: 0;
    }

    .history-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
      gap: 24px;
    }

    .history-card {
      background: white;
      border: 1px solid #e2e8f0;
      border-radius: 16px;
      transition: all 0.25s cubic-bezier(0.4, 0, 0.2, 1);
      display: flex;
      flex-direction: column;
      height: 100%;
      box-shadow: 0 1px 3px rgba(0, 0, 0, 0.02);
      overflow: hidden;
    }

    .history-card:hover {
      border-color: #c2d6ff;
      box-shadow: 0 8px 24px rgba(70, 95, 255, 0.08);
      transform: translateY(-3px);
    }

    .card-body {
      padding: 24px;
      display: flex;
      flex-direction: column;
      flex-grow: 1;
      height: 100%;
    }

    .company-brand {
      display: flex;
      align-items: center;
      gap: 12px;
      margin-bottom: 16px;
    }

    .company-logo {
      width: 44px;
      height: 44px;
      background: linear-gradient(135deg, #ecf3ff 0%, #dde9ff 100%);
      color: #465fff;
      display: flex;
      align-items: center;
      justify-content: center;
      border-radius: 12px;
      font-weight: 800;
      font-size: 15px;
      border: 1px solid #c2d6ff;
      flex-shrink: 0;
    }

    .company-meta {
      display: flex;
      flex-direction: column;
      min-width: 0;
      flex-grow: 1;
    }

    .company-name {
      font-size: 16px;
      font-weight: 700;
      color: #0f172a;
      margin: 0;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }

    .job-badge {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      padding: 2px 8px;
      background: #ecf3ff;
      color: #465fff;
      border-radius: 6px;
      font-size: 10.5px;
      font-weight: 700;
      margin-top: 4px;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
      max-width: 100%;
    }

    .card-details {
      display: flex;
      flex-direction: column;
      flex-grow: 1;
      justify-content: space-between;
    }

    .summary-text {
      font-size: 13px;
      color: #475569;
      line-height: 1.5;
      margin: 0 0 20px 0;
      display: -webkit-box;
      -webkit-line-clamp: 3;
      -webkit-box-orient: vertical;
      overflow: hidden;
      flex-grow: 1;
    }

    .card-footer {
      display: flex;
      flex-direction: column;
      gap: 12px;
      padding-top: 16px;
      border-top: 1px solid #f1f5f9;
      margin-top: auto;
    }

    .date-info {
      display: flex;
      align-items: center;
      gap: 6px;
      font-size: 11px;
      color: #94a3b8;
      font-weight: 500;
    }

    .footer-actions {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 8px;
    }

    .score-pill {
      display: flex;
      align-items: center;
      gap: 6px;
      padding: 4px 10px;
      background: #fef2f2;
      color: #dc2626;
      border-radius: 100px;
      font-size: 11px;
      font-weight: 700;
    }

    .score-pill.high {
      background: #f0fdf4;
      color: #16a34a;
    }

    .score-dot {
      width: 5px;
      height: 5px;
      background: currentColor;
      border-radius: 50%;
    }

    .btn-details {
      display: flex;
      align-items: center;
      gap: 4px;
      padding: 6px 14px;
      background: #465fff;
      color: white;
      border: none;
      border-radius: 8px;
      font-size: 11px;
      font-weight: 700;
      cursor: pointer;
      transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1);
    }

    .btn-details:hover {
      background: #3641f5;
      box-shadow: 0 4px 12px rgba(70, 95, 255, 0.3);
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
        summary: item.data.summary ?? '',
        // Fallback with limited data if rawPayload was never saved
      }, 
      score: item.data.compatibilityScore 
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
