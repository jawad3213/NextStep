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

interface PagedResponse<T> {
  offset: number;
  limit: number;
  total: number;
  hasMore: boolean;
  items: T[];
}

@Component({
  selector: 'app-cv-builder',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './cv-builder.component.html',
  styleUrl: './cv-builder.component.scss'
})
export class CvBuilderComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly offerService = inject(OfferService);
  private readonly baseUrl = environment.apiBaseUrl;

  readonly historyItems = signal<CvHistoryItem[]>([]);
  readonly loadingHistory = signal(false);
  readonly loadingMoreHistory = signal(false);
  readonly historyOffset = signal(0);
  readonly historyLimit = 10;
  readonly hasMoreHistory = signal(false);
  private useLegacyHistoryEndpoint = false;
  private legacyHistoryCache: CvHistoryItem[] = [];

  ngOnInit(): void {
    this.loadHistory();
  }

  async loadHistory(): Promise<void> {
    this.historyOffset.set(0);
    this.loadingHistory.set(true);
    this.useLegacyHistoryEndpoint = false;
    this.legacyHistoryCache = [];
    try {
      const page = await firstValueFrom(
        this.http.get<PagedResponse<CvHistoryItem>>(
          `${this.baseUrl}/cv/history/paged?offset=0&limit=${this.historyLimit}`
        )
      );
      const enriched = await this.enrichTargetOfferData(page.items);
      this.historyItems.set(enriched);
      this.historyOffset.set(enriched.length);
      this.hasMoreHistory.set(page.hasMore);
    } catch {
      await this.loadHistoryLegacyFallback();
    } finally {
      this.loadingHistory.set(false);
    }
  }

  async loadMoreHistory(): Promise<void> {
    if (this.loadingMoreHistory() || !this.hasMoreHistory()) return;
    this.loadingMoreHistory.set(true);
    try {
      if (this.useLegacyHistoryEndpoint) {
        const offset = this.historyOffset();
        const nextSlice = this.legacyHistoryCache.slice(offset, offset + this.historyLimit);
        this.historyItems.update((items) => [...items, ...nextSlice]);
        this.historyOffset.update((v) => v + nextSlice.length);
        this.hasMoreHistory.set(this.historyOffset() < this.legacyHistoryCache.length);
        return;
      }

      const offset = this.historyOffset();
      const page = await firstValueFrom(
        this.http.get<PagedResponse<CvHistoryItem>>(
          `${this.baseUrl}/cv/history/paged?offset=${offset}&limit=${this.historyLimit}`
        )
      );
      const enriched = await this.enrichTargetOfferData(page.items);
      this.historyItems.update((items) => [...items, ...enriched]);
      this.historyOffset.update((v) => v + enriched.length);
      this.hasMoreHistory.set(page.hasMore);
    } catch {
      if (!this.useLegacyHistoryEndpoint) {
        await this.loadHistoryLegacyFallback();
      }
    } finally {
      this.loadingMoreHistory.set(false);
    }
  }

  private async loadHistoryLegacyFallback(): Promise<void> {
    const all = await firstValueFrom(
      this.http.get<CvHistoryItem[]>(`${this.baseUrl}/cv/history`)
    );
    const enrichedAll = await this.enrichTargetOfferData(all);
    this.useLegacyHistoryEndpoint = true;
    this.legacyHistoryCache = enrichedAll;

    const initial = enrichedAll.slice(0, this.historyLimit);
    this.historyItems.set(initial);
    this.historyOffset.set(initial.length);
    this.hasMoreHistory.set(initial.length < enrichedAll.length);
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
