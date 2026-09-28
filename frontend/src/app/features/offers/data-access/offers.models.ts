export type OfferStepId = 'submit' | 'analysis' | 'template' | 'generation' | 'results';

export interface OfferStep {
  id: OfferStepId;
  label: string;
  icon: string;
}
