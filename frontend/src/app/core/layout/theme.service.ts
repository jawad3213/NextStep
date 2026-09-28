import { Injectable, signal } from '@angular/core';

export type ThemeMode = 'light' | 'dark';

@Injectable({
  providedIn: 'root'
})
export class ThemeService {
  private readonly storageKey = 'nextstep-theme';

  readonly theme = signal<ThemeMode>('light');

  constructor() {
    this.setTheme(this.readStoredTheme());
  }

  toggleTheme(): void {
    this.setTheme(this.theme() === 'light' ? 'dark' : 'light');
  }

  setTheme(theme: ThemeMode): void {
    this.theme.set(theme);

    try {
      localStorage.setItem(this.storageKey, theme);
    } catch {
      // Ignore storage failures and still apply the theme in-memory.
    }

    document.documentElement.classList.toggle('dark', theme === 'dark');
    document.documentElement.style.colorScheme = theme;
  }

  private readStoredTheme(): ThemeMode {
    try {
      const storedTheme = localStorage.getItem(this.storageKey);
      return storedTheme === 'dark' ? 'dark' : 'light';
    } catch {
      return 'light';
    }
  }
}
