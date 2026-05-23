
import { Component, signal, computed, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { CandidatureService, CandidatureDto } from '../../services/candidature.service';
import { OfferService } from '../../services/offer.service';

interface CandidatureCard {
  id: string;
  entreprise: string;
  role: string;
  type: string;
  statut: string;
  dateCreation: string;
  notes?: string;

}

@Component({
  selector: 'app-applications',
  standalone: true,

  imports: [CommonModule, FormsModule],
  templateUrl: './applications.component.html',
  styleUrl: './applications.component.scss'
})
export class ApplicationsComponent implements OnInit {
  private readonly candidatureService = inject(CandidatureService);
  private readonly offerService = inject(OfferService);
  filterStatut = signal('Tous');
  searchQuery = signal('');
  viewMode = signal<'kanban' | 'liste'>('kanban');
  showNewForm = signal(false);

  draggedCard = signal<CandidatureCard | null>(null);
  dragOverCol = signal<string | null>(null);

  newForm = signal({ entreprise: '', role: '', type: 'Stage', statut: 'envoye' });

  updateForm(key: string, value: string) {
    this.newForm.update(prev => ({ ...prev, [key]: value }));
  }

  cards = signal<CandidatureCard[]>([]);
  loading = signal<boolean>(true);
  error = signal<string | null>(null);

  ngOnInit() {
    this.load();
  }

  load() {
    this.loading.set(true);
    this.error.set(null);

    this.candidatureService.getMyCandidatures().subscribe({
      next: (candidatures) => {
        if (candidatures.length === 0) {
          this.cards.set([]);
          this.loading.set(false);
          return;
        }

        const requests = candidatures.map(c =>
          this.offerService.getOfferById(c.idOffre).pipe(
            catchError(() => of(null))
          )
        );

        forkJoin(requests).subscribe({
          next: (offers) => {
            const mappedCards: CandidatureCard[] = candidatures.map((c, i) => {
              const offer = offers[i];
              return {
                id: c.idCandidature,
                entreprise: offer?.entreprise || 'Entreprise Inconnue',
                role: offer?.titre || 'Poste Inconnu',
                type: 'CDI', // Defaulting as original API doesn't have contract type natively
                statut: this.mapStatusToKanban(c.statut),
                dateCreation: c.dateCreation,
              };
            });
            this.cards.set(mappedCards);
            this.loading.set(false);
          },
          error: () => {
            this.error.set('Impossible de charger les détails des offres.');
            this.loading.set(false);
          }
        });
      },
      error: () => {
        this.error.set('Impossible de charger vos candidatures.');
        this.loading.set(false);
      }
    });
  }

  mapStatusToKanban(backendStatus: string): string {
    switch (backendStatus) {
      case 'ENVOYE': return 'envoye';
      case 'ENTRETIEN_PROPOSE': return 'entretien';
      case 'ACCEPTE': return 'accepte';
      case 'REFUSE': return 'refuse';
      default: return 'en-attente';
    }
  }

  columns = [
    { key: 'envoye', label: 'Envoyé', color: '#465fff' },
    { key: 'en-attente', label: 'En attente', color: '#F59B00' },
    { key: 'entretien', label: 'Entretien', color: '#7c3aed' },
    { key: 'test-tech', label: 'Test Tech', color: '#757575' },
    { key: 'accepte', label: 'Accepté', color: '#34A853' },
    { key: 'refuse', label: 'Refusé', color: '#D93025' },
  ];

  filteredCards = computed(() => {
    const all = this.cards();
    const filter = this.filterStatut();
    const query = this.searchQuery().toLowerCase();
    return all.filter(c => {
      if (filter !== 'Tous' && c.statut !== filter) return false;
      if (query && !c.entreprise.toLowerCase().includes(query) && !c.role.toLowerCase().includes(query)) return false;
      return true;
    });
  });

  stats = computed(() => {
    const all = this.cards();
    return {
      total: all.length,
      envoyees: all.filter(c => c.statut === 'envoye').length,
      enAttente: all.filter(c => c.statut === 'en-attente').length,
      acceptees: all.filter(c => c.statut === 'accepte').length,
      tauxReponse: all.length > 0 ? Math.round((all.filter(c => c.statut !== 'envoye').length / all.length) * 100) : 0,
    };
  });

  getColumnCards(key: string) {
    return this.filteredCards().filter(c => c.statut === key);
  }

  setFilter(s: string) { this.filterStatut.set(s); }
  setView(v: 'kanban' | 'liste') { this.viewMode.set(v); }

  onDragStart(c: CandidatureCard) { this.draggedCard.set(c); }
  onDragOver(e: DragEvent, col: string) { e.preventDefault(); this.dragOverCol.set(col); }
  onDragLeave() { this.dragOverCol.set(null); }
  onDrop(e: DragEvent, col: string) {
    e.preventDefault();
    this.dragOverCol.set(null);
    const card = this.draggedCard();
    if (card && card.statut !== col) {
      // Optimistic update
      this.cards.update(list => list.map(c2 => c2.id === card.id ? { ...c2, statut: col } : c2));
      
      // We would ideally call the backend here to update the status
      // this.candidatureService.updateStatus(card.id, this.mapKanbanToBackend(col)).subscribe(...)
    }
    this.draggedCard.set(null);
  }

  createCandidature() {
    const f = this.newForm();
    if (!f.entreprise || !f.role) return;
    this.cards.update(list => [{
      id: crypto.randomUUID(),
      entreprise: f.entreprise,
      role: f.role,
      type: f.type,
      statut: f.statut,
      dateCreation: new Date().toISOString().slice(0, 10),
    }, ...list]);
    this.showNewForm.set(false);
    this.newForm.set({ entreprise: '', role: '', type: 'Stage', statut: 'envoye' });
  }

  getInitials(name: string): string {
    return name.split(' ').map(w => w[0]).join('').toUpperCase().slice(0, 2) || '?';
  }

  getTypeBadgeClass(type: string): string {
    const map: Record<string, string> = {
      'CDI': 'bg-brand-50 text-brand-600',
      'Stage PFA': 'bg-amber-50 text-amber-700',
      'Stage PFE': 'bg-orange-50 text-orange-700',
      'Stage': 'bg-gray-100 text-gray-600',
      'Freelance': 'bg-green-50 text-green-700',
      'CDD': 'bg-red-50 text-red-600',
      'Alternance': 'bg-blue-50 text-blue-600',
    };
    return map[type] || 'bg-gray-100 text-gray-500';
  }

  getCompanyColor(name: string): string {
    const colors = ['#465fff','#8f4900','#34A853','#F59B00','#D93025','#7d5700','#1A91F0','#b35e00'];
    let sum = 0; for (let i = 0; i < name.length; i++) sum += name.charCodeAt(i);
    return colors[sum % colors.length];
  }

  historyEntries = [
    { initial: 'S', company: 'Startup IA', role: 'Freelance DevOps', date: '12 Oct. 2023', issue: 'Refusé', issueClass: 'error' },
    { initial: 'M', company: 'MedTech Hub', role: 'Backend Dev', date: '05 Oct. 2023', issue: 'Retiré', issueClass: 'neutral' },
    { initial: 'A', company: 'Alten Maroc', role: 'Apprenti QA', date: '22 Sep. 2023', issue: 'Accepté', issueClass: 'success' },
  ];

  performanceCards = [
    { icon: 'analytics', iconBg: 'bg-brand-50', iconColor: 'text-brand-500', label: 'Taux de Conversion', value: '15%', badge: '+2%', badgeIcon: true, badgeColor: 'text-green-600', urgent: false },
    { icon: 'timer', iconBg: 'bg-purple-50', iconColor: 'text-purple-600', label: 'Temps Moyen', value: '4 jours', badge: 'Stabilité', badgeIcon: false, badgeColor: 'text-gray-500', urgent: false },
    { icon: 'forum', iconBg: 'bg-green-50', iconColor: 'text-green-600', label: 'Entretiens', value: '3', sub: 'cette semaine', badge: 'Actif', badgeIcon: false, badgeColor: 'text-brand-600', urgent: false },
    { icon: 'priority_high', iconBg: 'bg-red-50', iconColor: 'text-red-600', label: 'Relances', value: '2', sub: 'critiques', badge: 'Urgent', badgeIcon: false, badgeColor: 'text-red-600', urgent: true },
  ];

}
