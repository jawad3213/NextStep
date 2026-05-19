import { Component, inject, signal, WritableSignal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PipelineStateService } from '../../../../services/pipeline-state.service';
import { OfferApiService, ResumePipelineResponse } from '../../services/offer-api.service';
import { ProfileService } from '../../../../services/profile.service';
import { firstValueFrom } from 'rxjs';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { environment } from '../../../../../environments/environment';
import { SignalRService } from '../../../../services/signalr.service';

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
  previewVariant: 'modern' | 'professional' | 'elegant' | 'latex';
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
  private readonly offerApi = inject(OfferApiService);
  private readonly profileService = inject(ProfileService);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly signalR = inject(SignalRService);
  readonly previewData = SAMPLE_PREVIEW;
  private readonly thumbnailUrlCache = new Map<string, SafeResourceUrl>();
  private readonly backendThumbnailSlugs = new Set<string>(['modern', 'latex']);
  private readonly thumbnailNonce = Date.now();

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
      id: 'latex',
      name: 'LaTeX Tech',
      description: 'Traditional • ATS-friendly • Classic engineering structure',
      badge: { label: 'recommended', variant: 'secondary' },
      industry: 'IT & Engineering',
      experience: 'Mid Level',
      style: 'Traditional',
      layout: 'One Column',
      tag: 'recommended',
      accent: 'Slate',
      previewType: 'centered',
      previewVariant: 'latex',
    },
    {
      id: 'modern',
      name: 'Modern',
      description: 'Professional • Split Layout • Navy Accent',
      badge: { label: 'popular', variant: 'primary' },
      industry: 'IT & Engineering',
      experience: 'Mid Level',
      style: 'Modern',
      layout: 'Two Column',
      tag: 'popular',
      accent: 'Navy',
      previewType: 'sidebar',
      previewVariant: 'modern',
    },
  ];

  hasBackendThumbnail(slug: string): boolean {
    return this.backendThumbnailSlugs.has(slug);
  }

  templateThumbnailUrl(slug: string): SafeResourceUrl {
    const cached = this.thumbnailUrlCache.get(slug);
    if (cached) return cached;

    const origin = new URL(environment.apiBaseUrl).origin;
    const safeUrl = this.sanitizer.bypassSecurityTrustResourceUrl(
      `${origin}/api/cv/templates/${encodeURIComponent(slug)}/thumbnail?v=${this.thumbnailNonce}#toolbar=0&navpanes=0&scrollbar=0`
    );
    this.thumbnailUrlCache.set(slug, safeUrl);
    return safeUrl;
  }

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

  async next(): Promise<void> {
    const offerId = this.pipeline.currentOfferId();
    if (!offerId) {
      this.pipeline.pipelineError.set('Offre introuvable pour la génération CV.');
      return;
    }

    const selected = this.pipeline.selectedTemplateId();
    const templateIdMap: Record<string, number> = {
      chrono: 1,
      latex: 1,
      elegant: 2,
      circular: 3,
      modern: 4,
      luxe: 5,
    };
    const templateId = templateIdMap[selected] ?? 1;

    this.pipeline.pipelineError.set(null);
    this.pipeline.markStepDone(2);
    this.pipeline.goToStep(4);
    this.pipeline.setLoading(true, 'Génération CV (cv_optimizer + cv_engine) en cours...');

    this.pipeline.currentAgentProgress.set({
      step: 'generating_cv',
      agentName: 'cv_optimizer',
      label: 'Lancement de la generation du CV...',
      status: 'running',
      progressPercent: 5
    });

    try {
      await this.signalR.joinOfferGroup(offerId);
    } catch (err) {
      console.warn('[CV-PIPELINE] SignalR join failed, falling back to polling only', err);
    }

    const localProfile = await firstValueFrom(this.profileService.getFullProfile()).catch((err) => {
      console.warn('[CV-PIPELINE] profile fallback unavailable', err);
      return null;
    });

    this.offerApi.resumePipeline(offerId, templateId).subscribe({
      next: (res: ResumePipelineResponse) => {
        const current = this.pipeline.pipelineResult();
        if (current) {
          const profileForFallback = res?.profileData
            ?? res?.profile_data
            ?? current.profileData
            ?? localProfile;

          const generatedCv = res?.['cvGeneratedContent']
            ?? res?.cv_data
            ?? res?.cvData
            ?? res?.cv_optimized_content
            ?? res?.cvOptimizedContent
            ?? this.buildFallbackCv(
              profileForFallback,
              current
            );

          this.pipeline.setResult({
            ...current,
            profileData: profileForFallback ?? current.profileData,
            cvGeneratedContent: generatedCv,
            emailSubject: res?.email_subject ?? current.emailSubject ?? '',
            emailBody: res?.email_body ?? current.emailBody ?? '',
            recruiterName: res?.recruiter_name ?? current.recruiterName ?? '',
          });
        }
        this.pipeline.setLoading(false);
        this.pipeline.currentAgentProgress.set({
          step: 'generating_cv',
          agentName: 'db_persist',
          label: 'CV genere, ouverture de l editeur...',
          status: 'done',
          progressPercent: 100
        });
      },
      error: (err) => {
        this.pipeline.setLoading(false);
        this.pipeline.pipelineError.set(err?.error?.message || err?.message || 'Échec génération CV.');
      }
    });
  }

  back(): void {
    this.pipeline.goToStep(2);
  }

  private buildFallbackCv(profile: any, current: any): any {
    const source = profile?.data ?? profile?.profile ?? profile ?? {};
    const personal = source?.personalInfo ?? source?.personal_info ?? source?.personal ?? {};
    const experiences = this.asArray(source?.experiences);
    const projects = this.asArray(source?.projets ?? source?.projects);
    const formations = this.asArray(source?.formations ?? source?.education);
    const competences = this.asArray(source?.competences ?? source?.skills);
    const certifications = this.asArray(source?.certifications ?? source?.certificats);
    const matchedSkills = new Set(this.asArray(current?.matchingSkills).map((s: any) => this.normalizeText(s)));

    const firstName = personal?.prenom ?? personal?.firstName ?? personal?.first_name ?? '';
    const lastName = personal?.nom ?? personal?.lastName ?? personal?.last_name ?? '';
    const fullName = [firstName, lastName].filter(Boolean).join(' ').trim()
      || personal?.nomComplet
      || personal?.fullName
      || 'Candidat';
    const location = [personal?.ville ?? personal?.city, personal?.pays ?? personal?.country]
      .filter(Boolean)
      .join(', ');

    const technicalSkills = competences
      .filter((skill: any) => {
        const category = this.normalizeText(skill?.categorie ?? skill?.category ?? '');
        return category !== 'langue' && category !== 'language' && category !== 'certification';
      })
      .map((skill: any) => {
        const name = skill?.nom ?? skill?.name ?? skill?.label ?? '';
        return {
          name,
          level: this.toSkillLevel(skill?.niveau ?? skill?.level),
          isMatched: matchedSkills.has(this.normalizeText(name)),
        };
      })
      .filter((skill: any) => !!skill.name);

    const languages = competences
      .filter((skill: any) => {
        const category = this.normalizeText(skill?.categorie ?? skill?.category ?? '');
        return category === 'langue' || category === 'language';
      })
      .map((skill: any) => {
        const name = skill?.nom ?? skill?.name ?? skill?.label ?? '';
        const level = skill?.niveau ?? skill?.level ?? skill?.proficiency ?? '';
        return [name, level ? `(${level})` : ''].filter(Boolean).join(' ');
      })
      .filter(Boolean);

    const certificationNames = [
      ...certifications.map((cert: any) => {
        const name = cert?.nom ?? cert?.name ?? cert?.titre ?? cert?.title ?? '';
        const issuer = cert?.organisme ?? cert?.issuer ?? cert?.provider ?? '';
        return [name, issuer ? `- ${issuer}` : ''].filter(Boolean).join(' ');
      }),
      ...competences
        .filter((skill: any) => this.normalizeText(skill?.categorie ?? skill?.category ?? '') === 'certification')
        .map((skill: any) => skill?.nom ?? skill?.name ?? ''),
    ].filter(Boolean);

    return {
      candidate: {
        name: fullName,
        email: personal?.email ?? personal?.mail ?? '',
        phone: personal?.telephone ?? personal?.phone ?? '',
        location,
        title: personal?.titrePoste ?? personal?.title ?? current?.offerTitle ?? '',
        linkedIn: personal?.lienLinkedin ?? personal?.linkedin ?? personal?.linkedIn ?? null,
        gitHub: personal?.lienGithub ?? personal?.github ?? personal?.gitHub ?? null,
        portfolio: personal?.lienPortfolio ?? personal?.portfolio ?? null,
        photoUrl: personal?.photoUrl ?? personal?.photo_url ?? null,
      },
      summary: personal?.resumeProfessionnel
        ?? personal?.summary
        ?? source?.objectif
        ?? current?.descriptionPoste
        ?? '',
      experience: experiences.map((exp: any) => ({
        role: exp?.poste ?? exp?.titre ?? exp?.role ?? 'Experience',
        company: exp?.entreprise ?? exp?.company ?? '',
        start: this.toMonthValue(exp?.dateDebut ?? exp?.start),
        end: this.toMonthValue(exp?.dateFin ?? exp?.end),
        bullets: this.toBullets(exp?.taches ?? exp?.missions ?? exp?.description),
      })),
      education: formations.map((form: any) => ({
        degree: [form?.diplome ?? form?.titre ?? form?.degree, form?.specialisation ?? form?.speciality]
          .filter(Boolean)
          .join(' - ') || 'Formation',
        institution: form?.etablissement ?? form?.ecole ?? form?.institution ?? '',
        year: String(form?.anneeFin ?? form?.annee ?? form?.dateFin ?? form?.year ?? ''),
      })),
      skills: technicalSkills,
      projects: projects.map((project: any) => {
        const technologies = this.asArray(project?.technologies).join(', ');
        const bullets = this.toBullets(project?.taches ?? project?.description);
        if (technologies) bullets.push(`Technologies: ${technologies}`);
        return {
          title: project?.titreProjet ?? project?.titre ?? project?.title ?? 'Projet',
          description: project?.description ?? '',
          bullets,
        };
      }),
      certifications: certificationNames,
      languages,
      activities: [],
      atsScore: current?.atsScore ?? 0,
      matchingScore: current?.matchScore ?? 0,
      atsCoveragePct: current?.atsScore ?? 0,
    };
  }

  private asArray(value: any): any[] {
    return Array.isArray(value) ? value : [];
  }

  private normalizeText(value: any): string {
    return String(value ?? '')
      .trim()
      .toLowerCase()
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '');
  }

  private toSkillLevel(value: any): number {
    if (typeof value === 'number') return Math.min(5, Math.max(1, Math.round(value)));
    const normalized = this.normalizeText(value);
    if (['expert', 'avance', 'advanced', 'proficient', 'native'].includes(normalized)) return 5;
    if (['intermediaire', 'intermediate', 'upper-intermediate'].includes(normalized)) return 4;
    if (['elementaire', 'elementary', 'debutant', 'beginner'].includes(normalized)) return 2;
    return 3;
  }

  private toBullets(value: any): string[] {
    if (Array.isArray(value)) {
      return value.map(v => String(v).trim()).filter(Boolean);
    }
    const text = String(value ?? '').trim();
    if (!text) return ['A completer'];
    return text
      .split(/\r?\n|[.;]/)
      .map(part => part.trim())
      .filter(Boolean);
  }

  private toMonthValue(value: any): string {
    if (typeof value !== 'string') return '';
    const v = value.trim();
    if (!v) return '';
    const m = v.match(/^(\d{4})-(\d{2})/);
    if (m) return `${m[1]}-${m[2]}`;
    if (/^\d{4}-\d{2}$/.test(v)) return v;
    return '';
  }
}
