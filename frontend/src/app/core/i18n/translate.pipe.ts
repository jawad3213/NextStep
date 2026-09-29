import { Pipe, PipeTransform, inject } from '@angular/core';
import { LanguageService } from '@core/i18n/language.service';

/**
 * Translates a key string into the current app language.
 * Usage: {{ 'nav.dashboard' | translate }}
 * Falls back to the key itself if not found.
 */
@Pipe({
  name: 'translate',
  standalone: true,
  // Must be pure: false so it re-evaluates when the signal changes.
  pure: false
})
export class TranslatePipe implements PipeTransform {
  private readonly langService = inject(LanguageService);

  transform(key: string): string {
    return this.langService.t(key);
  }
}
