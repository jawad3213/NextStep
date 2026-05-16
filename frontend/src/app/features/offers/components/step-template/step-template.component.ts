import { Component, inject, signal, WritableSignal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PipelineStateService } from '../../../../services/pipeline-state.service';

interface CvTemplate {
  id: string;
  name: string;
  description: string;
  badge?: { label: string; variant: 'primary' | 'secondary' };
  industry: string;
  experience: string;
  style: string;
  layout: string;
  tag: string;
  accent: string;
  previewType: 'sidebar' | 'centered' | 'header-band' | 'top-bar' | 'split' | 'creative';
  previewVariant: 'modern' | 'professional' | 'elegant';
}

interface TemplatePreviewData {
  candidate: {
    name: string;
    title: string;
    email: string;
    phone: string;
    location: string;
    linkedIn: string;
    portfolio: string;
  };
  summary: string;
  experience: Array<{
    role: string;
    company: string;
    period: string;
    bullets: string[];
  }>;
  education: Array<{
    degree: string;
    institution: string;
    year: string;
  }>;
  projects: Array<{
    name: string;
    context: string;
    impact: string;
  }>;
  skills: string[];
  tools: string[];
  certifications: string[];
  languages: Array<{
    name: string;
    level: string;
  }>;
  achievements: string[];
}

const INDUSTRIES = [
  'Administrative & Office',
  'Business & Management',
  'Creative & Design',
  'Customer Service & Retail',
  'Education & Academic',
  'Finance & Accounting',
  'Food Service & Hospitality',
  'Healthcare & Medical',
  'IT & Engineering',
  'Marketing & Sales',
  'Other',
] as const;

const EXPERIENCE_LEVELS = [
  'Student / Entry Level',
  'Mid Level',
  'Senior / Executive',
] as const;

const STYLES = [
  'Corporate',
  'Creative',
  'Elegant',
  'Modern',
  'Professional',
  'Simple',
  'Traditional',
] as const;

const LAYOUTS = [
  'One Column',
  'Two Column',
  'With Photo',
  'Without Photo',
  'One Page',
  'Two Page',
] as const;

const TAGS = ['free', 'popular', 'recommended'] as const;

const SAMPLE_PREVIEW: TemplatePreviewData = {
  candidate: {
    name: 'Maya Bennett',
    title: 'Senior Product Designer',
    email: 'maya.bennett@example.com',
    phone: '+1 (415) 555-0146',
    location: 'Austin, TX',
    linkedIn: 'linkedin.com/in/mayabennett',
    portfolio: 'mayabennett.design',
  },
  summary: 'Strategic product designer with 7+ years creating onboarding, workflow, and analytics experiences for SaaS teams. Combines design systems, research, accessibility, and stakeholder alignment to ship measurable product improvements.',
  experience: [
    {
      role: 'Lead Product Designer',
      company: 'Northstar Cloud',
      period: '2023 - Present',
      bullets: [
        'Redesigned the activation journey and improved trial conversion by 28%.',
        'Built a reusable design system adopted across three product squads.',
        'Partnered with product and engineering to reduce release rework by 22%.'
      ]
    },
    {
      role: 'Senior UX Designer',
      company: 'Brightlane Studio',
      period: '2020 - 2022',
      bullets: [
        'Delivered responsive web and mobile flows for fintech and healthcare clients.',
        'Led stakeholder workshops and translated discovery findings into high-conviction prototypes.'
      ]
    },
    {
      role: 'Product Designer',
      company: 'Pixel Harbor',
      period: '2018 - 2020',
      bullets: [
        'Designed customer self-service experiences that reduced support tickets by 18%.'
      ]
    }
  ],
  education: [
    {
      degree: 'B.A. Graphic Design',
      institution: 'University of Washington',
      year: '2018'
    }
  ],
  projects: [
    {
      name: 'Enterprise Analytics Redesign',
      context: 'SaaS dashboard modernization',
      impact: 'Improved task completion for core reporting flows by 31%.'
    },
    {
      name: 'Mobile Onboarding Optimization',
      context: 'Growth and activation initiative',
      impact: 'Reduced first-session drop-off and increased activation quality.'
    }
  ],
  skills: [
    'Product Strategy',
    'Design Systems',
    'User Research',
    'Interaction Design',
    'Accessibility',
    'Cross-Functional Leadership'
  ],
  tools: ['Figma', 'FigJam', 'Adobe CC', 'Maze', 'Notion', 'Jira'],
  certifications: [
    'Google UX Design Certificate',
    'IAAP Accessibility Fundamentals',
    'Nielsen Norman Group UX Certification'
  ],
  languages: [
    { name: 'English', level: 'Native' },
    { name: 'Spanish', level: 'Professional' },
    { name: 'French', level: 'Conversational' }
  ],
  achievements: [
    'Speaker at Design Systems Summit 2025',
    'Mentored 4 junior designers into senior-track roles'
  ],
};

