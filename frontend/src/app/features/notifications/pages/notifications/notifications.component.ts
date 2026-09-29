import { Component, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

interface Notification {
  id: string;
  type: 'follow_up' | 'new_offer' | 'gmail_reply' | 'interview' | 'system';
  title: string;
  message: string;
  date: string;
  read: boolean;
  actionable: boolean;
  actionLabel?: string;
  relatedId?: string;
}

@Component({
  selector: 'app-notifications',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="notif-shell">
      <header class="page-header">
        <div class="header-left">
          <h1 class="page-title">Notifications</h1>
          <p class="page-subtitle">Alertes de relance, nouvelles offres, reponses email et rappels.</p>
        </div>
        <div class="header-actions">
          <button class="btn-mark-all" (click)="markAllRead()" [disabled]="unreadCount() === 0">
            Tout marquer comme lu
          </button>
        </div>
      </header>

      <div class="filter-bar">
        <button class="filter-btn" [class.active]="filter() === 'all'" (click)="filter.set('all')">Toutes ({{ notifications.length }})</button>
        <button class="filter-btn" [class.active]="filter() === 'unread'" (click)="filter.set('unread')">Non lues ({{ unreadCount() }})</button>
        <button class="filter-btn" [class.active]="filter() === 'follow_up'" (click)="filter.set('follow_up')">Relances</button>
        <button class="filter-btn" [class.active]="filter() === 'interview'" (click)="filter.set('interview')">Entretiens</button>
      </div>

      <div class="notif-list">
        @for (n of filteredNotifs(); track n.id) {
          <div class="notif-card" [class.unread]="!n.read" [class]="'type-' + n.type" (click)="markRead(n.id)">
            <div class="notif-icon">
              @if (n.type === 'follow_up') { <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="23 4 23 10 17 10"/><polyline points="1 20 1 14 7 14"/><path d="M3.51 9a9 9 0 0 1 14.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0 0 20.49 15"/></svg> }
              @else if (n.type === 'new_offer') { <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="2" y="7" width="20" height="14" rx="2" ry="2"/><path d="M16 21V5a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v16"/></svg> }
              @else if (n.type === 'gmail_reply') { <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z"/><polyline points="22,6 12,13 2,6"/></svg> }
              @else if (n.type === 'interview') { <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg> }
              @else { <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><line x1="12" y1="16" x2="12.01" y2="16"/><line x1="12" y1="8" x2="12" y2="12"/></svg> }
            </div>
            <div class="notif-body">
              <div class="notif-header-row">
                <h4>{{ n.title }}</h4>
                <span class="notif-date">{{ getRelativeDate(n.date) }}</span>
              </div>
              <p>{{ n.message }}</p>
              @if (n.actionable && n.actionLabel) {
                <button class="notif-action" (click)="$event.stopPropagation()">{{ n.actionLabel }}</button>
              }
            </div>
            @if (!n.read) { <div class="unread-dot"></div> }
          </div>
        } @empty {
          <div class="empty-state">
            <div class="empty-icon"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9"/><path d="M10.3 21a1.94 1.94 0 0 0 3.4 0"/></svg></div>
            <h3>Aucune notification</h3>
            <p>Vous recevrez des alertes de relance et des mises a jour ici.</p>
          </div>
        }
      </div>
    </div>
  `,
  styles: [`
    :host { display: flex; flex-direction: column; flex: 1; height: 100%; min-height: 0; overflow-y: auto; }
    .notif-shell { display: flex; flex-direction: column; padding: 24px 40px; gap: 20px; }
    .page-header { display: flex; justify-content: space-between; align-items: flex-start; }
    .header-left { display: flex; flex-direction: column; gap: 4px; }
    .page-title { font-size: 24px; font-weight: 700; color: #212121; margin: 0; font-family: 'Lato', sans-serif; }
    .page-subtitle { font-size: 14px; color: #616161; margin: 0; }
    .btn-mark-all { padding: 8px 16px; border: 1px solid #E0E0E0; border-radius: 8px; background: white; font-size: 13px; font-weight: 600; cursor: pointer; &:hover { border-color: #465FFF; } &:disabled { opacity: 0.4; cursor: not-allowed; } }

    .filter-bar { display: flex; gap: 8px; padding: 4px; background: #F5F5F5; border-radius: 8px; width: fit-content; }
    .filter-btn { padding: 6px 14px; border: none; background: transparent; border-radius: 6px; font-size: 13px; font-weight: 600; color: #616161; cursor: pointer; transition: all 0.2s; &.active { background: white; color: #465FFF; box-shadow: 0 1px 3px rgba(0,0,0,0.1); } }

    .notif-list { display: flex; flex-direction: column; gap: 8px; }
    .notif-card { display: flex; gap: 16px; background: white; border: 1px solid #E0E0E0; border-radius: 12px; padding: 16px; cursor: pointer; transition: all 0.2s; position: relative; &.unread { border-left: 3px solid #465FFF; background: #ecf3ff; } &:hover { border-color: #465FFF; } }
    .notif-icon { width: 40px; height: 40px; border-radius: 10px; display: flex; align-items: center; justify-content: center; flex-shrink: 0; svg { width: 20px; height: 20px; } .type-follow_up & { background: #FFF8E6; color: #F59B00; } .type-new_offer & { background: #ecf3ff; color: #465FFF; } .type-gmail_reply & { background: #E6F4EA; color: #34A853; } .type-interview & { background: #F0E6FF; color: #651FFF; } .type-system & { background: #F1F5F9; color: #475569; } }
    .notif-body { flex: 1; h4 { margin: 0 0 4px; font-size: 14px; font-weight: 600; color: #212121; } p { margin: 0; font-size: 13px; color: #616161; line-height: 1.5; } }
    .notif-header-row { display: flex; justify-content: space-between; align-items: center; }
    .notif-date { font-size: 11px; color: #9E9E9E; }
    .notif-action { margin-top: 8px; padding: 6px 14px; background: #465FFF; color: white; border: none; border-radius: 6px; font-size: 12px; font-weight: 600; cursor: pointer; &:hover { background: #3641F5; } }
    .unread-dot { width: 8px; height: 8px; background: #465FFF; border-radius: 50%; position: absolute; top: 16px; right: 16px; }

    .empty-state { display: flex; flex-direction: column; align-items: center; justify-content: center; padding: 60px; .empty-icon { width: 64px; height: 64px; background: #F1F5F9; border-radius: 50%; display: flex; align-items: center; justify-content: center; color: #94A3B8; margin-bottom: 16px; svg { width: 32px; height: 32px; } } h3 { margin: 0 0 8px; font-size: 18px; color: #212121; } p { margin: 0; font-size: 14px; color: #616161; } }
  `]
})
export class NotificationsComponent {
  filter = signal<'all' | 'unread' | 'follow_up' | 'interview'>('all');

  notifications: Notification[] = [
    { id: '1', type: 'follow_up', title: 'Relance J+7 - Capgemini', message: 'Votre candidature chez Capgemini pour le poste Full Stack Stage n\'a pas eu de reponse depuis 7 jours. Envoyez une relance ?', date: '2026-05-10T09:00:00', read: false, actionable: true, actionLabel: 'Generer la relance' },
    { id: '2', type: 'follow_up', title: 'Relance J+7 - CGI', message: 'Votre candidature chez CGI pour le poste Developpeur Java Spring approche de J+7.', date: '2026-05-09T14:00:00', read: false, actionable: true, actionLabel: 'Generer la relance' },
    { id: '3', type: 'interview', title: 'Entretien planifie - OCP', message: 'Vous avez un entretien avec OCP Group le 15 mai a 14h00 pour le poste DevOps Stage.', date: '2026-05-08T10:00:00', read: false, actionable: true, actionLabel: 'Voir les details' },
    { id: '4', type: 'gmail_reply', title: 'Nouvelle reponse - Maroc Telecom', message: 'Maroc Telecom a repondu a votre candidature Data Engineer : "Nous souhaitons vous rencontrer."', date: '2026-05-07T16:30:00', read: true, actionable: false },
    { id: '5', type: 'new_offer', title: 'Nouvelle offre recommandee', message: 'Une nouvelle offre "Cloud Architect" chez OCP Group correspond a votre profil.', date: '2026-05-06T08:00:00', read: true, actionable: true, actionLabel: 'Voir l\'offre' },
    { id: '6', type: 'system', title: 'Profil complete a 85%', message: 'Ajoutez vos certifications pour atteindre 100% de completion et maximiser vos chances.', date: '2026-05-05T12:00:00', read: true, actionable: false },
    { id: '7', type: 'interview', title: 'Preparation entretien - Capgemini', message: 'Votre entretien approche ! Entrainez-vous avec notre simulateur IA.', date: '2026-05-04T09:00:00', read: false, actionable: true, actionLabel: 'Simuler l\'entretien' },
  ];

  unreadCount = computed(() => this.notifications.filter(n => !n.read).length);

  filteredNotifs = computed(() => {
    const f = this.filter();
    return f === 'all' ? this.notifications : f === 'unread' ? this.notifications.filter(n => !n.read) : this.notifications.filter(n => n.type === f);
  });

  markRead(id: string) {
    const n = this.notifications.find(n => n.id === id);
    if (n) n.read = true;
  }

  markAllRead() {
    this.notifications.forEach(n => n.read = true);
  }

  getRelativeDate(dateStr: string): string {
    const d = new Date(dateStr);
    const now = new Date();
    const diff = now.getTime() - d.getTime();
    const days = Math.floor(diff / (1000 * 60 * 60 * 24));
    if (days === 0) return "Aujourd'hui";
    if (days === 1) return 'Hier';
    if (days < 7) return `Il y a ${days} jours`;
    return d.toLocaleDateString('fr-FR');
  }
}
