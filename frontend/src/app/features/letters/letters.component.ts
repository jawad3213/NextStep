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
  template: `
    <div class="letters-shell">
      <header class="page-header">
        <h1 class="title">Email & Letter</h1>
        <p class="subtitle">
          Tous les brouillons sont geres par candidature. Ouvre un workspace pour generer, modifier et envoyer.
        </p>
      </header>

      @if (loading()) {
        <div class="state-card">Chargement des drafts...</div>
      } @else if (error()) {
        <div class="state-card error">{{ error() }}</div>
      } @else {
        <section class="panel">
          <div class="panel-head">
            <h2>Mes candidatures email</h2>
            <span class="badge">{{ rows().length }}</span>
          </div>

          @if (rows().length === 0) {
            <div class="state-card">
              Aucune candidature trouvee. Cree une candidature depuis Applications pour activer les drafts.
            </div>
          } @else {
            <div class="cards-grid candidature-grid">
              @for (row of rows(); track row.candidatureId) {
                <article class="mail-card" [class]="getRowCardClass(row)">
                  <div class="card-head">
                    <span class="card-type">Candidature</span>
                    <span class="card-status-pill" [class]="getRowStatusClass(row)">
                      {{ getRowStatusLabel(row) }}
                    </span>
                  </div>
                  <div class="row-main">
                    <div class="row-title">{{ row.role }}</div>
                    <div class="row-sub">{{ row.company }}</div>
                    <div class="chips">
                      <span class="chip chip-blue">{{ row.draftCount }} draft(s)</span>
                      <span class="chip chip-green">{{ row.sentCount }} envoye(s)</span>
                      <span class="chip chip-gray">Dernier: {{ formatDate(row.lastDraftAt) }}</span>
                    </div>
                  </div>
                  <button class="open-btn" (click)="openWorkspace(row.candidatureId)">
                    Ouvrir workspace
                  </button>
                </article>
              }
            </div>
            @if (hasMoreCandidatures()) {
              <div class="load-more-wrap">
                <button class="load-more-btn" (click)="loadMore()" [disabled]="loadingMore()">
                  @if (loadingMore()) { Chargement... } @else { Voir plus }
                </button>
              </div>
            }
          }
        </section>

        <section class="panel">
          <div class="panel-head">
            <h2>Derniers drafts</h2>
            <span class="badge">{{ allDrafts().length }}</span>
          </div>

          @if (allDrafts().length === 0) {
            <div class="state-card">Aucun draft detecte pour le moment.</div>
          } @else {
            <div class="cards-grid drafts-grid">
              @for (d of recentDrafts(); track d.id) {
                <article class="mail-card compact" [class]="getDraftCardClass(d)">
                  <div class="card-head">
                    <span class="card-type">{{ d.emailType }}</span>
                    <span class="card-status-pill" [class]="getDraftStatusClass(d)">
                      {{ d.isSent ? 'Envoye' : d.isApproved ? 'Approuve' : 'Brouillon' }}
                    </span>
                  </div>
                  <div class="row-main">
                    <div class="row-title">{{ d.subject || '(sans sujet)' }}</div>
                    <div class="row-sub">{{ d.company }} - {{ d.role }}</div>
                    <div class="chips">
                      <span class="chip chip-indigo">{{ d.language.toUpperCase() }}</span>
                      <span class="chip chip-gray">{{ formatDate(d.createdAtUtc) }}</span>
                    </div>
                  </div>
                  <button class="open-btn" (click)="openWorkspace(d.candidatureId)">
                    Ouvrir
                  </button>
                </article>
              }
            </div>
          }
        </section>
      }
    </div>
  `,
  styles: [`
    :host { display: block; padding: 24px 40px; }
    .letters-shell { display: grid; gap: 16px; }
    .page-header { display: grid; gap: 6px; }
    .title { margin: 0; font-size: 30px; font-weight: 800; color: #0f172a; }
    .subtitle { margin: 0; color: #475569; }
    .panel { background: #fff; border: 1px solid #e2e8f0; border-radius: 14px; padding: 16px; display: grid; gap: 12px; }
    .panel-head { display: flex; align-items: center; justify-content: space-between; }
    .panel-head h2 { margin: 0; font-size: 18px; font-weight: 700; color: #111827; }
    .badge { background: #e0e7ff; color: #3730a3; border-radius: 999px; padding: 2px 10px; font-size: 12px; font-weight: 700; }
    .state-card { border: 1px dashed #cbd5e1; border-radius: 12px; padding: 16px; color: #475569; }
    .state-card.error { border-color: #fecaca; color: #b91c1c; background: #fef2f2; }
    .cards-grid { display: grid; gap: 14px; }
    .candidature-grid { grid-template-columns: repeat(auto-fit, minmax(300px, 1fr)); }
    .drafts-grid { grid-template-columns: repeat(auto-fit, minmax(260px, 1fr)); }
    .mail-card {
      display: flex;
      flex-direction: column;
      gap: 12px;
      border: 1px solid #e5e7eb;
      border-radius: 14px;
      padding: 14px;
      background: linear-gradient(180deg, #ffffff 0%, #fcfdff 100%);
      transition: border-color .2s ease, box-shadow .2s ease, transform .15s ease;
      min-height: 184px;
    }
    .mail-card:hover {
      border-color: #c7d2fe;
      box-shadow: 0 10px 20px rgba(15, 23, 42, 0.05);
      transform: translateY(-1px);
    }
    .mail-card.status-empty { border-top: 4px solid #94a3b8; }
    .mail-card.status-draft { border-top: 4px solid #f59b00; }
    .mail-card.status-approved { border-top: 4px solid #7c3aed; }
    .mail-card.status-sent { border-top: 4px solid #34a853; }
    .mail-card.compact { padding: 12px; }
    .card-head { display: flex; align-items: center; justify-content: space-between; gap: 10px; }
    .card-type { font-size: 11px; font-weight: 700; letter-spacing: .04em; text-transform: uppercase; color: #64748b; }
    .card-status-pill { border-radius: 999px; padding: 4px 10px; font-size: 11px; font-weight: 700; }
    .pill-empty { background: #f1f5f9; color: #475569; }
    .pill-draft { background: #fff4e5; color: #8f4900; }
    .pill-approved { background: #f5f0ff; color: #6d28d9; }
    .pill-sent { background: #eaf8ee; color: #1f8f49; }
    .row-main { min-width: 0; display: grid; gap: 4px; }
    .row-title { font-weight: 700; color: #111827; }
    .row-sub { color: #1d4ed8; font-weight: 600; }
    .chips { display: flex; gap: 8px; flex-wrap: wrap; }
    .chip { border-radius: 999px; padding: 4px 10px; font-size: 12px; font-weight: 600; }
    .chip-blue { background: #edf2ff; color: #465fff; }
    .chip-green { background: #eaf8ee; color: #1f8f49; }
    .chip-indigo { background: #f5f0ff; color: #7c3aed; }
    .chip-gray { background: #f8fafc; color: #475569; }
    .open-btn {
      margin-top: auto;
      border: 1px solid #b9c6ff;
      background: #edf2ff;
      color: #3347cc;
      border-radius: 10px;
      padding: 9px 12px;
      font-weight: 700;
      cursor: pointer;
    }
    .open-btn:hover { background: #dfe7ff; }
    .load-more-wrap { display: flex; justify-content: center; margin-top: 2px; }
    .load-more-btn {
      border: 1px solid #dbe3ff;
      background: #edf2ff;
      color: #3347cc;
      border-radius: 10px;
      padding: 9px 14px;
      font-weight: 700;
      cursor: pointer;
    }
    .load-more-btn:disabled { opacity: .65; cursor: not-allowed; }

    @media (max-width: 900px) {
      :host { padding: 16px; }
      .title { font-size: 24px; }
    }
  `]
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
