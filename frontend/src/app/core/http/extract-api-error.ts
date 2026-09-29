import { HttpErrorResponse } from '@angular/common/http';

export interface ExtractedApiError {
  status: number | null;
  message: string;
  details?: Record<string, string[]>;
  type?: string;
}

/**
 * Normalise n'importe quelle réponse d'erreur HTTP (ou exception) vers un objet
 * lisible par l'UI. Accepte le contrat d'erreur backend :
 *   { error, type?, details? , traceId? }
 * ainsi que les variantes FastAPI ({ detail }) et les erreurs réseau natives.
 */
export function extractApiError(err: unknown): ExtractedApiError {
  if (err instanceof HttpErrorResponse) {
    const status = err.status ?? 0;

    // Corps JSON backend / FastAPI
    if (err.error && typeof err.error === 'object') {
      const body = err.error as Record<string, unknown>;

      // Contrat backend: { error, type?, details? }
      if (typeof body['error'] === 'string' && (body['error'] as string).length > 0) {
        return {
          status,
          message: body['error'] as string,
          type: typeof body['type'] === 'string' ? (body['type'] as string) : undefined,
          details: isDetailsDict(body['details']) ? (body['details'] as Record<string, string[]>) : undefined,
        };
      }

      // FastAPI: { detail } (string ou tableau de validation)
      if (body['detail'] !== undefined) {
        if (typeof body['detail'] === 'string') {
          return { status, message: body['detail'] as string };
        }
        if (Array.isArray(body['detail'])) {
          const msgs = (body['detail'] as unknown[])
            .map(item => (item as Record<string, unknown>)?.['msg'])
            .filter((m): m is string => typeof m === 'string');
          return {
            status,
            message: msgs.length ? msgs.join(', ') : 'Validation error',
          };
        }
      }
    }

    // Plain text body
    if (typeof err.error === 'string' && err.error.length > 0) {
      return { status, message: err.error };
    }

    // Status code only or network failure
    if (status === 0) {
      return { status, message: 'Unable to reach the server. Please check your connection.' };
    }
    return { status, message: `HTTP Error ${status}` };
  }

  if (err instanceof Error) {
    return { status: null, message: err.message };
  }

  if (typeof err === 'string') {
    return { status: null, message: err };
  }

  return { status: null, message: 'An unexpected error occurred.' };
}

function isDetailsDict(value: unknown): value is Record<string, string[]> {
  return !!value && typeof value === 'object' && !Array.isArray(value);
}
