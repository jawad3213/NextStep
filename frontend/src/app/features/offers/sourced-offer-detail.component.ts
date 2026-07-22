import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { OfferApiService, SourcedOfferDetailDto } from './services/offer-api.service';

@Component({
  selector: 'app-sourced-offer-detail',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './sourced-offer-detail.component.html',
  styleUrl: './offer-detail.component.scss',
})
export class SourcedOfferDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly offerApi = inject(OfferApiService);

  readonly offer = signal<SourcedOfferDetailDto | null>(null);
  readonly isLoading = signal(true);
  readonly actionBusy = signal(false);
  readonly errorMessage = signal('');

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.isLoading.set(false);
      return;
    }

    this.offerApi.getSourcedOffer(id).subscribe({
      next: (offer) => {
        this.offer.set(offer);
        this.isLoading.set(false);
      },
      error: () => {
        this.errorMessage.set('Offre sourcee introuvable.');
        this.isLoading.set(false);
      },
    });
  }

  goBack(): void {
    this.router.navigate(['/offers-recent']);
  }

  openSimilar(id: string): void {
    this.router.navigate(['/offers-recent', id]);
  }

  toggleSaved(): void {
    const offer = this.offer();
    if (!offer || this.actionBusy()) return;
    this.patch({ isSaved: !offer.isSaved });
  }

  toggleShortlisted(): void {
    const offer = this.offer();
    if (!offer || this.actionBusy()) return;
    this.patch({ isShortlisted: !offer.isShortlisted, isSaved: true });
  }

  toggleArchived(): void {
    const offer = this.offer();
    if (!offer || this.actionBusy()) return;
    this.patch({ isArchived: !offer.isArchived });
  }

  analyze(): void {
    const offer = this.offer();
    if (!offer || this.actionBusy()) return;

    this.actionBusy.set(true);
    this.offerApi.promoteSourcedOffer(offer.id).subscribe({
      next: (promotion) => {
        this.actionBusy.set(false);
        this.router.navigate(['/offers/analyze'], {
          queryParams: {
            offerId: promotion.offerId,
            autoAnalyze: 1,
          },
        });
      },
      error: (err) => {
        this.actionBusy.set(false);
        this.errorMessage.set(err?.error?.error || err?.error?.detail || 'Impossible de promouvoir l offre sourcee.');
      },
    });
  }

  getBubbleGradient(company: string): string {
    const colors = [
      'linear-gradient(135deg, #465FFF 0%, #252dae 100%)',
      'linear-gradient(135deg, #00B0FF 0%, #0091EA 100%)',
      'linear-gradient(135deg, #00E676 0%, #00A250 100%)',
      'linear-gradient(135deg, #FF9100 0%, #FF6D00 100%)',
      'linear-gradient(135deg, #651FFF 0%, #4615B2 100%)',
      'linear-gradient(135deg, #D500F9 0%, #9C00AF 100%)',
    ];
    let sum = 0;
    for (let i = 0; i < company.length; i++) sum += company.charCodeAt(i);
    return colors[sum % colors.length];
  }

  getInitials(value: string): string {
    const parts = value.split(' ').filter(Boolean);
    if (parts.length === 0) return 'NS';
    if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
    return `${parts[0][0]}${parts[1][0]}`.toUpperCase();
  }

  getRankingTags(offer: SourcedOfferDetailDto): string[] {
    return offer.aiMatchedSkills?.length ? offer.aiMatchedSkills : offer.matchedItTerms;
  }

  getAiReasons(offer: SourcedOfferDetailDto): string[] {
    return offer.aiReasons ?? [];
  }

  getMissingSkills(offer: SourcedOfferDetailDto): string[] {
    return offer.aiMissingSkills ?? [];
  }

  private patch(payload: { isSaved?: boolean; isShortlisted?: boolean; isArchived?: boolean }): void {
    const offer = this.offer();
    if (!offer) return;
    this.actionBusy.set(true);
    this.offerApi.updateSourcedOffer(offer.id, payload).subscribe({
      next: (updated) => {
        this.offer.set({ ...updated, similarOffers: offer.similarOffers, sourceQuery: offer.sourceQuery });
        this.actionBusy.set(false);
      },
      error: (err) => {
        this.actionBusy.set(false);
        this.errorMessage.set(err?.error?.error || err?.error?.detail || 'Mise a jour impossible.');
      },
    });
  }
}
