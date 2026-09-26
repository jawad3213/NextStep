import { Injectable } from '@angular/core';
import { toast } from 'ngx-sonner';
import { extractApiError } from '../utils/extract-api-error';

/**
 * Couche d'abstraction sur ngx-sonner, affiché en haut à droite.
 * Garde l'API historique (success/error/info/dismiss/apiError) pour que tous
 * les composants et l'intercepteur HTTP fonctionnent sans modification.
 */
@Injectable({ providedIn: 'root' })
export class ToastService {
  success(message: string, duration = 3000): void {
    toast.success(message, { duration });
  }

  error(message: string, duration = 5000): void {
    toast.error(message, { duration });
  }

  info(message: string, duration = 3000): void {
    toast.message(message, { duration });
  }

  dismiss(): void {
    toast.dismiss();
  }

  /** Shortcut pour afficher une erreur d'API normalisée. */
  apiError(error: unknown, fallback = 'Une erreur est survenue.'): void {
    this.error(extractApiError(error).message || fallback);
  }
}