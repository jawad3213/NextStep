import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { OfferService } from '../../services/offer.service';

interface CvHistoryItem {
  id: string;
  title: string;
  templateSlug: string;
  templateName: string;
  fileUrl: string;
  fileSizeBytes: number;
  createdAt: string;
  updatedAt: string;
  targetedOfferId?: string | null;
  targetedOfferTitle?: string | null;
  targetedOfferCompany?: string | null;
}

@Component({
  selector: 'app-cv-builder',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="cv-shell">
      <header class="page-header">
        <div class="header-left">
          <h1 class="page-title">Mes CV Generes</h1>
          <p class="page-subtitle">Consultez et telechargez vos CV cibles pour vos candidatures.</p>
        </div>
        <button class="btn-refresh" (click)="loadHistory()" [class.spinning]="loadingHistory()" title="Rafraichir">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="23 4 23 10 17 10"/><polyline points="1 20 1 14 7 14"/><path d="M3.51 9a9 9 0 0 1 14.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0 0 20.49 15"/></svg>
        </button>
      </header>

      <section class="history-section">
        @if (loadingHistory()) {
          <div class="loading-center">
            <div class="spinner-lg"></div>
            <p>Chargement des CV...</p>
          </div>
        } @else if (historyItems().length === 0) {
          <div class="empty-state">
            <div class="empty-icon">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/></svg>
            </div>
            <h3>Aucun CV genere</h3>
            <p>Generez un CV depuis une offre pour le voir ici.</p>
          </div>
        } @else {
          <div class="history-grid">
            @for (item of historyItems(); track item.id) {
              <article class="history-card">
                <div class="card-header">
                  <div class="card-icon">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/></svg>
                  </div>
                  <button class="action-btn danger" title="Supprimer" (click)="deleteCv(item.id)">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="3 6 5 6 21 6"/><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/></svg>
                  </button>
                </div>

                <div class="card-body">
                  <h4 class="cv-title" [title]="getDisplayTitle(item)">{{ getDisplayTitle(item) }}</h4>

                  <div class="target-offer">
                    <span class="offer-label">Offre ciblee</span>
                    @if (item.targetedOfferTitle) {
                      <p class="offer-title">{{ item.targetedOfferTitle }}</p>
                      <p class="offer-company">{{ item.targetedOfferCompany || 'Entreprise non precisee' }}</p>
                    } @else {
                      <p class="offer-title muted">Offre ciblee indisponible</p>
                    }
                  </div>

                  <div class="cv-mini-preview" aria-hidden="true">
                    <div class="mini-header"></div>
                    <div class="mini-line w-80"></div>
                    <div class="mini-line w-60"></div>
                    <div class="mini-line w-90"></div>
                    <div class="mini-line w-75"></div>
                    <div class="mini-line w-55"></div>
                  </div>

                  <div class="meta-tags">
                    <span class="tag tag-blue">{{ item.templateName || item.templateSlug }}</span>
                    <span class="tag tag-gray">{{ item.createdAt | date:'dd MMM yyyy' }}</span>
                  </div>
                </div>

                <div class="card-footer">
                  <span class="file-size">{{ (item.fileSizeBytes / 1024).toFixed(0) }} KB PDF</span>
                  <button class="btn-primary" (click)="downloadCv(item.id)">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/></svg>
                    Ouvrir PDF
                  </button>
                </div>
              </article>
            }
          </div>
        }
      </section>
    </div>
  `,
  styles: [`
    :host {
      display: flex;
      flex-direction: column;
      flex: 1;
      min-height: 0;
      overflow: auto;
    }
    .cv-shell {
      display: flex;
      flex-direction: column;
      min-height: 100%;
      padding: 24px 40px;
      gap: 20px;
      background: #f8f9fc;
    }
    .page-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      gap: 12px;
    }
    .header-left {
      display: grid;
      gap: 4px;
    }
    .page-title {
      margin: 0;
      font-size: 24px;
      font-weight: 800;
      color: #0f172a;
    }
    .page-subtitle {
      margin: 0;
      font-size: 14px;
      color: #475569;
    }
    .btn-refresh {
      width: 40px;
      height: 40px;
      border-radius: 10px;
      border: 1px solid #dbe3ff;
      background: #fff;
      color: #64748b;
      display: flex;
      align-items: center;
      justify-content: center;
      cursor: pointer;
      transition: all .2s;
    }
    .btn-refresh svg {
      width: 18px;
      height: 18px;
    }
    .btn-refresh:hover {
      background: #edf2ff;
      border-color: #465fff;
      color: #465fff;
    }
    .btn-refresh.spinning svg {
      animation: spin 1s linear infinite;
    }
    @keyframes spin {
      to { transform: rotate(360deg); }
    }

    .history-section {
      flex: 1;
      max-width: 1320px;
      width: 100%;
      margin: 0 auto;
    }
    .history-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(290px, 1fr));
      gap: 18px;
      padding-bottom: 26px;
    }
    .history-card {
      background: #fff;
      border: 1px solid #e2e8f0;
      border-radius: 16px;
      padding: 16px;
      display: flex;
      flex-direction: column;
      min-height: 360px;
      position: relative;
      box-shadow: 0 3px 8px rgba(15, 23, 42, 0.05);
      transition: all .25s ease;
    }
    .history-card:hover {
      transform: translateY(-3px);
      border-color: #465fff;
      box-shadow: 0 12px 20px -8px rgba(70, 95, 255, 0.25);
    }
    .history-card::before {
      content: '';
      position: absolute;
      inset: 0 0 auto 0;
      height: 4px;
      border-top-left-radius: 16px;
      border-top-right-radius: 16px;
      background: linear-gradient(90deg, #465fff, #7c3aed);
      opacity: 0;
      transition: opacity .2s ease;
    }
    .history-card:hover::before {
      opacity: 1;
    }

    .card-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 10px;
    }
    .card-icon {
      width: 46px;
      height: 46px;
      border-radius: 12px;
      background: #edf2ff;
      color: #465fff;
      display: flex;
      align-items: center;
      justify-content: center;
    }
    .card-icon svg {
      width: 24px;
      height: 24px;
    }
    .action-btn {
      width: 34px;
      height: 34px;
      border: none;
      border-radius: 8px;
      background: transparent;
      color: #94a3b8;
      cursor: pointer;
      display: flex;
      align-items: center;
      justify-content: center;
      transition: all .2s;
    }
    .action-btn svg {
      width: 16px;
      height: 16px;
    }
    .action-btn.danger:hover {
      color: #d93025;
      background: #fce8e6;
    }

    .card-body {
      flex: 1;
      display: grid;
      gap: 10px;
      margin-bottom: 14px;
    }
    .cv-title {
      margin: 0;
      font-size: 18px;
      line-height: 1.35;
      font-weight: 800;
      color: #0f172a;
      display: -webkit-box;
      -webkit-line-clamp: 2;
      -webkit-box-orient: vertical;
      overflow: hidden;
    }
    .target-offer {
      border: 1px solid #e2e8f0;
      border-radius: 10px;
      background: #f8fafc;
      padding: 10px;
      display: grid;
      gap: 4px;
    }
    .offer-label {
      font-size: 10px;
      font-weight: 700;
      letter-spacing: .06em;
      text-transform: uppercase;
      color: #64748b;
    }
    .offer-title {
      margin: 0;
      font-size: 13px;
      font-weight: 800;
      color: #0f172a;
      line-height: 1.35;
    }
    .offer-company {
      margin: 0;
      font-size: 12px;
      font-weight: 700;
      color: #465fff;
    }
    .offer-title.muted {
      color: #64748b;
      font-weight: 700;
    }

    .cv-mini-preview {
      border: 1px solid #dbe3ff;
      border-radius: 10px;
      background: linear-gradient(180deg, #ffffff 0%, #f8fbff 100%);
      padding: 10px;
      display: grid;
      gap: 6px;
    }
    .mini-header {
      height: 7px;
      width: 40%;
      border-radius: 999px;
      background: #465fff;
    }
    .mini-line {
      height: 6px;
      border-radius: 999px;
      background: #dbe3ff;
    }
    .mini-line.w-90 { width: 90%; }
    .mini-line.w-80 { width: 80%; }
    .mini-line.w-75 { width: 75%; }
    .mini-line.w-60 { width: 60%; }
    .mini-line.w-55 { width: 55%; }

    .meta-tags {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
    }
    .tag {
      display: inline-flex;
      align-items: center;
      border-radius: 999px;
      padding: 4px 10px;
      font-size: 12px;
      font-weight: 700;
    }
    .tag-blue {
      background: #edf2ff;
      color: #3347cc;
    }
    .tag-gray {
      background: #f1f5f9;
      color: #475569;
    }

    .card-footer {
      border-top: 1px solid #f1f5f9;
      padding-top: 12px;
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 10px;
    }
    .file-size {
      font-size: 13px;
      color: #64748b;
      font-weight: 600;
    }
    .btn-primary {
      border: none;
      border-radius: 10px;
      padding: 9px 14px;
      background: #465fff;
      color: #fff;
      display: inline-flex;
      align-items: center;
      gap: 8px;
      font-size: 13px;
      font-weight: 700;
      cursor: pointer;
      transition: all .2s;
    }
    .btn-primary svg {
      width: 15px;
      height: 15px;
    }
    .btn-primary:hover {
      background: #3347cc;
      box-shadow: 0 4px 12px rgba(70, 95, 255, 0.3);
    }

    .loading-center {
      display: grid;
      place-items: center;
      gap: 10px;
      color: #64748b;
      padding: 60px;
    }
    .spinner-lg {
      width: 32px;
      height: 32px;
      border: 3px solid #e2e8f0;
      border-top-color: #465fff;
      border-radius: 50%;
      animation: spin .8s linear infinite;
    }
    .empty-state {
      display: grid;
      justify-items: center;
      text-align: center;
      gap: 8px;
      padding: 60px;
      color: #475569;
    }
    .empty-icon {
      width: 64px;
      height: 64px;
      border-radius: 50%;
      background: #f1f5f9;
      color: #94a3b8;
      display: flex;
      align-items: center;
      justify-content: center;
    }
    .empty-icon svg {
      width: 32px;
      height: 32px;
    }
    .empty-state h3 {
      margin: 0;
      font-size: 18px;
      color: #0f172a;
    }
    .empty-state p {
      margin: 0;
      font-size: 14px;
    }

    @media (max-width: 900px) {
      .cv-shell { padding: 16px; }
      .page-title { font-size: 22px; }
    }
  `],
})
export class CvBuilderComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly offerService = inject(OfferService);
  private readonly baseUrl = environment.apiBaseUrl;

  readonly historyItems = signal<CvHistoryItem[]>([]);
  readonly loadingHistory = signal(false);

  ngOnInit(): void {
    this.loadHistory();
  }

  async loadHistory(): Promise<void> {
    this.loadingHistory.set(true);
    try {
      const data = await firstValueFrom(this.http.get<CvHistoryItem[]>(`${this.baseUrl}/cv/history`));
      this.historyItems.set(await this.enrichTargetOfferData(data));
    } catch {
      this.historyItems.set([]);
    } finally {
      this.loadingHistory.set(false);
    }
  }

  async downloadCv(id: string): Promise<void> {
    try {
      const blob = await firstValueFrom(
        this.http.get(`${this.baseUrl}/cv/${id}/download-file`, { responseType: 'blob' })
      );
      const url = window.URL.createObjectURL(blob);
      window.open(url, '_blank');
    } catch (e) {
      console.error('Download error:', e);
    }
  }

  async deleteCv(id: string): Promise<void> {
    try {
      await firstValueFrom(this.http.delete(`${this.baseUrl}/cv/${id}`));
      this.historyItems.update((items) => items.filter((i) => i.id !== id));
    } catch (e) {
      console.error('Delete error:', e);
    }
  }

  getDisplayTitle(item: CvHistoryItem): string {
    const raw = item.title?.trim();
    if (!raw) return item.templateName || 'CV genere';

    if (this.extractLegacyOfferId(raw)) {
      if (item.targetedOfferTitle) return `CV cible - ${item.targetedOfferTitle}`;
      return 'CV cible';
    }

    return raw;
  }

  private async enrichTargetOfferData(items: CvHistoryItem[]): Promise<CvHistoryItem[]> {
    const enriched = items.map((item) => ({ ...item }));
    const unresolved: Array<{ index: number; offerId: string }> = [];

    enriched.forEach((item, index) => {
      const fromStructuredTitle = this.parseTargetedOfferFromTitle(item.title);
      if (fromStructuredTitle) {
        item.targetedOfferTitle = fromStructuredTitle.offerTitle;
        item.targetedOfferCompany = fromStructuredTitle.offerCompany;
        return;
      }

      const legacyOfferId = this.extractLegacyOfferId(item.title);
      if (legacyOfferId) {
        item.targetedOfferId = legacyOfferId;
        unresolved.push({ index, offerId: legacyOfferId });
      }
    });

    if (unresolved.length === 0) return enriched;

    await Promise.all(
      unresolved.map(async ({ index, offerId }) => {
        try {
          const offer = await firstValueFrom(this.offerService.getOfferById(offerId));
          enriched[index].targetedOfferTitle = offer.titre;
          enriched[index].targetedOfferCompany = offer.entreprise ?? 'Entreprise non precisee';
        } catch {
          // keep fallback text
        }
      })
    );

    return enriched;
  }

  private parseTargetedOfferFromTitle(
    title: string
  ): { offerTitle: string; offerCompany: string } | null {
    const value = title?.trim();
    if (!value) return null;

    const match = value.match(/^CV\\s*-\\s*(.+?)\\s*-\\s*(.+)$/i);
    if (!match) return null;

    const offerTitle = match[1]?.trim();
    const offerCompany = match[2]?.trim();
    if (!offerTitle) return null;

    return {
      offerTitle,
      offerCompany: offerCompany || 'Entreprise non precisee',
    };
  }

  private extractLegacyOfferId(title: string): string | null {
    const value = title?.trim();
    if (!value) return null;

    const guidPattern =
      '[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}';
    const regex = new RegExp(`^CV[_\\-\\s]?(${guidPattern})$`, 'i');
    const match = value.match(regex);
    return match?.[1] ?? null;
  }
}
