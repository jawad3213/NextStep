import { Injectable } from '@angular/core';
import { toast } from 'ngx-sonner';
import { extractApiError } from '@core/http/extract-api-error';

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

  /** Shortcut to show a normalized API error. */
  apiError(error: unknown, fallback = 'An unexpected error occurred.'): void {
    this.error(extractApiError(error).message || fallback);
  }
}