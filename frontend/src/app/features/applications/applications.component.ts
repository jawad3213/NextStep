import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { forkJoin, catchError, of } from 'rxjs';
import { CandidatureService, CandidatureDto } from '../../services/candidature.service';
import { EmailService, EmailDraftDto } from '../../services/email.service';
import { OfferService, OfferDto } from '../../services/offer.service';

interface CandidatureRow {
  candidature: CandidatureDto;
  offer: OfferDto | null;
  latestDraft: EmailDraftDto | null;
}

@Component({
  selector: 'app-applications',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './applications.component.html',
  styleUrl: './applications.component.scss'
})
export class ApplicationsComponent implements OnInit {
  private readonly candidatureService = inject(CandidatureService);
  private readonly emailService = inject(EmailService);
  private readonly offerService = inject(OfferService);
  private readonly router = inject(Router);

  loading = signal(true);
  error = signal<string | null>(null);
  rows = signal<CandidatureRow[]>([]);

  ngOnInit() {
    this.load();
  }

  load() {
    this.loading.set(true);
    this.error.set(null);

    this.candidatureService.getMyCandidatures().subscribe({
      next: (candidatures) => {
        if (candidatures.length === 0) {
          this.rows.set([]);
          this.loading.set(false);
          return;
        }

        // Load offers and drafts in parallel for each candidature
        const requests = candidatures.map(c =>
          forkJoin({
            offer: this.offerService.getOfferById(c.idOffre).pipe(
              catchError(() => of(null)) // Handle case where offer analysis doesn't exist
            ),
            drafts: this.emailService.getDraftsByCandidature(c.idCandidature).pipe(
              catchError(() => of([])) // Handle case where draft retrieval fails
            )
          })
        );

        forkJoin(requests).subscribe({
          next: (results) => {
            const rows: CandidatureRow[] = candidatures.map((c, i) => ({
              candidature: c,
              offer: results[i].offer,
              latestDraft: results[i].drafts.length > 0
                ? results[i].drafts.sort((a, b) =>
                    new Date(b.createdAtUtc).getTime() - new Date(a.createdAtUtc).getTime()
                  )[0]
                : null
            }));
            this.rows.set(rows);
            this.loading.set(false);
          },
          error: (err) => {
            this.error.set('Impossible de charger les données.');
            this.loading.set(false);
          }
        });
      },
      error: (err) => {
        this.error.set('Impossible de charger vos candidatures.');
        this.loading.set(false);
      }
    });
  }

  navigateToEmail(candidatureId: string) {
    this.router.navigate(['/candidatures', candidatureId, 'email']);
  }

  navigateToOffers() {
    this.router.navigate(['/offers']);
  }

  getDraftStatusLabel(draft: EmailDraftDto | null): string {
    if (!draft) return 'Aucun brouillon';
    if (draft.isSent) return 'Envoyé';
    if (draft.isApproved) return 'Approuvé';
    return 'Brouillon';
  }

  getDraftStatusClass(draft: EmailDraftDto | null): string {
    if (!draft) return 'status-none';
    if (draft.isSent) return 'status-sent';
    if (draft.isApproved) return 'status-approved';
    return 'status-draft';
  }

  getCandidatureStatusClass(statut: string): string {
    switch (statut) {
      case 'ENVOYE': return 'status-sent';
      case 'VU': return 'status-approved';
      case 'ENTRETIEN': return 'status-info';
      default: return 'status-none';
    }
  }

  formatDate(dateStr: string | null): string {
    if (!dateStr) return '—';
    return new Date(dateStr).toLocaleDateString('fr-FR', { day: '2-digit', month: 'short', year: 'numeric' });
  }
}
