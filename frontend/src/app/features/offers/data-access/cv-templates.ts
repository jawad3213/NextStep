import { CvDesignConfig } from '@features/cv-builder/data-access/cv.models';

/**
 * The HTML CV templates rendered by the backend (backend/Modules/Cv/Templates/Html/<slug>).
 * Single source of truth for the template step, the generation step and the editor.
 * Keep in sync with TemplateDefaults in backend CvHtmlTemplateRenderer.cs.
 */
export type CvTemplateSlug = 'modern' | 'latex' | 'executive' | 'horizon';

export interface CvTemplateDefinition {
  slug: CvTemplateSlug;
  label: string;
  tone: string;
  /** True when the template has a side column (its width is adjustable in the editor). */
  hasSidebar: boolean;
  defaults: CvDesignConfig;
}

export const CV_TEMPLATES: readonly CvTemplateDefinition[] = [
  {
    slug: 'modern',
    label: 'Modern',
    tone: 'Tech Minimal',
    hasSidebar: true,
    defaults: {
      themeColor: '#2d3a8c', fontFamily: "Inter, 'Segoe UI', Arial, sans-serif",
      fontSize: '14px', lineSpacing: '1.45', sectionSpacing: '1.2rem', sidebarWidth: '31%',
    },
  },
  {
    slug: 'latex',
    label: 'LaTeX Tech',
    tone: 'Classic Engineering',
    hasSidebar: false,
    defaults: {
      themeColor: '#111827', fontFamily: "'IBM Plex Sans', 'Segoe UI', Arial, sans-serif",
      fontSize: '13px', lineSpacing: '1.38', sectionSpacing: '1rem', sidebarWidth: '0%',
    },
  },
  {
    slug: 'executive',
    label: 'Executive',
    tone: 'Elegant Serif',
    hasSidebar: false,
    defaults: {
      themeColor: '#b93317', fontFamily: "Georgia, 'Liberation Serif', 'Times New Roman', serif",
      fontSize: '13.5px', lineSpacing: '1.45', sectionSpacing: '1.15rem', sidebarWidth: '0%',
    },
  },
  {
    slug: 'horizon',
    label: 'Horizon',
    tone: 'Timeline & Header Band',
    hasSidebar: true,
    defaults: {
      themeColor: '#18a7a0', fontFamily: "'Segoe UI', 'Liberation Sans', Arial, sans-serif",
      fontSize: '13.5px', lineSpacing: '1.45', sectionSpacing: '1.1rem', sidebarWidth: '34%',
    },
  },
];

export const CV_TEMPLATE_SLUGS: readonly CvTemplateSlug[] = CV_TEMPLATES.map((t) => t.slug);

/** Maps any stored/legacy id to a known template ('tech-latex' -> 'latex'; unknown -> 'modern'). */
export function normalizeCvTemplateSlug(value: string | null | undefined): CvTemplateSlug {
  const normalized = String(value ?? '').trim().toLowerCase();
  if (normalized === 'tech-latex' || normalized === 'tech_latex') return 'latex';
  return (CV_TEMPLATE_SLUGS as readonly string[]).includes(normalized) ? (normalized as CvTemplateSlug) : 'modern';
}

export function cvTemplate(slug: string | null | undefined): CvTemplateDefinition {
  const normalized = normalizeCvTemplateSlug(slug);
  return CV_TEMPLATES.find((t) => t.slug === normalized)!;
}

/** A fresh copy of the template's default design (safe to mutate). */
export function defaultCvDesignConfig(slug: string | null | undefined): CvDesignConfig {
  return { ...cvTemplate(slug).defaults };
}
