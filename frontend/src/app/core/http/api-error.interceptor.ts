import { HttpInterceptorFn } from '@angular/common/http';
import { throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { inject } from '@angular/core';
import { ToastService } from '../notifications/toast.service';
import { extractApiError } from '../utils/extract-api-error';

/**
 * Interceptor global de gestion d'erreurs HTTP :
 *   - normalise les erreurs via le contrat d'erreur commun (extractApiError)
 *   - affiche un toast pour les réponses 5xx non attendues (et 401 hors auth angulaire)
 *   - ré-émet l'erreur normalisée pour que les services/composants puissent la traiter.
 *
 * Les composants restent libres d'afficher leur propre état d'erreur ; le toast
 * est un filet de sécurité pour les cas non couverts par une page.
 */
export const apiErrorInterceptor: HttpInterceptorFn = (req, next) => {
  const toast = inject(ToastService);

  const reqWithHeader = req.clone({
    setHeaders: {
      'ngrok-skip-browser-warning': 'true',
    },
  });

  return next(reqWithHeader).pipe(
    catchError((err: unknown) => {
      const normalized = extractApiError(err);

      // Toast global uniquement pour les 5xx (le backend est down, une route a
      // échoué sans gestion dédiée). On évite de spammer sur les 400/404 qui
      // sont souvent gérés localement. Les imports de CV/Profile gèrent leur
      // propre affichage (toast + feed terminal) — pas de doublon ici.
      const isProfileImport =
        req.url.includes('/parse-resume') || req.url.includes('/import-linkedin');

      if (normalized.status && normalized.status >= 500 && !isProfileImport) {
        toast.error(normalized.message || 'Le serveur rencontre un problème.');
      }

      return throwError(() => err);
    })
  );
};