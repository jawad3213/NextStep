import { HttpContextToken, HttpInterceptorFn } from '@angular/common/http';
import { throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { inject } from '@angular/core';
import { ToastService } from '@core/notifications/toast.service';
import { extractApiError } from './extract-api-error';

/**
 * Jeton à passer via HttpContext pour supprimer le toast automatique d'erreur
 * pour une requête précise (utile quand le composant gère lui-même l'affichage).
 */
export const SUPPRESS_ERROR_TOAST = new HttpContextToken<boolean>(() => false);

/**
 * Préfixes d'URL gérés localement par leur écran (toast/banner custom déjà en place).
 * On évite de déclencher un toast global en double.
 */
const SELF_MANAGED_HINTS = [
  '/parse-resume',
  '/import-linkedin',
  '/emails',           // email-workspace (toasts locaux)
  '/email-connections',// gmail-settings (toasts locaux)
  '/arena',            // chatbot (gestion en-session)
  '/chatbot',          // chatbot (gestion en-session)
  '/sn/',              // SN Copilot (réponse in-chat)
];

/**
 * Interceptor global de gestion d'erreurs HTTP :
 *   - normalise n'importe quelle erreur (4xx, 5xx, réseau) via extractApiError
 *   - affiche un toast d'échec pour TOUTES les actions utilisateur
 *   - ré-émet l'erreur d'origine pour que services/composants puissent la traiter.
 *
 * Désactivation au cas-par-cas : mettre SUPPRESS_ERROR_TOAST dans le HttpContext
 * de la requête, ou laisser une URL listée dans SELF_MANAGED_HINTS.
 */
export const apiErrorInterceptor: HttpInterceptorFn = (req, next) => {
  const toast = inject(ToastService);

  return next(req).pipe(
    catchError((err: unknown) => {
      const normalized = extractApiError(err);
      const suppressed =
        req.context.get(SUPPRESS_ERROR_TOAST) ||
        SELF_MANAGED_HINTS.some((hint) => req.url.includes(hint));

      if (!suppressed) {
        toast.error(normalized.message || 'Une erreur est survenue.');
      }

      return throwError(() => err);
    })
  );
};