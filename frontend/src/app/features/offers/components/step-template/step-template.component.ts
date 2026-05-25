import { Component, OnInit, inject, signal, WritableSignal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PipelineStateService } from '../../../../services/pipeline-state.service';
import { CvTemplateDto, OfferApiService, ResumePipelineResponse } from '../../services/offer-api.service';
import { ProfileService } from '../../../../services/profile.service';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../../../environments/environment';
import { SignalRService } from '../../../../services/signalr.service';

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
export class StepTemplateComponent implements OnInit {
  pipeline = inject(PipelineStateService);
  private readonly offerApi = inject(OfferApiService);
  private readonly profileService = inject(ProfileService);
  private readonly signalR = inject(SignalRService);
  private readonly thumbnailUrlCache = new Map<string, string>();
  private readonly thumbnailNonce = Date.now();

  readonly templates = signal<CvTemplateDto[]>([]);
  readonly isLoadingTemplates = signal<boolean>(false);
  readonly templateLoadError = signal<string | null>(null);

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

  ngOnInit(): void {
    this.loadTemplates();
  }

  loadTemplates(): void {
    this.isLoadingTemplates.set(true);
    this.templateLoadError.set(null);

    this.offerApi.getCvTemplates().subscribe({
      next: (templates) => {
        const htmlTemplateSlugs = new Set(['modern', 'latex']);
        const availableTemplates = (templates ?? [])
          .filter(template => htmlTemplateSlugs.has(template.slug))
          .sort((a, b) => this.sortRank(a) - this.sortRank(b));

        this.templates.set(availableTemplates);
        if (!this.pipeline.selectedTemplateId() && availableTemplates.length > 0) {
          this.selectTemplate(availableTemplates[0].slug);
        }
        this.isLoadingTemplates.set(false);
      },
      error: (err) => {
        console.warn('[CV-PIPELINE] template catalog unavailable', err);
        this.templateLoadError.set('Impossible de charger les templates HTML/CSS depuis le backend.');
        this.templates.set([]);
        this.isLoadingTemplates.set(false);
      }
    });
  }

  templateThumbnailUrl(slug: string): string {
    const cached = this.thumbnailUrlCache.get(slug);
    if (cached) return cached;

    const origin = new URL(environment.apiBaseUrl).origin;
    const url = `${origin}/api/cv/templates/${encodeURIComponent(slug)}/thumbnail?v=${this.thumbnailNonce}`;
    this.thumbnailUrlCache.set(slug, url);
    return url;
  }

  get filteredTemplates(): CvTemplateDto[] {
    const industries = this.filterIndustry();
    const experiences = this.filterExperience();
    const styles = this.filterStyle();
    const layouts = this.filterLayout();
    const tags = this.filterTag();
    const colors = this.filterColor();

    return this.templates().filter(t => {
      if (industries.length > 0 && !t.industries.some(industry => industries.includes(industry))) return false;
      if (experiences.length > 0 && !t.experienceLevels.some(experience => experiences.includes(experience))) return false;
      if (styles.length > 0 && !styles.includes(t.style)) return false;
      if (layouts.length > 0 && !t.layoutFlags.some(layout => layouts.includes(layout))) return false;
      if (tags.length > 0 && !t.tags.some(tag => tags.includes(tag.toLowerCase()))) return false;
      if (colors.length > 0 && !colors.includes(this.colorLabel(t.backgroundColor))) return false;
      return true;
    }).sort((a, b) => this.sortRank(a) - this.sortRank(b));
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

  selectTemplate(slug: string): void {
    this.pipeline.selectedTemplateId.set(slug);
  }

  isRecommended(template: CvTemplateDto): boolean {
    return template.tags.some(tag => tag.toLowerCase() === 'recommended');
  }

  isPopular(template: CvTemplateDto): boolean {
    return template.tags.some(tag => tag.toLowerCase() === 'popular');
  }

  colorLabel(hex: string | null | undefined): string {
    const normalized = String(hex ?? '').toLowerCase();
    const pairs: Record<string, string> = {
      '#2d3a8c': 'Navy',
      '#111827': 'Slate',
      '#1b2a4a': 'Navy',
      '#11610c': 'Green',
      '#18a7a0': 'Teal',
      '#6f42c1': 'Purple',
      '#2f9be5': 'Blue',
      '#c65b1b': 'Amber',
      '#be3b7b': 'Rose',
      '#b93317': 'Burgundy',
    };
    return pairs[normalized] ?? 'Slate';
  }

  async next(): Promise<void> {
    const offerId = this.pipeline.currentOfferId();
    if (!offerId) {
      this.pipeline.pipelineError.set('Offre introuvable pour la generation CV.');
      return;
    }

    const selected = this.pipeline.selectedTemplateId();
    const templateId = this.agentTemplateId(selected);

    this.pipeline.pipelineError.set(null);
    this.pipeline.markStepDone(2);
    this.pipeline.goToStep(4);
    this.pipeline.setLoading(true, 'Generation CV (cv_optimizer + cv_engine) en cours...');

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
            ?? this.buildFallbackCv(profileForFallback, current);

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
        this.pipeline.pipelineError.set(err?.error?.message || err?.message || 'Echec generation CV.');
      }
    });
  }

  back(): void {
    this.pipeline.goToStep(2);
  }

  private sortRank(template: CvTemplateDto): number {
    if (this.sortBy() === 'popular') return this.isPopular(template) ? 0 : 1;
    if (this.sortBy() === 'newest') return template.slug === 'modern' ? 0 : 1;
    return this.isRecommended(template) ? 0 : 1;
  }

  private agentTemplateId(slug: string | null): number {
    // The agent endpoint still accepts numeric IDs; the UI/editor use HTML template slugs.
    return slug === 'modern' ? 4 : 1;
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

    const firstName = personal?.prenom ?? personal?.firstName ?? personal?.first_name ?? source?.firstName ?? source?.first_name ?? '';
    const lastName = personal?.nom ?? personal?.lastName ?? personal?.last_name ?? source?.lastName ?? source?.last_name ?? '';
    const fullName = [firstName, lastName].filter(Boolean).join(' ').trim()
      || personal?.nomComplet
      || personal?.fullName
      || source?.nomComplet
      || source?.fullName
      || source?.name
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
        photoUrl: personal?.photoUrl
          ?? personal?.photo_url
          ?? personal?.profilePhoto
          ?? personal?.profile_photo
          ?? personal?.avatar
          ?? source?.photoUrl
          ?? source?.photo_url
          ?? source?.profilePhoto
          ?? source?.profile_photo
          ?? source?.avatar
          ?? null,
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
        const bullets = this.toBullets(
          project?.bullets
          ?? project?.taches
          ?? project?.missions
          ?? project?.tasks
          ?? project?.responsibilities
          ?? project?.realisations
          ?? project?.description
        );
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
