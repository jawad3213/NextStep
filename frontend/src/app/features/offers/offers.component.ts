import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { OfferService, OfferDto } from '../../services/offer.service';
import { CandidatureService } from '../../services/candidature.service';

@Component({
  selector: 'app-offers',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './offers.component.html',
  styleUrl: './offers.component.scss'
})
export class OffersComponent implements OnInit {
  private readonly offerService = inject(OfferService);
  private readonly candidatureService = inject(CandidatureService);
  private readonly router = inject(Router);

  loading = signal(true);
  applying = signal<string | null>(null);
  isSubmitting = signal(false);
  showSubmitForm = signal(false);
  rawText = signal('');
  titre = signal('');
  entreprise = signal('');
  error = signal<string | null>(null);
  successMessage = signal<string | null>(null);
  offers = signal<OfferDto[]>([]);

  ngOnInit() {
    this.load();
  }

  load() {
    this.loading.set(true);
    this.error.set(null);
    this.offerService.getMyOffers().subscribe({
      next: (offers) => {
        this.offers.set(offers);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Impossible de charger les offres.');
        this.loading.set(false);
      }
    });
  }

  apply(offer: OfferDto) {
    if (this.applying()) return;
    this.applying.set(offer.offerId);
    this.error.set(null);
    this.successMessage.set(null);

    this.candidatureService.create({ idOffre: offer.offerId }).subscribe({
      next: (candidature) => {
        this.applying.set(null);
        this.router.navigate(['/candidatures', candidature.idCandidature, 'email']);
      },
      error: (err) => {
        this.applying.set(null);
        this.error.set(err?.error || 'Erreur lors de la création de la candidature.');
      }
    });
  }

  submitJob() {
    if (!this.rawText().trim()) return;
    
    this.isSubmitting.set(true);
    this.error.set(null);

    this.offerService.submitOffer({ 
      rawText: this.rawText(),
      titre: this.titre(),
      entreprise: this.entreprise(),
      templateId: 'standard' 
    }).subscribe({
      next: (newOffer) => {
        this.isSubmitting.set(false);
        this.showSubmitForm.set(false);
        this.rawText.set('');
        this.titre.set('');
        this.entreprise.set('');
        // Add the new offer to the top of the list
        this.offers.update(prev => [newOffer, ...prev]);
        this.successMessage.set('Offre ajoutée avec succès !');
        setTimeout(() => this.successMessage.set(null), 5000);
      },
      error: (err) => {
        this.isSubmitting.set(false);
        this.error.set(err?.error || 'Erreur lors de l\'ajout de l\'offre.');
      }
    });
  }

  getScoreColor(score: number): string {
    if (score >= 70) return 'score-good';
    if (score >= 40) return 'score-medium';
    return 'score-low';
  }
}
