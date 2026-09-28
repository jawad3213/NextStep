/// <reference types="vitest" />
import { defineConfig } from 'vite';
import angular from '@analogjs/vite-plugin-angular';
import { fileURLToPath } from 'node:url';

const src = (dir: string) => fileURLToPath(new URL(`./src/${dir}`, import.meta.url));

export default defineConfig(({ mode }) => ({
  plugins: [angular()],
  resolve: {
    // Same aliases as tsconfig.json "paths"
    alias: {
      '@core': src('app/core'),
      '@shared': src('app/shared'),
      '@features': src('app/features'),
      '@env': src('environments'),
    },
  },
  test: {
    globals: true,
    setupFiles: ['src/setup-vitest.ts'],
    environment: 'jsdom',
    include: ['src/**/*.test.ts'],
    reporters: ['default'],
  },
  define: {
    'import.meta.vitest': mode !== 'production',
  },
}));
