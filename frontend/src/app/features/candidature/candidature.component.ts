import { Component, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';

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
  selector: 'app-candidature',
  standalone: true,
  imports: [CommonModule, FormsModule, MatIconModule],
  templateUrl: './candidature.component.html',
  styleUrl: './candidature.component.scss'
})
export class CandidatureComponent {
  filterStatut = signal('Tous');
  searchQuery = signal('');
  viewMode = signal<'kanban' | 'liste'>('kanban');
  showNewForm = signal(false);

  draggedCard = signal<CandidatureCard | null>(null);
  dragOverCol = signal<string | null>(null);

  newForm = signal({ entreprise: '', role: '', type: 'Stage', statut: 'envoye' });

  updateFormField(field: string, value: string) {
    this.newForm.update(f => ({ ...f, [field]: value }));
  }

  cards = signal<CandidatureCard[]>([
    { id: '1', entreprise: 'Maroc Telecom', role: 'Ingénieur Cloud', type: 'CDI', statut: 'envoye', dateCreation: '2026-05-10' },
    { id: '2', entreprise: 'OCP Group', role: 'Data Analyst', type: 'Stage PFA', statut: 'en-attente', dateCreation: '2026-05-08' },
    { id: '3', entreprise: 'Capgemini', role: 'Développeur Fullstack', type: 'Stage PFE', statut: 'entretien', dateCreation: '2026-05-05' },
    { id: '4', entreprise: 'Lydec', role: 'Administrateur Réseaux', type: 'Stage', statut: 'test-tech', dateCreation: '2026-05-03' },
    { id: '5', entreprise: 'Intelcia', role: 'UX/UI Designer', type: 'Stage', statut: 'accepte', dateCreation: '2026-04-28' },
    { id: '6', entreprise: 'Startup IA', role: 'Freelance DevOps', type: 'Freelance', statut: 'refuse', dateCreation: '2026-04-20' },
  ]);

  columns = [
    { key: 'envoye', label: 'Envoyé', color: 'var(--primary)' },
    { key: 'en-attente', label: 'En attente', color: 'var(--warning)' },
    { key: 'entretien', label: 'Entretien', color: 'var(--tertiary)' },
    { key: 'test-tech', label: 'Test Tech', color: 'var(--outline)' },
    { key: 'accepte', label: 'Accepté', color: 'var(--success)' },
    { key: 'refuse', label: 'Refusé', color: 'var(--error)' },
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
    if (card) {
      this.cards.update(list => list.map(c2 => c2.id === card.id ? { ...c2, statut: col } : c2));
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
      'CDI': 'badge-cdi',
      'Stage PFA': 'badge-pfa',
      'Stage PFE': 'badge-pfe',
      'Stage': 'badge-stage',
      'Freelance': 'badge-freelance',
      'CDD': 'badge-cdd',
      'Alternance': 'badge-alternance',
    };
    return map[type] || 'badge-default';
  }

  getCompanyColor(name: string): string {
    const colors = ['#005ea1','#8f4900','#34A853','#F59B00','#D93025','#7d5700','#0078ca','#b35e00'];
    let sum = 0; for (let i = 0; i < name.length; i++) sum += name.charCodeAt(i);
    return colors[sum % colors.length];
  }

  historyEntries = [
    { initial: 'S', company: 'Startup IA', role: 'Freelance DevOps', date: '12 Oct. 2023', issue: 'Refusé', issueClass: 'error' },
    { initial: 'M', company: 'MedTech Hub', role: 'Backend Dev', date: '05 Oct. 2023', issue: 'Retiré', issueClass: 'neutral' },
    { initial: 'A', company: 'Alten Maroc', role: 'Apprenti QA', date: '22 Sep. 2023', issue: 'Accepté', issueClass: 'success' },
  ];

  performanceCards = [
    { icon: 'analytics', iconBg: 'primary-tint', iconColor: 'primary', label: 'Taux de Conversion', value: '15%', badge: '+2%', badgeIcon: 'trending_up', badgeColor: 'success' },
    { icon: 'timer', iconBg: 'tertiary-fixed', iconColor: 'tertiary', label: 'Temps Moyen', value: '4 jours', badge: 'Stabilité', badgeColor: 'on-surface-variant' },
    { icon: 'forum', iconBg: 'success-tint', iconColor: 'success', label: 'Entretiens', value: '3', sub: 'cette semaine', badge: 'Actif', badgeColor: 'primary' },
    { icon: 'priority_high', iconBg: 'error-tint', iconColor: 'error', label: 'Relances', value: '2', sub: 'critiques', badge: 'Urgent', badgeColor: 'error', urgent: true },
  ];
}
