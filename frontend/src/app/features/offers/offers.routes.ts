import { Routes } from '@angular/router';

/** /offers — analysed job offers and the analysis → CV pipeline. */
export const OFFERS_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./pages/offers/offers.component').then(m => m.OffersComponent),
  },
  {
    path: 'analyze',
    loadComponent: () => import('./pages/offer-pipeline/offer-pipeline.component').then(m => m.OfferPipelineComponent),
  },
  {
    path: ':id',
    loadComponent: () => import('./pages/offer-detail/offer-detail.component').then(m => m.OfferDetailComponent),
  },
];

/** /offers-recent — offers sourced from job boards. */
export const SOURCED_OFFERS_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./pages/sourced-offers/offers-recent.component').then(m => m.OffersRecentComponent),
  },
  {
    path: ':id',
    loadComponent: () =>
      import('./pages/sourced-offer-detail/sourced-offer-detail.component').then(m => m.SourcedOfferDetailComponent),
  },
];
