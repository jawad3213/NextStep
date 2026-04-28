import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-offers',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="shell-container">
      <header class="page-header">
        <h1 class="page-title">Offres d'emploi</h1>
        <p class="page-description">Analysez les annonces et suivez vos opportunités grâce à l'intelligence artificielle.</p>
      </header>
      
      <div class="card card-accent">
        <p class="card-placeholder">
          Le moteur d'analyse d'offres est en cours d'intégration. 
          Vous pourrez bientôt coller une URL ou un texte pour obtenir un score de compatibilité instantané.
        </p>
      </div>

      <div style="margin-top: 24px; display: grid; grid-template-columns: 1fr 1fr; gap: 24px;">
        <div class="card">
          <h4 style="margin-top: 0; color: #1e293b;">Dernières recherches</h4>
          <p style="color: #64748b; font-size: 14px;">Vos analyses récentes apparaîtront ici pour un accès rapide.</p>
        </div>
        <div class="card">
          <h4 style="margin-top: 0; color: #1e293b;">Suggestions IA</h4>
          <p style="color: #64748b; font-size: 14px;">Basé sur votre profil, nous vous suggérerons les meilleures offres du marché.</p>
        </div>
      </div>
    </div>
  `,
  styles: [] 
})
export class OffersComponent {}
