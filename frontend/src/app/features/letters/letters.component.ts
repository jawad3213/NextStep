import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { catchError, forkJoin, of } from 'rxjs';
import { CandidatureService } from '../../services/candidature.service';
import { OfferService } from '../../services/offer.service';
import { EmailDraftDto, EmailService } from '../../services/email.service';

type DraftRow = {
  candidatureId: string;
  company: string;
  role: string;
  draftCount: number;
  sentCount: number;
  lastDraftAt: string | null;
};

type FlatDraft = EmailDraftDto & {
  company: string;
  role: string;
};

@Component({
  selector: 'app-letters',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './letters.component.html',
  styleUrl: './letters.component.scss'
})
export class LettersComponent implements OnInit {
  private readonly candidatureService = inject(CandidatureService);
  private readonly offerService = inject(OfferService);
  private readonly emailService = inject(EmailService);
  private readonly router = inject(Router);

  readonly loading = signal<boolean>(true);
  readonly loadingMore = signal<boolean>(false);
  readonly hasMoreCandidatures = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly rows = signal<DraftRow[]>([]);
  readonly allDrafts = signal<FlatDraft[]>([]);
  readonly recentDrafts = computed(() => this.allDrafts().slice(0, 8));
  private offset = 0;
  private readonly pageSize = 10;

  ngOnInit(): void {
    this.load(true);
  }

  load(reset = false): void {
    if (reset) {
      this.offset = 0;
      this.rows.set([]);
      this.allDrafts.set([]);
      this.loading.set(true);
    } else {
      this.loadingMore.set(true);
    }
    this.error.set(null);

    this.candidatureService.getMyCandidaturesPaged(this.offset, this.pageSize).subscribe({
      next: (page) => {
        const candidatures = page.items;
        if (candidatures.length === 0 && reset) {
          this.loading.set(false);
          this.loadingMore.set(false);
          this.hasMoreCandidatures.set(false);
          return;
        }

        const perCandidature = candidatures.map((c) =>
          forkJoin({
            offer: this.offerService.getOfferById(c.idOffre).pipe(catchError(() => of(null))),
            drafts: this.emailService.getDraftsByCandidature(c.idCandidature).pipe(
              catchError(() => of([] as EmailDraftDto[]))
            ),
          }).pipe(
            catchError(() =>
              of({
                offer: null,
                drafts: [] as EmailDraftDto[],
              })
            )
          )
        );

        forkJoin(perCandidature).subscribe({
          next: (results) => {
            const nextRows: DraftRow[] = [];
            const nextDrafts: FlatDraft[] = [];

            results.forEach((result, index) => {
              const cand = candidatures[index];
              const company = result.offer?.entreprise ?? 'Entreprise inconnue';
              const role = result.offer?.titre ?? 'Poste inconnu';
              const drafts = [...result.drafts].sort(
                (a, b) => new Date(b.createdAtUtc).getTime() - new Date(a.createdAtUtc).getTime()
              );

              nextRows.push({
                candidatureId: cand.idCandidature,
                company,
                role,
                draftCount: drafts.length,
                sentCount: drafts.filter((d) => d.isSent).length,
                lastDraftAt: drafts[0]?.createdAtUtc ?? null,
              });

              drafts.forEach((d) => nextDrafts.push({ ...d, company, role }));
            });

            nextRows.sort((a, b) => {
              const aTime = a.lastDraftAt ? new Date(a.lastDraftAt).getTime() : 0;
              const bTime = b.lastDraftAt ? new Date(b.lastDraftAt).getTime() : 0;
              return bTime - aTime;
            });

            nextDrafts.sort(
              (a, b) => new Date(b.createdAtUtc).getTime() - new Date(a.createdAtUtc).getTime()
            );

            this.rows.update((rows) => reset ? nextRows : [...rows, ...nextRows]);
            this.allDrafts.update((drafts) => {
              const merged = reset ? [...nextDrafts] : [...drafts, ...nextDrafts];
              return merged.sort(
                (a, b) => new Date(b.createdAtUtc).getTime() - new Date(a.createdAtUtc).getTime()
              );
            });
            this.offset += nextRows.length;
            this.hasMoreCandidatures.set(page.hasMore);
            this.loading.set(false);
            this.loadingMore.set(false);
          },
          error: () => {
            this.error.set('Impossible de charger les drafts email.');
            this.loading.set(false);
            this.loadingMore.set(false);
          },
        });
      },
      error: () => {
        this.error.set('Impossible de charger les candidatures.');
        this.loading.set(false);
        this.loadingMore.set(false);
      },
    });
  }

  loadMore(): void {
    if (this.loadingMore() || !this.hasMoreCandidatures()) return;
    this.load(false);
  }

  openWorkspace(candidatureId: string): void {
    this.router.navigate(['/letters', candidatureId], {
      queryParams: { source: 'letters' },
    });
  }

  formatDate(value: string | null): string {
    if (!value) return '-';
    return new Date(value).toLocaleString('fr-FR', {
      day: '2-digit',
      month: 'short',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  }

  getRowStatusClass(row: DraftRow): string {
    if (row.draftCount === 0) return 'pill-empty';
    if (row.sentCount >= row.draftCount) return 'pill-sent';
    if (row.sentCount === 0) return 'pill-draft';
    return 'pill-approved';
  }

  getRowCardClass(row: DraftRow): string {
    if (row.draftCount === 0) return 'status-empty';
    if (row.sentCount >= row.draftCount) return 'status-sent';
    if (row.sentCount === 0) return 'status-draft';
    return 'status-approved';
  }

  getRowStatusLabel(row: DraftRow): string {
    if (row.draftCount === 0) return 'Aucun draft';
    if (row.sentCount >= row.draftCount) return 'Tout envoye';
    if (row.sentCount === 0) return 'A envoyer';
    return 'En cours';
  }

  getDraftStatusClass(draft: FlatDraft): string {
    if (draft.isSent) return 'pill-sent';
    if (draft.isApproved) return 'pill-approved';
    return 'pill-draft';
  }

  getDraftCardClass(draft: FlatDraft): string {
    if (draft.isSent) return 'status-sent';
    if (draft.isApproved) return 'status-approved';
    return 'status-draft';
  }
}
