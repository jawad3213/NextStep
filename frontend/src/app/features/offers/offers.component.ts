import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { trigger, transition, style, animate, query, stagger } from '@angular/animations';
import { ANALYSIS_STEPS, MOCK_OFFERS, OfferCard } from './offers-data';
import { OfferApiService } from './services/offer-api.service';

@Component({
  selector: 'app-offers',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './offers.component.html',
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
export class OffersComponent implements OnInit {
  readonly steps = ANALYSIS_STEPS;
  private readonly offerApi = inject(OfferApiService);
  readonly offers = signal<OfferCard[]>([]);

  readonly searchTerm = signal('');
  readonly filterStatus = signal('');
  readonly sortBy = signal<'date' | 'score' | 'company' | 'step'>('date');
  readonly currentPage = signal(1);
  readonly pageSize = signal(10);
  readonly selectedOfferIds = signal<Set<string>>(new Set<string>());
  readonly isDeleting = signal(false);
  readonly showDeleteConfirm = signal(false);

  ngOnInit(): void {
    this.offerApi.getOffersHistory().subscribe({
      next: (items) => {
        this.offers.set(items.map((o) => ({
          id: o.offerId,
          initials: this.getInitials(o.entreprise || o.titre),
          title: o.titre || 'Offre',
          company: (o.entreprise && o.entreprise !== 'null') ? o.entreprise : 'Entreprise',
          location: (o.localisation && o.localisation !== 'null') ? o.localisation : '',
          matchingScore: o.scoreMatching,
          tags: [],
          status: o.status,
          createdAt: new Date(o.dateCreation),
          currentStep: o.currentStep || 1,
        })));
        this.selectedOfferIds.set(new Set<string>());
      },
      error: () => this.offers.set(MOCK_OFFERS)
    });
  }

  get filteredOffers(): OfferCard[] {
    const search = this.searchTerm().trim().toLowerCase();
    const status = this.filterStatus();
    const sort = this.sortBy();

    let result = this.offers().filter(o => {
      const matchSearch = !search ||
        o.title.toLowerCase().includes(search) ||
        o.company.toLowerCase().includes(search) ||
        o.location.toLowerCase().includes(search) ||
        o.tags.some(t => t.toLowerCase().includes(search));
      const matchStatus = !status || o.status === status;
      return matchSearch && matchStatus;
    });

    result.sort((a, b) => {
      switch (sort) {
        case 'score': return (b.matchingScore ?? 0) - (a.matchingScore ?? 0);
        case 'company': return a.company.localeCompare(b.company);
        case 'step': return (b.currentStep ?? 0) - (a.currentStep ?? 0);
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

  get allVisibleSelected(): boolean {
    const visible = this.paginatedOffers;
    if (visible.length === 0) return false;
    const selected = this.selectedOfferIds();
    return visible.every((o) => selected.has(o.id));
  }

  get selectedCount(): number {
    return this.selectedOfferIds().size;
  }

  onSearchInput(value: string): void {
    this.searchTerm.set(value);
    this.currentPage.set(1);
  }

  onSortChange(value: string): void {
    this.sortBy.set(value as 'date' | 'score' | 'company' | 'step');
    this.currentPage.set(1);
  }

  onFilterStatusChange(value: string): void {
    this.filterStatus.set(value);
    this.currentPage.set(1);
  }

  loadMore(): void {
    this.currentPage.update(p => p + 1);
  }

  toggleOfferSelection(offerId: string, checked: boolean): void {
    const next = new Set(this.selectedOfferIds());
    if (checked) next.add(offerId);
    else next.delete(offerId);
    this.selectedOfferIds.set(next);
  }

  toggleSelectAllVisible(checked: boolean): void {
    const next = new Set(this.selectedOfferIds());
    if (checked) {
      for (const offer of this.paginatedOffers) next.add(offer.id);
    } else {
      for (const offer of this.paginatedOffers) next.delete(offer.id);
    }
    this.selectedOfferIds.set(next);
  }

  openDeleteConfirm(): void {
    if (this.selectedCount === 0 || this.isDeleting()) return;
    this.showDeleteConfirm.set(true);
  }

  cancelDeleteConfirm(): void {
    if (this.isDeleting()) return;
    this.showDeleteConfirm.set(false);
  }

  bulkDeleteSelected(): void {
    const ids = [...this.selectedOfferIds()];
    if (ids.length === 0) return;
    this.isDeleting.set(true);
    this.offerApi.bulkDeleteOffers(ids).subscribe({
      next: () => {
        this.offers.update((list) => list.filter((o) => !this.selectedOfferIds().has(o.id)));
        this.selectedOfferIds.set(new Set<string>());
        this.isDeleting.set(false);
        this.showDeleteConfirm.set(false);
      },
      error: (err) => {
        this.isDeleting.set(false);
        const msg = err?.error?.message || err?.error?.error || 'Suppression impossible pour le moment.';
        alert(msg);
      },
    });
  }

  getStatusLabel(status: OfferCard['status']): string {
    switch (status) {
      case 'cv_genere': return 'CV Généré';
      case 'analysee': return 'Analysée';
      case 'non_traitee': return 'Non traitée';
    }
  }

  getStatusColor(status: OfferCard['status']): string {
    switch (status) {
      case 'cv_genere': return 'bg-green-100 text-green-700';
      case 'analysee': return 'bg-blue-100 text-blue-600';
      case 'non_traitee': return 'bg-slate-100 text-slate-500';
    }
  }

  getNextAction(offer: OfferCard): { label: string; link: string; offerId?: string } | null {
    if (offer.expired) return null;
    if (offer.currentStep <= 4) return { label: 'Continuer', link: '/offers/analyze', offerId: offer.id };
    return { label: 'Voir les résultats', link: `/offers/${offer.id}` };
  }

  getRecencyLabel(date: Date): string {
    const now = new Date();
    const diff = now.getTime() - new Date(date).getTime();
    const days = Math.floor(diff / (1000 * 60 * 60 * 24));
    if (days === 0) return "Aujourd'hui";
    if (days === 1) return 'Hier';
    if (days < 7) return `Il y a ${days} jours`;
    const weeks = Math.floor(days / 7);
    if (weeks === 1) return 'Il y a 1 semaine';
    return `Il y a ${weeks} semaines`;
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

  private getInitials(value: string): string {
    const parts = value.split(' ').filter(Boolean);
    if (parts.length === 0) return 'NS';
    if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
    return `${parts[0][0]}${parts[1][0]}`.toUpperCase();
  }
}
