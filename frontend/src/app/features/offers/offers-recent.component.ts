import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { trigger, transition, style, animate, query, stagger } from '@angular/animations';
import { MOCK_OFFERS, OfferCard } from './offers-data';

@Component({
  selector: 'app-offers-recent',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './offers-recent.component.html',
  styleUrl: './offers.component.scss',
  animations: [
    trigger('cardAnimation', [
      transition('* => *', [
        query(':enter', [
          style({ opacity: 0, transform: 'translateY(12px)' }),
          stagger(60, [
            animate('300ms ease-out', style({ opacity: 1, transform: 'translateY(0)' }))
          ])
        ], { optional: true })
      ])
    ])
  ]
})
export class OffersRecentComponent {
  readonly offers: OfferCard[] = MOCK_OFFERS;

  readonly searchTerm = signal('');
  readonly filterContract = signal('');
  readonly filterStatus = signal('');
  readonly sortBy = signal<'date' | 'score' | 'company' | 'daysLeft'>('date');
  readonly viewMode = signal<'list' | 'grid'>('list');

  readonly pageSize = signal(6);
  readonly currentPage = signal(1);

  get filteredOffers(): OfferCard[] {
    const search = this.searchTerm().trim().toLowerCase();
    const contract = this.filterContract();
    const status = this.filterStatus();
    const sort = this.sortBy();

    let result = this.offers.filter(o => {
      const matchSearch = !search ||
        o.title.toLowerCase().includes(search) ||
        o.company.toLowerCase().includes(search) ||
        o.location.toLowerCase().includes(search) ||
        o.tags.some(t => t.toLowerCase().includes(search));

      const matchContract = !contract || o.tags.some(t => t.toLowerCase().includes(contract.toLowerCase()));
      const matchStatus = !status || o.status === status;

      return matchSearch && matchContract && matchStatus;
    });

    result.sort((a, b) => {
      switch (sort) {
        case 'score': return (b.matchingScore ?? 0) - (a.matchingScore ?? 0);
        case 'company': return a.company.localeCompare(b.company);
        case 'daysLeft': return (a.daysLeft ?? 0) - (b.daysLeft ?? 0);
        case 'date': return new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime();
      }
    });

    return result;
  }

  get paginatedOffers(): OfferCard[] {
    return this.filteredOffers.slice(0, this.currentPage() * this.pageSize());
  }

  get hasMore(): boolean {
    return this.paginatedOffers.length < this.filteredOffers.length;
  }

  onSearchInput(value: string): void {
    this.searchTerm.set(value);
    this.currentPage.set(1);
  }

  onSortChange(value: string): void {
    this.sortBy.set(value as 'date' | 'score' | 'company' | 'daysLeft');
    this.currentPage.set(1);
  }

  onFilterContractChange(value: string): void {
    this.filterContract.set(value);
    this.currentPage.set(1);
  }

  onFilterStatusChange(value: string): void {
    this.filterStatus.set(value);
    this.currentPage.set(1);
  }

  loadMore(): void {
    this.currentPage.update(p => p + 1);
  }

  getRecencyLabel(date: Date): string {
    const now = new Date();
    const diff = now.getTime() - new Date(date).getTime();
    const days = Math.floor(diff / (1000 * 60 * 60 * 24));
    if (days === 0) return "Ajouté aujourd'hui";
    if (days === 1) return 'Ajouté hier';
    return `Ajouté il y a ${days} jours`;
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
}
