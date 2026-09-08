import { Injectable, inject } from '@angular/core';
import { MatSnackBar, MatSnackBarConfig } from '@angular/material/snack-bar';

@Injectable({ providedIn: 'root' })
export class ToastService {
  private readonly snackBar = inject(MatSnackBar);

  success(message: string, duration = 3000): void {
    this.show(message, { duration, panelClass: ['toast-success'] });
  }

  error(message: string, duration = 5000): void {
    this.show(message, { duration, panelClass: ['toast-error'] });
  }

  info(message: string, duration = 3000): void {
    this.show(message, { duration, panelClass: ['toast-info'] });
  }

  dismiss(): void {
    this.snackBar.dismiss();
  }

  /** Shortcut pour afficher une erreur d'API normalisée. */
  apiError(error: unknown, fallback = 'Une erreur est survenue.'): void {
    const message = this.extractShortMessage(error) ?? fallback;
    this.error(message);
  }

  private extractShortMessage(error: unknown): string | null {
    if (error && typeof error === 'object' && 'message' in error) {
      const m = (error as { message?: unknown }).message;
      if (typeof m === 'string' && m.length > 0 && m.length < 300) return m;
    }
    return null;
  }

  private show(message: string, config: MatSnackBarConfig): void {
    this.snackBar.open(message, 'Fermer', config);
  }
}
