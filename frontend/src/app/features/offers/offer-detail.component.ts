import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { MOCK_OFFERS, OfferCard } from './offers-data';

@Component({
  selector: 'app-offer-detail',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './offer-detail.component.html',
  styleUrl: './offer-detail.component.scss'
})
export class OfferDetailComponent {
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  offer: OfferCard | undefined;

  constructor() {
    const id = this.route.snapshot.paramMap.get('id');
    this.offer = MOCK_OFFERS.find(o => o.id === id);
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
}
