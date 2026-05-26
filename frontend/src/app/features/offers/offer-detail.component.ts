import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { OfferCard } from './offers-data';
import { OfferApiService } from './services/offer-api.service';

@Component({
  selector: 'app-offer-detail',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './offer-detail.component.html',
  styleUrl: './offer-detail.component.scss'
})
export class OfferDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private offerApi = inject(OfferApiService);

  offer: OfferCard | undefined;
  isLoading = signal(true);
  isDownloadingCv = signal(false);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.isLoading.set(false);
      return;
    }

    this.offerApi.getOffersHistory().subscribe({
      next: (items) => {
        const item = items.find((o) => o.offerId === id);
        this.offer = item
          ? {
              id: item.offerId,
              initials: this.getInitials(item.entreprise || item.titre),
              title: item.titre || 'Offre',
              company: (item.entreprise && item.entreprise !== 'null') ? item.entreprise : 'Entreprise',
              location: (item.localisation && item.localisation !== 'null') ? item.localisation : '',
              matchingScore: item.scoreMatching,
              tags: [],
              status: item.status,
              createdAt: new Date(item.dateCreation),
              currentStep: item.currentStep || 1,
            }
          : undefined;
        this.isLoading.set(false);
      },
      error: () => {
        this.offer = undefined;
        this.isLoading.set(false);
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

  getScoreClass(score: number): string {
    if (score >= 70) return 'text-green-600';
    if (score >= 50) return 'text-amber-500';
    return 'text-red-500';
  }

  getStatusConfig(status: OfferCard['status']): { bg: string; text: string; dot: string } {
    const map: Record<string, { bg: string; text: string; dot: string }> = {
      cv_genere: { bg: 'bg-green-50', text: 'text-green-700', dot: 'bg-green-500' },
      analysee: { bg: 'bg-blue-50', text: 'text-blue-600', dot: 'bg-blue-500' },
      non_traitee: { bg: 'bg-slate-100', text: 'text-slate-500', dot: 'bg-slate-400' },
    };
    const key = status as string;
    return map[key] || map['non_traitee'];
  }

  goBack(): void {
    this.router.navigate(['/offers']);
  }

  async viewCv(): Promise<void> {
    if (!this.offer?.id || this.isDownloadingCv()) return;
    this.router.navigate(['/offers/analyze'], {
      queryParams: {
        offerId: this.offer.id,
        step: 'results'
      }
    });
  }

  private getInitials(value: string): string {
    const parts = value.split(' ').filter(Boolean);
    if (parts.length === 0) return 'NS';
    if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
    return `${parts[0][0]}${parts[1][0]}`.toUpperCase();
  }
}