@Component({
  selector: 'app-step-template',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './step-template.component.html',
  styleUrl: './step-template.component.scss'
})
export class StepTemplateComponent {
  pipeline = inject(PipelineStateService);
  readonly previewData = SAMPLE_PREVIEW;

  readonly filterIndustry = signal<string[]>([]);
  readonly filterExperience = signal<string[]>([]);
  readonly filterStyle = signal<string[]>([]);
  readonly filterLayout = signal<string[]>([]);
  readonly filterTag = signal<string[]>([]);
  readonly filterColor = signal<string[]>([]);

  readonly activeDropdown = signal<string | null>(null);

  readonly sortBy = signal<'recommended' | 'popular' | 'newest'>('recommended');

  readonly industries = INDUSTRIES;
  readonly experienceLevels = EXPERIENCE_LEVELS;
  readonly styles = STYLES;
  readonly layouts = LAYOUTS;
  readonly tags = TAGS;
  readonly colors = ['Blue', 'Green', 'Amber', 'Rose', 'Navy', 'Purple', 'Slate', 'Burgundy', 'Teal'] as const;

  readonly templates: CvTemplate[] = [
    {
      id: 'modern',
      name: 'Modern',
      description: 'Modern • IT & Engineering • Blue Accent',
      badge: { label: 'popular', variant: 'primary' },
      industry: 'IT & Engineering',
      experience: 'Mid Level',
      style: 'Modern',
      layout: 'Two Column',
      tag: 'popular',
      accent: 'Blue',
      previewType: 'sidebar',
      previewVariant: 'modern',
    },
    {
      id: 'classic',
      name: 'Classic',
      description: 'Traditional • Finance & Accounting • Amber',
      badge: { label: 'recommended', variant: 'secondary' },
      industry: 'Finance & Accounting',
      experience: 'Mid Level',
      style: 'Corporate',
      layout: 'One Column',
      tag: 'recommended',
      accent: 'Amber',
      previewType: 'centered',
      previewVariant: 'professional',
    },
    {
      id: 'executive',
      name: 'Executive',
      description: 'Professional • Business & Management • Navy Accent',
      badge: { label: 'recommended', variant: 'secondary' },
      industry: 'Business & Management',
      experience: 'Senior / Executive',
      style: 'Professional',
      layout: 'One Page',
      tag: 'recommended',
      accent: 'Navy',
      previewType: 'split',
      previewVariant: 'professional',
    },
    {
      id: 'pro',
      name: 'Pro',
      description: 'Modern • IT & Engineering • Green Accent',
      badge: { label: 'popular', variant: 'primary' },
      industry: 'IT & Engineering',
      experience: 'Mid Level',
      style: 'Modern',
      layout: 'Two Column',
      tag: 'popular',
      accent: 'Green',
      previewType: 'sidebar',
      previewVariant: 'modern',
    },
    {
      id: 'elegant',
      name: 'Elegant',
      description: 'Elegant • Education & Academic • Burgundy Accent',
      industry: 'Education & Academic',
      experience: 'Senior / Executive',
      style: 'Elegant',
      layout: 'Two Page',
      tag: 'recommended',
      accent: 'Burgundy',
      previewType: 'centered',
      previewVariant: 'elegant',
    },
  ];

  get filteredTemplates(): CvTemplate[] {
    const industries = this.filterIndustry();
    const experiences = this.filterExperience();
    const styles = this.filterStyle();
    const layouts = this.filterLayout();
    const tags = this.filterTag();
    const colors = this.filterColor();

    return this.templates.filter(t => {
      if (industries.length > 0 && !industries.includes(t.industry)) return false;
      if (experiences.length > 0 && !experiences.includes(t.experience)) return false;
      if (styles.length > 0 && !styles.includes(t.style)) return false;
      if (layouts.length > 0 && !layouts.includes(t.layout)) return false;
      if (tags.length > 0 && !tags.includes(t.tag)) return false;
      if (colors.length > 0 && !colors.includes(t.accent)) return false;
      return true;
    });
  }

  toggleDropdown(name: string): void {
    this.activeDropdown.update(curr => curr === name ? null : name);
  }

  toggleFilter(filter: WritableSignal<string[]>, val: string): void {
    filter.update(list =>
      list.includes(val) ? list.filter(v => v !== val) : [...list, val]
    );
  }

  clearFilters(): void {
    this.filterIndustry.set([]);
    this.filterExperience.set([]);
    this.filterStyle.set([]);
    this.filterLayout.set([]);
    this.filterTag.set([]);
    this.filterColor.set([]);
  }

  get hasActiveFilters(): boolean {
    return this.filterIndustry().length > 0
      || this.filterExperience().length > 0
      || this.filterStyle().length > 0
      || this.filterLayout().length > 0
      || this.filterTag().length > 0
      || this.filterColor().length > 0;
  }

  selectTemplate(id: string): void {
    this.pipeline.selectedTemplateId.set(id);
  }

  next(): void {
    this.pipeline.markStepDone(3);
    this.pipeline.goToStep(4);
  }

  back(): void {
    this.pipeline.goToStep(2);
  }
}
