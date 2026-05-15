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

@Component({
  selector: 'app-step-template',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './step-template.component.html',
  styleUrl: './step-template.component.scss'
})
export class StepTemplateComponent {
  pipeline = inject(PipelineStateService);

  readonly filterIndustry = signal<string[]>([]);
  readonly filterExperience = signal<string[]>([]);
  readonly filterStyle = signal<string[]>([]);
  readonly filterLayout = signal<string[]>([]);
  readonly filterTag = signal<string[]>([]);

  readonly sortBy = signal<'recommended' | 'popular' | 'newest'>('recommended');

  readonly industries = INDUSTRIES;
  readonly experienceLevels = EXPERIENCE_LEVELS;
  readonly styles = STYLES;
  readonly layouts = LAYOUTS;
  readonly tags = TAGS;

  readonly templates: CvTemplate[] = [
    {
      id: 'executive-diamond',
      name: 'Executive Diamond',
      description: 'Modern • IT & Engineering • Green Accent',
      badge: { label: 'popular', variant: 'primary' },
      industry: 'IT & Engineering',
      experience: 'Senior / Executive',
      style: 'Modern',
      layout: 'Two Column',
      tag: 'popular',
      accent: 'Green',
      previewType: 'sidebar',
    },
    {
      id: 'corporate-standard',
      name: 'Corporate Standard',
      description: 'Traditional • Finance & Accounting • Amber',
      badge: { label: 'recommended', variant: 'secondary' },
      industry: 'Finance & Accounting',
      experience: 'Mid Level',
      style: 'Corporate',
      layout: 'One Column',
      tag: 'recommended',
      accent: 'Amber',
      previewType: 'centered',
    },
    {
      id: 'minimalist-centered',
      name: 'Minimalist Centered',
      description: 'Creative • Creative & Design • Rose',
      industry: 'Creative & Design',
      experience: 'Student / Entry Level',
      style: 'Creative',
      layout: 'With Photo',
      tag: 'free',
      accent: 'Rose',
      previewType: 'header-band',
    },
    {
      id: 'minimalist-modern',
      name: 'Minimalist Modern',
      description: 'Clean • IT & Engineering • Blue Accent',
      badge: { label: 'popular', variant: 'primary' },
      industry: 'IT & Engineering',
      experience: 'Mid Level',
      style: 'Modern',
      layout: 'Two Column',
      tag: 'popular',
      accent: 'Blue',
      previewType: 'top-bar',
    },
    {
      id: 'professional-executive',
      name: 'Professional Executive',
      description: 'Elegant • Finance & Accounting • Navy Accent',
      badge: { label: 'recommended', variant: 'secondary' },
      industry: 'Finance & Accounting',
      experience: 'Senior / Executive',
      style: 'Professional',
      layout: 'One Page',
      tag: 'recommended',
      accent: 'Navy',
      previewType: 'split',
    },
    {
      id: 'creative-portfolio',
      name: 'Creative Portfolio',
      description: 'Creative • Marketing & Sales • Purple/Pink Accent',
      industry: 'Marketing & Sales',
      experience: 'Student / Entry Level',
      style: 'Creative',
      layout: 'Without Photo',
      tag: 'free',
      accent: 'Purple',
      previewType: 'creative',
    },
    {
      id: 'simple-clean',
      name: 'Simple Clean',
      description: 'Simple • Business & Management • Slate Accent',
      industry: 'Business & Management',
      experience: 'Mid Level',
      style: 'Simple',
      layout: 'One Column',
      tag: 'free',
      accent: 'Slate',
      previewType: 'top-bar',
    },
    {
      id: 'elegant-academic',
      name: 'Elegant Academic',
      description: 'Elegant • Education & Academic • Burgundy Accent',
      industry: 'Education & Academic',
      experience: 'Senior / Executive',
      style: 'Elegant',
      layout: 'Two Page',
      tag: 'recommended',
      accent: 'Burgundy',
      previewType: 'centered',
    },
    {
      id: 'retail-ready',
      name: 'Retail Ready',
      description: 'Clean • Customer Service & Retail • Teal Accent',
      industry: 'Customer Service & Retail',
      experience: 'Student / Entry Level',
      style: 'Simple',
      layout: 'With Photo',
      tag: 'free',
      accent: 'Teal',
      previewType: 'sidebar',
    },
  ];

  get filteredTemplates(): CvTemplate[] {
    const industries = this.filterIndustry();
    const experiences = this.filterExperience();
    const styles = this.filterStyle();
    const layouts = this.filterLayout();
    const tags = this.filterTag();

    return this.templates.filter(t => {
      if (industries.length > 0 && !industries.includes(t.industry)) return false;
      if (experiences.length > 0 && !experiences.includes(t.experience)) return false;
      if (styles.length > 0 && !styles.includes(t.style)) return false;
      if (layouts.length > 0 && !layouts.includes(t.layout)) return false;
      if (tags.length > 0 && !tags.includes(t.tag)) return false;
      return true;
    });
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
  }

  get hasActiveFilters(): boolean {
    return this.filterIndustry().length > 0
      || this.filterExperience().length > 0
      || this.filterStyle().length > 0
      || this.filterLayout().length > 0
      || this.filterTag().length > 0;
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
