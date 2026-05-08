import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { forkJoin, catchError, of } from 'rxjs';
import { CandidatureService, CandidatureDto } from '../../../services/candidature.service';
import { OfferService, OfferDto } from '../../../services/offer.service';
import {
  EmailService,
  EmailDraftDto,
  EmailConnectionStatusDto,
  GenerateReplyDraftPayload
} from '../../../services/email.service';

@Component({
  selector: 'app-email-workspace',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './email-workspace.component.html',
  styleUrl: './email-workspace.component.scss'
})
export class EmailWorkspaceComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly candidatureService = inject(CandidatureService);
  private readonly offerService = inject(OfferService);
  private readonly emailService = inject(EmailService);

  // ── State ────────────────────────────────────────────────────────────────
  candidatureId = '';
  candidature = signal<CandidatureDto | null>(null);
  offer = signal<OfferDto | null>(null);
  drafts = signal<EmailDraftDto[]>([]);
  selectedDraft = signal<EmailDraftDto | null>(null);
  gmailStatus = signal<EmailConnectionStatusDto | null>(null);

  // Loading flags
  loadingPage = signal(true);
  generatingDraft = signal(false);
  generatingReply = signal(false);
  savingDraft = signal(false);
  approvingDraft = signal(false);
  sendingDraft = signal(false);

  // Editor fields
  recipientEmail = '';
  subject = '';
  body = '';
  emailType = 'application';
  language = 'fr';
  userInstructions = ''; // for reply draft generation

  // Messages
  successMessage = signal<string | null>(null);
  errorMessage = signal<string | null>(null);
  pageError = signal<string | null>(null);

  // ── Init ─────────────────────────────────────────────────────────────────
  ngOnInit() {
    this.candidatureId = this.route.snapshot.paramMap.get('candidatureId') ?? '';
    if (!this.candidatureId) {
      this.pageError.set('Identifiant de candidature manquant.');
      this.loadingPage.set(false);
      return;
    }
    this.loadPage();
  }

  private loadPage() {
    this.loadingPage.set(true);
    this.pageError.set(null);

    // Parallel load: candidature + drafts + Gmail status
    forkJoin({
      candidature: this.candidatureService.getById(this.candidatureId).pipe(catchError(() => of(null))),
      drafts: this.emailService.getDraftsByCandidature(this.candidatureId).pipe(catchError(() => of([]))),
      gmail: this.emailService.getGmailStatus().pipe(catchError(() => of(null)))
    }).subscribe(({ candidature, drafts, gmail }) => {
      if (!candidature) {
        this.pageError.set('Candidature introuvable ou accès refusé.');
        this.loadingPage.set(false);
        return;
      }

      this.candidature.set(candidature);
      this.gmailStatus.set(gmail);
      this.drafts.set(drafts as EmailDraftDto[]);

      // Auto-select the latest non-sent draft, or latest draft
      const sorted = [...(drafts as EmailDraftDto[])].sort(
        (a, b) => new Date(b.createdAtUtc).getTime() - new Date(a.createdAtUtc).getTime()
      );
      const best = sorted.find(d => !d.isSent) ?? sorted[0] ?? null;
      if (best) this.selectDraft(best);

      // Load offer details
      if (candidature.idOffre) {
        this.offerService.getOfferById(candidature.idOffre).pipe(catchError(() => of(null)))
          .subscribe(offer => this.offer.set(offer));
      }

      this.loadingPage.set(false);
    });
  }

  // ── Draft selection ───────────────────────────────────────────────────────
  selectDraft(draft: EmailDraftDto) {
    this.selectedDraft.set(draft);
    this.recipientEmail = draft.recipientEmail ?? '';
    this.subject = draft.subject ?? '';
    this.body = draft.body ?? '';
    this.clearMessages();
  }

  // ── Actions ───────────────────────────────────────────────────────────────
  generateDraft() {
    if (this.generatingDraft()) return;
    this.generatingDraft.set(true);
    this.clearMessages();

    this.emailService.generateDraft({
      candidatureId: this.candidatureId,
      emailType: this.emailType,
      language: this.language
    }).subscribe({
      next: (draft) => {
        // Prepend to local drafts list and select immediately
        this.drafts.update(drafts => [draft, ...drafts]);
        this.selectDraft(draft);
        this.successMessage.set('Brouillon généré avec succès.');
        this.generatingDraft.set(false);
      },
      error: (err) => {
        this.errorMessage.set(err?.error || 'Erreur lors de la génération du brouillon.');
        this.generatingDraft.set(false);
      }
    });
  }

  generateFollowUpDraft() {
    if (this.generatingDraft()) return;
    this.generatingDraft.set(true);
    this.clearMessages();

    this.emailService.generateFollowUpDraft({
      candidatureId: this.candidatureId,
      language: this.language,
      tone: 'professionnel'
    }).subscribe({
      next: (draft) => {
        this.drafts.update(drafts => [draft, ...drafts]);
        this.selectDraft(draft);
        const current = this.candidature();
        if (current) {
          this.candidature.set({ ...current, responseStatus: 'RELANCE_GENEREE', statut: 'RELANCE_GENEREE' });
        }
        this.successMessage.set('Email de relance généré avec succès.');
        this.generatingDraft.set(false);
      },
      error: (err) => {
        this.errorMessage.set(err?.error || 'Erreur lors de la génération de la relance.');
        this.generatingDraft.set(false);
      }
    });
  }

  generateReplyDraft() {
    if (this.generatingReply()) return;
    this.generatingReply.set(true);
    this.clearMessages();

    const payload: GenerateReplyDraftPayload = {
      candidatureId: this.candidatureId,
      language: this.language,
      tone: 'professionnel',
      userInstructions: this.userInstructions.trim() || undefined
    };

    this.emailService.generateReplyDraft(payload).subscribe({
      next: (draft) => {
        this.drafts.update(drafts => [draft, ...drafts]);
        this.selectDraft(draft);
        this.userInstructions = '';
        this.successMessage.set('Brouillon de réponse généré. Vérifiez et approuvez avant envoi.');
        this.generatingReply.set(false);
      },
      error: (err) => {
        this.errorMessage.set(err?.error || 'Erreur lors de la génération de la réponse.');
        this.generatingReply.set(false);
      }
    });
  }

  saveDraft() {
    const draft = this.selectedDraft();
    if (!draft || this.savingDraft()) return;
    this.savingDraft.set(true);
    this.clearMessages();

    this.emailService.updateDraft(draft.id, {
      recipientEmail: this.recipientEmail,
      subject: this.subject,
      body: this.body
    }).subscribe({
      next: (updated) => {
        this.updateDraftInList(updated);
        this.selectedDraft.set(updated);
        this.successMessage.set('Brouillon sauvegardé.');
        this.savingDraft.set(false);
      },
      error: (err) => {
        this.errorMessage.set(err?.error || 'Erreur lors de la sauvegarde.');
        this.savingDraft.set(false);
      }
    });
  }

  approveDraft() {
    const draft = this.selectedDraft();
    if (!draft || this.approvingDraft()) return;
    this.approvingDraft.set(true);
    this.clearMessages();

    this.emailService.approveDraft(draft.id).subscribe({
      next: (updated) => {
        this.updateDraftInList(updated);
        this.selectedDraft.set(updated);
        this.successMessage.set('Brouillon approuvé. Vous pouvez maintenant l\'envoyer.');
        this.approvingDraft.set(false);
      },
      error: (err) => {
        this.errorMessage.set(err?.error || 'Erreur lors de l\'approbation.');
        this.approvingDraft.set(false);
      }
    });
  }

  sendDraft() {
    const draft = this.selectedDraft();
    if (!draft || this.sendingDraft()) return;
    this.sendingDraft.set(true);
    this.clearMessages();

    this.emailService.sendDraft(draft.id).subscribe({
      next: (result) => {
        this.sendingDraft.set(false);
        if (result.success) {
          // Update the draft in local state
          const updated: EmailDraftDto = {
            ...draft,
            isSent: true,
            sentAtUtc: result.sentAtUtc,
            providerMessageId: result.providerMessageId,
            errorMessage: null
          };
          this.updateDraftInList(updated);
          this.selectedDraft.set(updated);
          this.successMessage.set(`Email envoyé avec succès ! ID Gmail : ${result.providerMessageId ?? '—'}`);
        } else {
          this.errorMessage.set(result.errorMessage ?? 'L\'envoi a échoué.');
        }
      },
      error: (err) => {
        this.errorMessage.set(err?.error || 'Erreur lors de l\'envoi.');
        this.sendingDraft.set(false);
      }
    });
  }

  connectGmail() {
    this.emailService.getGmailLoginUrl().subscribe({
      next: (res) => {
        if (res?.url) window.location.href = res.url;
      },
      error: () => {
        this.errorMessage.set('Impossible d\'obtenir l\'URL de connexion Gmail.');
      }
    });
  }

  refreshGmailStatus() {
    this.emailService.getGmailStatus().subscribe({
      next: (status) => this.gmailStatus.set(status)
    });
  }

  // ── Helpers ───────────────────────────────────────────────────────────────
  private updateDraftInList(updated: EmailDraftDto) {
    this.drafts.update(drafts => drafts.map(d => d.id === updated.id ? updated : d));
  }

  private clearMessages() {
    this.successMessage.set(null);
    this.errorMessage.set(null);
  }

  get canSave(): boolean {
    const d = this.selectedDraft();
    return !!d && !d.isSent && !this.savingDraft();
  }

  get canApprove(): boolean {
    const d = this.selectedDraft();
    return !!d && !d.isSent && !d.isApproved &&
      !!this.recipientEmail.trim() && !!this.subject.trim() && !!this.body.trim() &&
      !this.approvingDraft();
  }

  get canSend(): boolean {
    const d = this.selectedDraft();
    const gmail = this.gmailStatus();
    return !!d && d.isApproved && !d.isSent &&
      !!d.recipientEmail && !!d.subject && !!d.body &&
      !!gmail?.isConnected && !this.sendingDraft();
  }

  get canGenerateRelance(): boolean {
    const cand = this.candidature();
    if (!cand) return false;
    if (cand.hasResponse) return false;
    return this.drafts().some(d => d.isSent) && !this.generatingDraft();
  }

  get canGenerateReply(): boolean {
    const cand = this.candidature();
    if (!cand) return false;
    // Only show if a recruiter response was detected
    return !!cand.hasResponse && !this.generatingReply();
  }

  formatDate(dateStr: string | null): string {
    if (!dateStr) return '—';
    return new Date(dateStr).toLocaleDateString('fr-FR', { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' });
  }

  getDraftLabel(draft: EmailDraftDto): string {
    const status = draft.isSent ? '✓ Envoyé' : draft.isApproved ? '✓ Approuvé' : 'Brouillon';
    return `${draft.emailType} — ${status}`;
  }

  getDraftClass(draft: EmailDraftDto): string {
    if (draft.isSent) return 'draft-sent';
    if (draft.isApproved) return 'draft-approved';
    return 'draft-pending';
  }

  getCandidatureStatusClass(statut: string): string {
    switch (statut) {
      case 'ENVOYE': return 'status-sent';
      case 'VU': return 'status-approved';
      case 'ENTRETIEN_PROPOSE': return 'status-info';
      case 'INFORMATIONS_DEMANDEES': return 'status-warning';
      case 'ACCEPTE': return 'status-success';
      case 'REFUSE': return 'status-error';
      case 'REPONSE_RECUE': return 'status-info';
      case 'REPONSE_AUTOMATIQUE': return 'status-none';
      default: return 'status-neutral';
    }
  }

  goBack() {
    this.router.navigate(['/applications']);
  }
}
