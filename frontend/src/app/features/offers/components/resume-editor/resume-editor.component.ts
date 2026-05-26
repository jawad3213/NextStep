import { Component, Input, Output, EventEmitter, signal, effect, OnInit, OnChanges, OnDestroy, SimpleChanges, inject, AfterViewInit, ElementRef, ViewChild } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CdkDragDrop, moveItemInArray, DragDropModule } from '@angular/cdk/drag-drop';
import { SafeHtml } from '@angular/platform-browser';
import { SidebarService } from '../../../../shared/services/sidebar.service';
import { CvDesignConfig } from '../../services/offer-api.service';
import { environment } from '../../../../../environments/environment';

export interface Candidate {
  name: string;
  email: string;
  phone: string;
  location: string;
  title: string;
  photoUrl: string | null;
  linkedIn: string | null;
  gitHub: string | null;
  portfolio: string | null;
}

export interface Experience {
  role: string;
  company: string;
  start: string;
  end: string;
  bullets: string[];
  relevance?: string;
  keywords?: string[];
}

export interface Education {
  degree: string;
  institution: string;
  year: string;
  startYear?: string;
  endYear?: string;
}

export interface Skill {
  name: string;
  level: number;
  isMatched: boolean;
  isHighlighted?: boolean;
  category?: string;
  typeCompetence?: string;
}

export interface Project {
  title: string;
  description?: string;
  technologies?: string[];
  dateRealisation?: string;
  bullets: string[];
  relevance?: string;
  keywords?: string[];
}

export interface Activity {
  title: string;
  role?: string | null;
  description?: string | null;
  startDate?: string | null;
  endDate?: string | null;
}

export interface CvGeneratedSchema {
  candidate: Candidate;
  summary: string;
  experience: Experience[];
  education: Education[];
  skills: Skill[];
  projects: Project[];
  certifications: string[];
  languages: string[];
  activities: Activity[];
  accomplishments: string[];
  atsScore: number;
  matchingScore: number;
  atsCoveragePct: number;
}

export interface CvSectionItem {
  primaryText: string;
  secondaryText?: string;
  startDate?: string | null;
  endDate?: string | null;
  location?: string | null;
  description?: string | null;
  level?: number | null;
  isMatched?: boolean;
  bullets: string[];
}

export interface CvSection {
  id: string;
  type: string;
  title: string;
  placement: 'main' | 'sidebar';
  isVisible: boolean;
  order: number;
  text?: string | null;
  items: CvSectionItem[];
}

interface PreviewSectionOverlay {
  id: string;
  title: string;
  top: number;
  left: number;
  width: number;
  height: number;
  order: number;
}


type EditorPanelTab = 'templates' | 'design' | 'sections';
type SupportedEditorTemplate = 'modern' | 'latex';

@Component({
  selector: 'app-resume-editor',
  standalone: true,
  imports: [CommonModule, FormsModule, DragDropModule],
  providers: [DatePipe],
  templateUrl: './resume-editor.component.html',
  styleUrls: ['./resume-editor.component.scss']
})
export class ResumeEditorComponent implements OnInit, OnChanges, AfterViewInit, OnDestroy {
  @Input() templateId: string = 'modern';
  @Input() offerId: string | null = null;
  @Input() initialData: any = null;
  @Input() renderedHtml: SafeHtml | null = null;
  @Input() designConfig: CvDesignConfig | null = null;
  @Input() isRenderingPreview: boolean = false;
  @Input() previewError: string | null = null;
  @Input() isReoptimizingCv: boolean = false;
  @Input() isGeneratingHighQualityPdf: boolean = false;
  @Input() isSavingFinal: boolean = false;
  @Output() back = new EventEmitter<void>();
  @Output() continue = new EventEmitter<void>();
  @Output() finishRequested = new EventEmitter<void>();
  @Output() download = new EventEmitter<void>();
  @Output() reoptimize = new EventEmitter<void>();
  @Output() dataChange = new EventEmitter<CvGeneratedSchema>();
  @Output() designConfigChange = new EventEmitter<CvDesignConfig>();
  @Output() templateChange = new EventEmitter<SupportedEditorTemplate>();
  @ViewChild('renderedPreviewContent')
  set renderedPreviewContent(value: ElementRef<HTMLDivElement> | undefined) {
    this.renderedPreviewContentRef = value;
    if (value) {
      this.attachPreviewObservers();
      this.schedulePreviewOverlayRefresh();
      this.attachPreviewAssetListeners();
    } else {
      this.previewResizeObserver?.disconnect();
      this.previewMutationObserver?.disconnect();
      this.detachPreviewAssetListeners();
    }
  }
  private renderedPreviewContentRef?: ElementRef<HTMLDivElement>;
  private readonly sidebarService = inject(SidebarService);

  // DYNAMIC CHOSEN MODEL
  readonly activeTemplate = signal<string>('modern');
  readonly supportedTemplateIds: SupportedEditorTemplate[] = ['modern', 'latex'];
  readonly activePanelTab = signal<EditorPanelTab>('templates');
  readonly isPanelOpen = signal<boolean>(true);
  readonly activeAccentColor = signal<string>('green');
  readonly fontFamily = signal<string>("Inter, 'Segoe UI', Arial, sans-serif");
  readonly fontSize = signal<string>('14px');
  readonly lineSpacing = signal<string>('1.45');
  readonly sectionSpacing = signal<string>('1.2rem');
  readonly sidebarWidth = signal<string>('31%');
  readonly documentTitle = signal<string>('EL HAIL JAOUAD_Resume_4');
  readonly isEditingDocumentTitle = signal<boolean>(false);
  readonly draftDocumentTitle = signal<string>('EL HAIL JAOUAD_Resume_4');
  readonly activePreviewSection = signal<string | null>(null);
  readonly isSectionFormOpen = signal<boolean>(false);
  readonly editingSectionTitle = signal<string | null>(null);
  readonly sectionTitleOverrides = signal<Record<string, string>>({});
  readonly previewZoom = signal<number>(90);
  readonly previewSectionOverlays = signal<PreviewSectionOverlay[]>([]);
  readonly minPreviewZoom = 70;
  readonly maxPreviewZoom = 130;
  private readonly previewZoomStep = 10;
  private overlayRefreshFrame: number | null = null;
  private previewResizeObserver: ResizeObserver | null = null;
  private previewMutationObserver: MutationObserver | null = null;
  private previewAssetCleanup: Array<() => void> = [];

  // SIMULATED OPTIMIZATION LOADING
  readonly isImproving = signal<boolean>(false);

  // Section visibility toggles
  readonly sectionVisibility = signal<any>({
    summary: true,
    experience: true,
    projects: true,
    education: true,
    skills: true,
    softskills: true,
    certifications: true,
    languages: true,
    activities: true,
    accomplishments: true,
  });

  readonly sectionOrder = signal<string[]>([
    'summary',
    'experience',
    'projects',
    'education',
    'skills',
    'softskills',
    'certifications',
    'languages',
    'activities',
    'accomplishments',
  ]);

  readonly customSections = signal<CvSection[]>([]);

  // Draft saved indicator
  readonly lastSaved = signal<string | null>(null);
  private saveTimeout: any = null;

  // SEED THE EXACT RESUME DATA RETURNED BY AGENTS
  readonly cvData = signal<CvGeneratedSchema>({
    candidate: {
      name: "Nichan Said",
      email: "saidnichan6@gmail.com",
      phone: "+212 713 668 431",
      location: "Salé",
      title: "Développeur Full Stack",
      photoUrl: null,
      linkedIn: "linkedin.com/in/saidnichan",
      gitHub: "github.com/saidnichan",
      portfolio: "saidnichan.dev"
    },
    summary: "Développeur Full Stack passionné par la création d'applications web évolutives, l'intégration d'IA et l'automatisation. Je recherche activement un poste de Développeur Full Stack Python / React pour mettre en pratique mes compétences et acquérir de nouvelles expériences.",
    experience: [
      {
        role: "Stage Développeur Full-Stack",
        company: "Smart Automation Technologie",
        start: "2025-07",
        end: "2025-08",
        bullets: [
          "Contribution à l'amélioration et à la refonte d'une plateforme web marketplace en utilisant Angular, développement d'un chatbot IA basé sur l'approche RAG pour automatiser le support client, mise en place de l'automatisation des workflows avec n8n, collaboration au sein d'une équipe Agile avec gestion du versionning via GitLab."
        ]
      }
    ],
    education: [
      {
        degree: "Cycle d'Ingénieur – Génie Informatique",
        institution: "Ecole Nationale des Sciences Appliquées (ENSA) Tanger",
        year: "2024"
      },
      {
        degree: "Classes Préparatoires Intégrées",
        institution: "Ecole Nationale des Sciences Appliquées",
        year: "2022"
      }
    ],
    skills: [
      { name: "Python", level: 3, isMatched: true },
      { name: "React.js", level: 3, isMatched: true },
      { name: "TypeScript", level: 3, isMatched: true },
      { name: "Django REST", level: 3, isMatched: false },
      { name: "Docker", level: 3, isMatched: true },
      { name: "Git", level: 3, isMatched: true },
      { name: "CI/CD", level: 3, isMatched: true },
      { name: "DevOps", level: 3, isMatched: true },
      { name: "Kubernetes", level: 3, isMatched: true },
      { name: "FastAPI", level: 3, isMatched: true },
      { name: "PostgreSQL", level: 3, isMatched: true },
      { name: "HTML/CSS", level: 3, isMatched: true },
      { name: "Redis", level: 3, isMatched: true },
      { name: "Java", level: 3, isMatched: false },
      { name: "Node.js", level: 3, isMatched: true },
      { name: "Angular", level: 3, isMatched: true },
      { name: "Vue.js", level: 3, isMatched: true },
      { name: "GitLab", level: 3, isMatched: true },
      { name: "Linux", level: 3, isMatched: true },
      { name: "Agile/Scrum", level: 3, isMatched: true }
    ],
    projects: [
      {
        title: "Roadmap Builder",
        bullets: [
          "Conception et déploiement d'une plateforme interactive avec la Clean Architecture et une authentification sécurisée (JWT, Supabase), intégration d'un assistant IA (BMO) pour générer des roadmaps visuelles et gérer les nœuds structurés, mise en place du CI/CD (Vercel, Render) et gestion du projet avec la méthodologie Agile (Jira)."
        ]
      },
      {
        title: "FlowCom – Chat Temps Réel",
        bullets: [
          "Développement d'une application de messagerie similaire à WhatsApp avec gestion du temps réel et notifications, développement des API Backend et intégration d'une base de données NoSQL pour le stockage sécurisé des messages."
        ]
      },
      {
        title: "Soft Skills Evaluation Web App",
        bullets: [
          "Développement d'une application web pour l'évaluation des compétences sociales, assurance qualité (QA) et réalisation des phases de test unitaires et E2E."
        ]
      }
    ],
    certifications: [
      "Oracle Certified Professional : Java SE 17 Developer - Oracle",
      "Agile Project Management – Google (Coursera) - Google"
    ],
    languages: [
      "Arabe (Maternel)",
      "Anglais (Professionnel)",
      "Français (Courant)"
    ],
    activities: [
      {
        title: "Hackathon ENSA Winner",
        role: "Participant",
        description: "Won a university hackathon by presenting a production-ready full-stack prototype.",
        startDate: "2024-05",
        endDate: "2024-05",
      },
      {
        title: "Competitive Programming Club",
        role: "Active member",
        description: "Practiced weekly algorithmic problem solving and peer code reviews.",
        startDate: "2023-09",
        endDate: "2024-06",
      }
    ],
    accomplishments: [
      "Built full-stack and AI-powered projects with measurable delivery outcomes",
      "Recognized in hackathon and team-based technical competitions"
    ],
    atsScore: 85,
    matchingScore: 85,
    atsCoveragePct: 85.0
  });

  // ACTIVE ACCORDION SECTION STATE
  readonly activeSection = signal<string | null>('coordonnees');

  readonly sectionDefinitions = [
    { id: 'summary', label: 'Resume', placement: 'main' as const },
    { id: 'experience', label: 'Experiences', placement: 'main' as const },
    { id: 'projects', label: 'Projects', placement: 'main' as const },
    { id: 'certifications', label: 'Certifications', placement: 'main' as const },
    { id: 'activities', label: 'Activities', placement: 'main' as const },
    { id: 'accomplishments', label: 'Accomplishments', placement: 'main' as const },
    { id: 'education', label: 'Education', placement: 'sidebar' as const },
    { id: 'skills', label: 'Technical Skills', placement: 'sidebar' as const },
    { id: 'softskills', label: 'Soft Skills', placement: 'sidebar' as const },
    { id: 'languages', label: 'Languages', placement: 'sidebar' as const },
  ];

  readonly accentColorOptions = [
    { id: 'rainbow', label: 'Default', cssClass: 'rainbow' },
    { id: 'gray', label: 'Gray', cssClass: 'gray' },
    { id: 'navy', label: 'Navy', cssClass: 'navy' },
    { id: 'purple', label: 'Purple', cssClass: 'purple' },
    { id: 'blue', label: 'Blue', cssClass: 'blue' },
    { id: 'teal', label: 'Teal', cssClass: 'teal' },
    { id: 'green', label: 'Green', cssClass: 'green' },
    { id: 'red', label: 'Red', cssClass: 'red' },
    { id: 'empty', label: 'No color', cssClass: 'empty' },
  ] as const;

  readonly recommendedAccentColors = ['olive', 'teal', 'blue', 'orange', 'slate', 'green', 'red', 'pink', 'black'] as const;
  readonly fontFamilyOptions = [
    "Inter, 'Segoe UI', Arial, sans-serif",
    "'IBM Plex Sans', 'Segoe UI', Arial, sans-serif",
    "'Georgia', 'Times New Roman', serif"
  ];
  readonly fontSizeOptions = ['12px', '13px', '14px', '15px', '16px'];
  readonly lineSpacingOptions = ['1.25', '1.38', '1.45', '1.55', '1.7'];
  readonly sectionSpacingOptions = ['0.8rem', '1rem', '1.2rem', '1.4rem', '1.6rem'];
  readonly sidebarWidthOptions = ['26%', '29%', '31%', '34%', '37%'];
  private readonly templateThumbnailUrlCache = new Map<string, string>();
  private readonly templateThumbnailNonce = Date.now();
  private readonly previewOverlaySync = effect(() => {
    void this.renderedHtml;
    this.previewZoom();
    this.schedulePreviewOverlayRefresh();
  });

  toggleSection(section: string) {
    if (this.activeSection() === section) {
      this.activeSection.set(null); // Close if already open
    } else {
      this.activeSection.set(section); // Open the clicked one
    }
  }

  setTemplate(id: string) {
    if (!this.supportedTemplateIds.includes(id as SupportedEditorTemplate)) {
      return;
    }

    if (this.activeTemplate() === id) {
      return;
    }

    this.activeTemplate.set(id);
    this.templateChange.emit(id as SupportedEditorTemplate);
  }

  templateThumbnailUrl(slug: SupportedEditorTemplate): string {
    const cached = this.templateThumbnailUrlCache.get(slug);
    if (cached) return cached;
    const origin = new URL(environment.apiBaseUrl).origin;
    const url = `${origin}/api/cv/templates/${encodeURIComponent(slug)}/thumbnail?v=${this.templateThumbnailNonce}`;
    this.templateThumbnailUrlCache.set(slug, url);
    return url;
  }

  startDocumentTitleEdit(): void {
    this.draftDocumentTitle.set(this.documentTitle());
    this.isEditingDocumentTitle.set(true);
  }

  saveDocumentTitle(): void {
    const nextTitle = this.draftDocumentTitle().trim();
    if (nextTitle) {
      this.documentTitle.set(nextTitle);
    }
    this.isEditingDocumentTitle.set(false);
  }

  cancelDocumentTitleEdit(): void {
    this.draftDocumentTitle.set(this.documentTitle());
    this.isEditingDocumentTitle.set(false);
  }

  openPanelTab(tab: EditorPanelTab): void {
    this.activePanelTab.set(tab);
    this.isPanelOpen.set(true);
  }

  closePanel(): void {
    this.isPanelOpen.set(false);
  }

  togglePanel(): void {
    this.isPanelOpen.update((isOpen) => !isOpen);
  }

  panelTitle(): string {
    switch (this.activePanelTab()) {
      case 'design':
        return 'Design & formatting';
      case 'sections':
        return 'Add section';
      default:
        return 'Templates';
    }
  }

  selectAccentColor(colorId: string): void {
    this.activeAccentColor.set(colorId);
    this.emitDesignConfig();
  }

  accentColorHex(): string {
    const colors: Record<string, string> = {
      rainbow: '#11610c',
      gray: '#6f7781',
      navy: '#1d3f91',
      purple: '#6f42c1',
      blue: '#2f9be5',
      teal: '#18a7a0',
      green: '#11610c',
      red: '#b93317',
      empty: '#0f172a',
      olive: '#586c2f',
      orange: '#c65b1b',
      slate: '#334155',
      pink: '#be3b7b',
      black: '#111827',
    };
    return colors[this.activeAccentColor()] ?? colors['green'];
  }

  updateFontFamily(fontFamily: string): void {
    this.fontFamily.set(fontFamily);
    this.emitDesignConfig();
  }

  updateFontSize(fontSize: string): void {
    this.fontSize.set(fontSize);
    this.emitDesignConfig();
  }

  updateLineSpacing(lineSpacing: string): void {
    this.lineSpacing.set(lineSpacing);
    this.emitDesignConfig();
  }

  updateSectionSpacing(sectionSpacing: string): void {
    this.sectionSpacing.set(sectionSpacing);
    this.emitDesignConfig();
  }

  updateSidebarWidth(sidebarWidth: string): void {
    this.sidebarWidth.set(sidebarWidth);
    this.emitDesignConfig();
  }

  isSectionVisible(sectionId: string): boolean {
    return !!this.sectionVisibility()[this.normalizeSectionId(sectionId)];
  }

  setActivePreviewSection(sectionId: string): void {
    this.activePreviewSection.set(this.normalizeSectionId(sectionId));
  }

  selectedSection(): CvSection | null {
    if (this.activePreviewSection() === 'header') {
      return {
        id: 'header',
        type: 'header',
        title: 'Header',
        placement: 'main',
        isVisible: true,
        order: -1,
        items: [],
      };
    }
    return this.orderedSections.find(section => section.id === this.activePreviewSection()) ?? null;
  }

  openSectionInspector(sectionId: string): void {
    this.setActivePreviewSection(sectionId);
    this.isSectionFormOpen.set(true);
  }

  closeSectionForm(): void {
    this.isSectionFormOpen.set(false);
    this.activePreviewSection.set(null);
    this.schedulePreviewOverlayRefresh();
  }

  startSectionTitleEdit(sectionId: string): void {
    this.editingSectionTitle.set(sectionId);
    this.activePreviewSection.set(this.normalizeSectionId(sectionId));
  }

  stopSectionTitleEdit(): void {
    this.editingSectionTitle.set(null);
  }

  updateSectionTitle(sectionId: string, value: string): void {
    if (sectionId === 'header') {
      return;
    }
    if (this.isCustomSection(sectionId)) {
      this.updateCustomSectionTitle(sectionId, value.trimStart());
      return;
    }
    this.sectionTitleOverrides.update((titles) => ({
      ...titles,
      [this.normalizeSectionId(sectionId)]: value.trimStart(),
    }));
  }

  hidePreviewSection(sectionId: string): void {
    if (sectionId === 'header') {
      return;
    }
    if (this.isCustomSection(sectionId)) {
      this.toggleCustomSectionVisibility(sectionId);
    } else {
      this.toggleSectionVisibility(this.normalizeSectionId(sectionId));
    }
  }

  moveSection(sectionId: string, direction: -1 | 1): void {
    const order = [...this.sectionOrder()];
    const index = order.indexOf(sectionId);
    const nextIndex = index + direction;
    if (index < 0 || nextIndex < 0 || nextIndex >= order.length) return;
    moveItemInArray(order, index, nextIndex);
    this.sectionOrder.set(order);
  }

  canMoveSection(sectionId: string, direction: -1 | 1): boolean {
    const order = this.sectionOrder();
    const index = order.indexOf(sectionId);
    const nextIndex = index + direction;
    return index >= 0 && nextIndex >= 0 && nextIndex < order.length;
  }

  visibleMainSections(): CvSection[] {
    return this.orderedSections.filter(section => section.isVisible && section.placement === 'main');
  }

  visibleSidebarSections(): CvSection[] {
    return this.orderedSections.filter(section => section.isVisible && section.placement === 'sidebar');
  }

  visibleTemplateSections(): CvSection[] {
    return this.orderedSections.filter(section => section.isVisible);
  }

  sectionById(sectionId: string): CvSection | null {
    return this.orderedSections.find(section => section.id === sectionId) ?? null;
  }

  sectionItems(sectionId: string): CvSectionItem[] {
    return this.sectionById(sectionId)?.items ?? [];
  }

  contactLine(): string {
    return [
      this.cvData().candidate.email,
      this.cvData().candidate.phone,
      this.cvData().candidate.location,
      this.cvData().candidate.linkedIn,
      this.cvData().candidate.gitHub,
      this.cvData().candidate.portfolio,
    ]
      .filter((value) => !!String(value ?? '').trim())
      .join(' | ');
  }

  skillPercent(level: number | null | undefined): number {
    return Math.max(20, Math.min(100, (Number(level ?? 1) || 1) * 20));
  }

  previewSectionIcon(sectionId: string): string {
    const icons: Record<string, string> = {
      header: 'badge',
      summary: 'description',
      experience: 'work',
      projects: 'deployed_code',
      education: 'school',
      skills: 'bolt',
      softskills: 'psychology',
      certifications: 'workspace_premium',
      languages: 'translate',
      activities: 'interests',
      accomplishments: 'military_tech',
    };
    return icons[sectionId] ?? 'article';
  }

  activityLine(item: CvSectionItem): string {
    const title = String(item.primaryText ?? '').trim();
    const role = String(item.secondaryText ?? '').trim();
    if (title && role) return `${title} - ${role}`;
    return title || role;
  }

  languageLine(): string {
    return this.sectionItems('languages')
      .map((item) => String(item.primaryText ?? '').trim())
      .filter(Boolean)
      .join('   •   ');
  }

  toggleFocusMode(): void {
    this.sidebarService.toggleEditorFocusMode();
  }

  isFocusMode(): boolean {
    return this.sidebarService.editorFocusModeValue;
  }

  onBack() {
    this.back.emit();
  }

  get orderedSections(): CvSection[] {
    const standard = this.sectionOrder().map((id, index) => this.buildStandardSection(id, index)).filter(Boolean) as CvSection[];
    const custom = [...this.customSections()]
      .sort((a, b) => a.order - b.order)
      .map((section) => ({ ...section, order: section.order + standard.length }));
    return [...standard, ...custom];
  }

  private buildStandardSection(id: string, order: number): CvSection | null {
    const data = this.cvData();
    const visibility = this.sectionVisibility();
    const def = this.sectionDefinitions.find(section => section.id === id);
    if (!def) return null;

    switch (id) {
      case 'summary':
        return {
          id,
          type: id,
          title: this.sectionLabel(id),
          placement: def.placement,
          isVisible: !!visibility[id],
          order,
          text: data.summary,
          items: [],
        };
      case 'experience':
        return {
          id,
          type: id,
          title: this.sectionLabel(id),
          placement: def.placement,
          isVisible: !!visibility[id],
          order,
          items: data.experience.map(exp => ({
            primaryText: exp.role,
            secondaryText: exp.company,
            startDate: exp.start,
            endDate: exp.end,
            bullets: [...exp.bullets],
          })),
        };
      case 'projects':
        return {
          id,
          type: id,
          title: this.sectionLabel(id),
          placement: def.placement,
          isVisible: !!visibility[id],
          order,
          items: data.projects.map(project => ({
            primaryText: project.title,
            secondaryText: Array.isArray(project.technologies) ? project.technologies.join(', ') : '',
            startDate: project.dateRealisation ?? null,
            description: project.description ?? null,
            bullets: [...project.bullets],
          })),
        };
      case 'education':
        return {
          id,
          type: id,
          title: this.sectionLabel(id),
          placement: def.placement,
          isVisible: !!visibility[id],
          order,
          items: data.education.map(education => ({
            primaryText: education.degree,
            secondaryText: education.institution,
            startDate: education.startYear ?? null,
            endDate: education.endYear ?? null,
            description: education.year,
            bullets: [],
          })),
        };
      case 'skills':
      case 'softskills':
        const skillCategory = id === 'softskills' ? 'soft' : 'technical';
        return {
          id,
          type: id,
          title: this.sectionLabel(id),
          placement: def.placement,
          isVisible: !!visibility[id],
          order,
          items: this.getSkillsBySection(skillCategory, data.skills).map(skill => ({
            primaryText: skill.name,
            secondaryText: skill.category ?? '',
            level: skill.level,
            isMatched: skill.isMatched,
            bullets: [],
          })),
        };
      case 'certifications':
        return {
          id,
          type: id,
          title: this.sectionLabel(id),
          placement: def.placement,
          isVisible: !!visibility[id],
          order,
          items: data.certifications.map(certification => ({
            primaryText: certification,
            secondaryText: '',
            bullets: [],
          })),
        };
      case 'languages':
        return {
          id,
          type: id,
          title: this.sectionLabel(id),
          placement: def.placement,
          isVisible: !!visibility[id],
          order,
          items: data.languages.map(language => ({
            primaryText: language,
            secondaryText: '',
            bullets: [],
          })),
        };
      case 'activities':
        return {
          id,
          type: id,
          title: this.sectionLabel(id),
          placement: def.placement,
          isVisible: !!visibility[id],
          order,
          items: data.activities.map(activity => ({
            primaryText: activity.title,
            secondaryText: activity.role ?? '',
            startDate: activity.startDate ?? null,
            endDate: activity.endDate ?? null,
            description: activity.description ?? null,
            bullets: [],
          })),
        };
      case 'accomplishments':
        return {
          id,
          type: id,
          title: this.sectionLabel(id),
          placement: def.placement,
          isVisible: !!visibility[id],
          order,
          items: data.accomplishments.map(accomplishment => ({
            primaryText: accomplishment,
            secondaryText: '',
            bullets: [],
          })),
        };
      default:
        return null;
    }
  }

  toggleCustomSectionVisibility(sectionId: string): void {
    this.customSections.update(sections =>
      sections.map(section => section.id === sectionId ? { ...section, isVisible: !section.isVisible } : section)
    );
  }

  private isCustomSection(sectionId: string): boolean {
    return this.customSections().some(section => section.id === sectionId);
  }

  addCustomSection(): void {
    const nextIndex = this.customSections().length + 1;
    this.customSections.update(sections => [
      ...sections,
      {
        id: `custom-${Date.now()}`,
        type: 'custom',
        title: `Custom Section ${nextIndex}`,
        placement: 'main',
        isVisible: true,
        order: sections.length,
        text: null,
        items: [{ primaryText: 'New item', secondaryText: '', bullets: ['Add details here'] }],
      }
    ]);
  }

  removeCustomSection(sectionId: string): void {
    this.customSections.update(sections =>
      sections
        .filter(section => section.id !== sectionId)
        .map((section, index) => ({ ...section, order: index }))
    );
  }

  updateCustomSectionTitle(sectionId: string, title: string): void {
    this.customSections.update(sections =>
      sections.map(section => section.id === sectionId ? { ...section, title } : section)
    );
  }

  updateCustomSectionText(sectionId: string, text: string): void {
    this.customSections.update(sections =>
      sections.map(section => section.id === sectionId ? { ...section, text } : section)
    );
  }

  updateCustomSectionItem(sectionId: string, itemIndex: number, field: keyof CvSectionItem, value: any): void {
    this.customSections.update(sections => sections.map(section => {
      if (section.id !== sectionId) return section;
      const items = [...section.items];
      items[itemIndex] = { ...items[itemIndex], [field]: value };
      return { ...section, items };
    }));
  }

  updateCustomSectionBullet(sectionId: string, itemIndex: number, bulletIndex: number, value: string): void {
    this.customSections.update(sections => sections.map(section => {
      if (section.id !== sectionId) return section;
      const items = [...section.items];
      const bullets = [...items[itemIndex].bullets];
      bullets[bulletIndex] = value;
      items[itemIndex] = { ...items[itemIndex], bullets };
      return { ...section, items };
    }));
  }

  addCustomSectionItem(sectionId: string): void {
    this.customSections.update(sections => sections.map(section => {
      if (section.id !== sectionId) return section;
      return {
        ...section,
        items: [...section.items, { primaryText: 'New item', secondaryText: '', bullets: ['Add details here'] }]
      };
    }));
  }

  removeCustomSectionItem(sectionId: string, itemIndex: number): void {
    this.customSections.update(sections => sections.map(section => {
      if (section.id !== sectionId) return section;
      return { ...section, items: section.items.filter((_, index) => index !== itemIndex) };
    }));
  }

  addCustomSectionBullet(sectionId: string, itemIndex: number): void {
    this.customSections.update(sections => sections.map(section => {
      if (section.id !== sectionId) return section;
      const items = [...section.items];
      items[itemIndex] = { ...items[itemIndex], bullets: [...items[itemIndex].bullets, 'New detail'] };
      return { ...section, items };
    }));
  }

  removeCustomSectionBullet(sectionId: string, itemIndex: number, bulletIndex: number): void {
    this.customSections.update(sections => sections.map(section => {
      if (section.id !== sectionId) return section;
      const items = [...section.items];
      items[itemIndex] = { ...items[itemIndex], bullets: items[itemIndex].bullets.filter((_, index) => index !== bulletIndex) };
      return { ...section, items };
    }));
  }

  dropSectionOrder(event: CdkDragDrop<string[]>): void {
    const ordered = [...this.sectionOrder()];
    moveItemInArray(ordered, event.previousIndex, event.currentIndex);
    this.sectionOrder.set(ordered);
  }

  // INTERACTIVE MUTATION HELPER METHODS
  updateCandidateField(field: keyof Candidate, value: any) {
    this.cvData.update(data => ({
      ...data,
      candidate: {
        ...data.candidate,
        [field]: value
      }
    }));
  }

  updateSummary(value: string) {
    this.cvData.update(data => ({
      ...data,
      summary: value
    }));
  }

  updateExperience(index: number, field: keyof Experience, value: any) {
    this.cvData.update(data => {
      const exp = [...data.experience];
      exp[index] = { ...exp[index], [field]: value };
      return { ...data, experience: exp };
    });
  }

  updateExperienceBullet(expIndex: number, bulletIndex: number, value: string) {
    this.cvData.update(data => {
      const exp = [...data.experience];
      const bullets = [...exp[expIndex].bullets];
      bullets[bulletIndex] = value;
      exp[expIndex] = { ...exp[expIndex], bullets };
      return { ...data, experience: exp };
    });
  }

  addExperience() {
    this.cvData.update(data => ({
      ...data,
      experience: [
        ...data.experience,
        { role: 'Nouveau Poste', company: 'Nouvelle Entreprise', start: '2026-01', end: '', bullets: ['Responsabilite cle ou realisation technique accomplie.'] }
      ]
    }));
  }

  removeExperience(index: number) {
    this.cvData.update(data => ({
      ...data,
      experience: data.experience.filter((_, i) => i !== index)
    }));
  }

  updateProject(index: number, field: keyof Project, value: any) {
    this.cvData.update(data => {
      const projs = [...data.projects];
      projs[index] = { ...projs[index], [field]: value };
      return { ...data, projects: projs };
    });
  }

  headerFirstName(): string {
    const fullName = String(this.cvData().candidate.name ?? '').trim();
    if (!fullName) return '';
    const parts = fullName.split(/\s+/);
    return parts[0] ?? '';
  }

  headerLastName(): string {
    const fullName = String(this.cvData().candidate.name ?? '').trim();
    if (!fullName) return '';
    const parts = fullName.split(/\s+/);
    return parts.slice(1).join(' ');
  }

  updateHeaderFirstName(value: string): void {
    const lastName = this.headerLastName();
    const next = [value?.trim(), lastName].filter(Boolean).join(' ').trim();
    this.updateCandidateField('name', next);
  }

  updateHeaderLastName(value: string): void {
    const firstName = this.headerFirstName();
    const next = [firstName, value?.trim()].filter(Boolean).join(' ').trim();
    this.updateCandidateField('name', next);
  }

  private candidateLocationParts(): { city: string; province: string; zipCode: string } {
    const raw = String(this.cvData().candidate.location ?? '').trim();
    if (!raw) {
      return { city: '', province: '', zipCode: '' };
    }
    const parts = raw.split(',').map((part) => part.trim());
    return {
      city: parts[0] ?? '',
      province: parts[1] ?? '',
      zipCode: parts[2] ?? '',
    };
  }

  headerCity(): string {
    return this.candidateLocationParts().city;
  }

  headerProvince(): string {
    return this.candidateLocationParts().province;
  }

  headerZipCode(): string {
    return this.candidateLocationParts().zipCode;
  }

  updateHeaderLocationPart(part: 'city' | 'province' | 'zipCode', value: string): void {
    const current = this.candidateLocationParts();
    const next = {
      ...current,
      [part]: String(value ?? '').trim(),
    };
    const merged = [next.city, next.province, next.zipCode].filter(Boolean).join(', ');
    this.updateCandidateField('location', merged);
  }

  parseCommaSeparatedList(value: any): string[] {
    return String(value ?? '')
      .split(',')
      .map(item => item.trim())
      .filter(Boolean);
  }

  updateProjectBullet(projIndex: number, bulletIndex: number, value: string) {
    this.cvData.update(data => {
      const projs = [...data.projects];
      const bullets = [...projs[projIndex].bullets];
      bullets[bulletIndex] = value;
      projs[projIndex] = { ...projs[projIndex], bullets };
      return { ...data, projects: projs };
    });
  }

  addProject() {
    this.cvData.update(data => ({
      ...data,
      projects: [
        ...data.projects,
        {
          title: 'Nouveau Projet',
          description: 'Description du projet et de son impact.',
          technologies: [],
          dateRealisation: '',
          bullets: ['Description de l\'architecture technique et de l\'impact du projet.']
        }
      ]
    }));
  }

  removeProject(index: number) {
    this.cvData.update(data => ({
      ...data,
      projects: data.projects.filter((_, i) => i !== index)
    }));
  }

  updateEducation(index: number, field: keyof Education, value: any) {
    this.cvData.update(data => {
      const edu = [...data.education];
      edu[index] = { ...edu[index], [field]: value };
      return { ...data, education: edu };
    });
  }

  addEducation() {
    this.cvData.update(data => ({
      ...data,
      education: [
        ...data.education,
        { degree: 'Diplôme ou Certification', institution: 'Université / École', year: '2026' }
      ]
    }));
  }

  removeEducation(index: number) {
    this.cvData.update(data => ({
      ...data,
      education: data.education.filter((_, i) => i !== index)
    }));
  }

  updateSkill(section: 'technical' | 'soft', index: number, field: keyof Skill, value: any) {
    this.cvData.update(data => {
      const targetIndexes = this.getSkillsBySection(section, data.skills)
        .map((skill) => data.skills.indexOf(skill))
        .filter((skillIndex) => skillIndex >= 0);
      const absoluteIndex = targetIndexes[index];
      if (absoluteIndex == null) {
        return data;
      }

      const skills = [...data.skills];
      skills[absoluteIndex] = { ...skills[absoluteIndex], [field]: value };
      return { ...data, skills };
    });
  }

  addSkill(section: 'technical' | 'soft' = 'technical') {
    const category = section === 'soft' ? 'Soft Skills' : 'Technical';
    this.cvData.update(data => ({
      ...data,
      skills: [
        ...data.skills,
        { name: section === 'soft' ? 'Communication' : 'Nouveau Skill', level: 3, isMatched: true, category, typeCompetence: category }
      ]
    }));
  }

  removeSkill(section: 'technical' | 'soft', index: number) {
    this.cvData.update(data => {
      const targetIndexes = this.getSkillsBySection(section, data.skills)
        .map((skill) => data.skills.indexOf(skill))
        .filter((skillIndex) => skillIndex >= 0);
      const absoluteIndex = targetIndexes[index];
      if (absoluteIndex == null) {
        return data;
      }

      return {
        ...data,
        skills: data.skills.filter((_, i) => i !== absoluteIndex)
      };
    });
  }

  updateCertification(index: number, value: string) {
    this.cvData.update(data => {
      const certs = [...data.certifications];
      certs[index] = value;
      return { ...data, certifications: certs };
    });
  }

  addCertification() {
    this.cvData.update(data => ({
      ...data,
      certifications: [...data.certifications, 'Nouvelle Certification Professionnelle']
    }));
  }

  removeCertification(index: number) {
    this.cvData.update(data => ({
      ...data,
      certifications: data.certifications.filter((_, i) => i !== index)
    }));
  }

  updateLanguage(index: number, value: string) {
    this.cvData.update(data => {
      const langs = [...data.languages];
      langs[index] = value;
      return { ...data, languages: langs };
    });
  }

  addLanguage() {
    this.cvData.update(data => ({
      ...data,
      languages: [...data.languages, 'Nouvelle Langue']
    }));
  }

  removeLanguage(index: number) {
    this.cvData.update(data => ({
      ...data,
      languages: data.languages.filter((_, i) => i !== index)
    }));
  }

  addActivity(): void {
    this.cvData.update(data => ({
      ...data,
      activities: [
        ...data.activities,
        {
          title: 'Nouvelle activite',
          role: '',
          description: '',
          startDate: null,
          endDate: null,
        }
      ]
    }));
  }

  updateActivity(index: number, field: keyof Activity, value: string | null): void {
    this.cvData.update(data => ({
      ...data,
      activities: data.activities.map((activity, activityIndex) => activityIndex === index ? { ...activity, [field]: value } : activity)
    }));
  }

  removeActivity(index: number): void {
    this.cvData.update(data => ({
      ...data,
      activities: data.activities.filter((_, activityIndex) => activityIndex !== index)
    }));
  }

  addAccomplishment(): void {
    this.cvData.update(data => ({
      ...data,
      accomplishments: [...data.accomplishments, 'New accomplishment']
    }));
  }

  updateAccomplishment(index: number, value: string): void {
    this.cvData.update(data => ({
      ...data,
      accomplishments: data.accomplishments.map((item, accomplishmentIndex) => accomplishmentIndex === index ? value : item)
    }));
  }

  removeAccomplishment(index: number): void {
    this.cvData.update(data => ({
      ...data,
      accomplishments: data.accomplishments.filter((_, accomplishmentIndex) => accomplishmentIndex !== index)
    }));
  }

  sectionLabel(sectionId: string): string {
    const normalizedId = this.normalizeSectionId(sectionId);
    const override = this.sectionTitleOverrides()[normalizedId]?.trim();
    return override || (this.sectionDefinitions.find(section => section.id === normalizedId)?.label ?? normalizedId);
  }

  // Local persistence is immediate; backend autosave is owned by StepGeneration
  // so preview/save/export all share the same normalized data flow.
  private autoSave = effect(() => {
    const data = this.cvData();
    const visibility = this.sectionVisibility();
    const payload = this.composeEditorPayload(data);
    this.dataChange.emit(payload);
    try {
      localStorage.setItem('nextstep_cv_draft', JSON.stringify(payload));
      localStorage.setItem('nextstep_cv_visibility', JSON.stringify(visibility));
    } catch {}

    clearTimeout(this.saveTimeout);
    this.saveTimeout = setTimeout(() => {
      this.lastSaved.set(new Date().toLocaleTimeString());
    }, 300);
  });

  private composeEditorPayload(data: CvGeneratedSchema): CvGeneratedSchema & { sections: CvSection[] } {
    return {
      ...data,
      sections: this.orderedSections.map((section, index) => ({ ...section, order: index })),
    };
  }

  ngOnInit(): void {
    this.activeTemplate.set(this.normalizeTemplateId(this.templateId));
    this.hydrateDesignConfig(this.designConfig);
    console.log('[CV-PIPELINE] ResumeEditor init', {
      templateId: this.templateId,
      offerId: this.offerId,
      hasInitialData: !!this.initialData,
      initialDataKeys: this.initialData && typeof this.initialData === 'object' ? Object.keys(this.initialData) : [],
    });
    if (this.initialData) {
      this.hydrateFromGenerated(this.initialData);
    } else {
      const saved = localStorage.getItem('nextstep_cv_draft');
      if (saved) {
        try {
          const parsed = JSON.parse(saved);
          this.hydrateFromGenerated(parsed);
        } catch {}
      }
    }
    const savedVis = localStorage.getItem('nextstep_cv_visibility');
    if (savedVis) {
      try {
        this.sectionVisibility.set(JSON.parse(savedVis));
      } catch {}
    }

    const data = this.cvData();
    console.log('[CV-PIPELINE] ResumeEditor ready for preview', {
      candidateName: data?.candidate?.name,
      hasSummary: !!data?.summary,
      experienceCount: Array.isArray(data?.experience) ? data.experience.length : 0,
      skillsCount: Array.isArray(data?.skills) ? data.skills.length : 0,
      languagesCount: Array.isArray(data?.languages) ? data.languages.length : 0,
      atsScore: data?.atsScore,
      matchingScore: data?.matchingScore,
    });
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['templateId'] && changes['templateId'].currentValue) {
      this.activeTemplate.set(this.normalizeTemplateId(changes['templateId'].currentValue));
    }

    if (
      changes['initialData'] &&
      !changes['initialData'].firstChange &&
      changes['initialData'].currentValue
    ) {
      this.hydrateFromGenerated(changes['initialData'].currentValue);
    }

    if (changes['designConfig']) {
      this.hydrateDesignConfig(changes['designConfig'].currentValue);
    }

    if (changes['renderedHtml']) {
      this.schedulePreviewOverlayRefresh();
    }
  }

  ngAfterViewInit(): void {
    this.schedulePreviewOverlayRefresh();
  }

  ngOnDestroy(): void {
    this.sidebarService.setEditorFocusMode(false);
    if (this.overlayRefreshFrame !== null) {
      cancelAnimationFrame(this.overlayRefreshFrame);
    }
    this.previewResizeObserver?.disconnect();
    this.previewMutationObserver?.disconnect();
    this.detachPreviewAssetListeners();
  }

  private hydrateFromGenerated(generated: any): void {
    const normalized = generated?.cvData
      ?? generated?.cv_data
      ?? generated?.cvGeneratedContent
      ?? generated?.cv_optimized_content
      ?? generated?.cvOptimizedContent
      ?? generated;
    const profileSource = generated?.profileData?.data
      ?? generated?.profileData?.profile
      ?? generated?.profile_data?.data
      ?? generated?.profile_data?.profile
      ?? generated?.profileData
      ?? generated?.profile_data
      ?? {};
    if (!normalized || typeof normalized !== 'object') {
      console.warn('[CV-PIPELINE] ResumeEditor hydrate skipped: invalid payload', { generated });
      return;
    }
    const profilePersonal = profileSource?.personalInfo
      ?? profileSource?.personal_info
      ?? profileSource?.personal
      ?? {};
    const candidate = normalized?.candidate
      ?? normalized?.personal
      ?? normalized?.personalInfo
      ?? normalized?.personal_info
      ?? profilePersonal
      ?? {};
    const experience = this.firstArray(normalized, ['experience', 'experiences', 'experiences_optimisees']);
    const sectionExperience = this.extractExperienceFromSections(normalized?.sections);
    const education = this.firstArray(normalized, ['education', 'formations', 'formations_optimisees']);
    const skills = this.collectHydratedSkills(normalized);
    const sectionSkills = this.extractSkillsFromSections(normalized?.sections);
    const projects = this.firstArray(normalized, ['projects', 'projets', 'projets_optimises']);
    const sectionProjects = this.extractProjectsFromSections(normalized?.sections);
    const certifications = this.firstArray(normalized, ['certifications', 'certificats', 'certifications_optimisees']);
    const languages = this.firstArray(normalized, ['languages', 'langues']);
    const activities = this.firstArray(normalized, ['activities', 'extracurricular', 'activites', 'activités']);
    const highlightedSkills = new Set(
      this.firstArray(normalized, ['competences_mises_en_avant'])
        .map((skill: any) => this.cleanText(typeof skill === 'string' ? skill : skill?.name ?? skill?.nom ?? skill?.label).toLowerCase())
        .filter(Boolean)
    );

    console.log('[CV-PIPELINE] ResumeEditor hydrating generated CV', {
      hasCandidate: !!candidate,
      candidateName: this.extractCandidateName(candidate),
      profileName: this.extractCandidateName(profilePersonal),
      hasPhoto: !!this.extractCandidatePhotoUrl(candidate, profilePersonal, profileSource, generated),
      experienceCount: experience.length,
      skillsCount: skills.length,
      languagesCount: languages.length,
      hasSummary: !!(normalized?.summary ?? normalized?.resume ?? normalized?.resumeProfessionnel),
      keys: Object.keys(normalized),
    });

    this.cvData.update(current => ({
      ...current,
      candidate: {
        ...current.candidate,
        name: this.resolvePreferredCandidateName(candidate, profilePersonal, current.candidate.name),
        email: this.cleanText(candidate?.email ?? candidate?.mail ?? profilePersonal?.email ?? profilePersonal?.mail) || current.candidate.email,
        phone: this.cleanText(candidate?.phone ?? candidate?.telephone ?? profilePersonal?.phone ?? profilePersonal?.telephone) || current.candidate.phone,
        location: this.resolvePreferredLocation(candidate, profilePersonal, current.candidate.location),
        title: this.cleanText(candidate?.title ?? candidate?.titrePoste ?? candidate?.poste ?? profilePersonal?.title ?? profilePersonal?.titrePoste ?? profilePersonal?.poste) || current.candidate.title,
        photoUrl: this.extractCandidatePhotoUrl(candidate, profilePersonal, profileSource, generated) ?? current.candidate.photoUrl,
        linkedIn: candidate?.linkedIn ?? candidate?.linkedin ?? candidate?.lienLinkedin ?? profilePersonal?.linkedIn ?? profilePersonal?.linkedin ?? profilePersonal?.lienLinkedin ?? current.candidate.linkedIn,
        gitHub: candidate?.gitHub ?? candidate?.github ?? candidate?.lienGithub ?? profilePersonal?.gitHub ?? profilePersonal?.github ?? profilePersonal?.lienGithub ?? current.candidate.gitHub,
        portfolio: candidate?.portfolio ?? candidate?.lienPortfolio ?? profilePersonal?.portfolio ?? profilePersonal?.lienPortfolio ?? current.candidate.portfolio,
      },
      summary: normalized?.summary ?? normalized?.resume ?? normalized?.resumeProfessionnel ?? current.summary,
      experience: this.mergeHydratedExperience(
        experience.length > 0 ? experience.map((e: any) => ({
          role: this.cleanText(e?.role ?? e?.title ?? e?.poste ?? e?.titre),
          company: this.cleanText(e?.company ?? e?.entreprise),
          start: this.toMonthValue(e?.start ?? e?.dateDebut ?? e?.date_debut),
          end: this.toMonthValue(e?.end ?? e?.dateFin ?? e?.date_fin),
          bullets: this.asStringArray(e?.bullets ?? e?.taches_optimisees ?? e?.missions ?? e?.taches ?? e?.description_optimisee ?? e?.description),
          relevance: this.cleanText(e?.niveau_pertinence ?? e?.relevance),
          keywords: this.asStringArray(e?.mots_cles_cibles ?? e?.keywords),
        })) : [],
        sectionExperience,
        projects.length > 0
          ? projects.map((project: any) => this.cleanText(project?.title ?? project?.titreProjet ?? project?.titre))
          : sectionProjects.map((project) => this.cleanText(project?.title)),
        current.experience
      ),
      education: education.length > 0
        ? education.map((education: any) => ({
            degree: String(education?.degree ?? education?.diplome ?? education?.titre ?? ''),
            institution: String(education?.institution ?? education?.etablissement ?? education?.ecole ?? ''),
            year: String(education?.year ?? education?.annee ?? education?.anneeFin ?? education?.dateFin ?? ''),
            startYear: String(education?.startYear ?? education?.anneeDebut ?? education?.dateDebut ?? ''),
            endYear: String(education?.endYear ?? education?.anneeFin ?? education?.dateFin ?? ''),
          }))
        : current.education,
      skills: this.mergeHydratedSkills(
        skills.length > 0 ? skills.map((skill: any) => {
          const name = typeof skill === 'string' ? skill : this.cleanText(skill?.name ?? skill?.nom ?? skill?.label);
          return {
            name: this.cleanText(name),
            level: Math.min(5, Math.max(1, Number(skill?.level ?? skill?.niveau ?? 3))),
            isMatched: !!(skill?.isMatched ?? skill?.matched ?? skill?.statut === 'correspond'),
            isHighlighted: highlightedSkills.has(this.normalizeKey(name)),
            category: this.cleanText(skill?.category ?? skill?.categorie ?? skill?.typeCompetence ?? skill?.type_competence),
            typeCompetence: this.cleanText(skill?.typeCompetence ?? skill?.type_competence ?? skill?.category ?? skill?.categorie),
          };
        }).filter((skill: Skill) => !!skill.name) : [],
        sectionSkills,
        current.skills
      ),
      projects: this.mergeHydratedProjects(
        projects.length > 0
          ? projects.map((project: any) => ({
              title: String(project?.title ?? project?.titreProjet ?? project?.titre ?? ''),
              description: String(project?.description ?? project?.description_optimisee ?? ''),
              technologies: this.extractProjectTechnologies(project),
              dateRealisation: this.toMonthValue(project?.dateRealisation ?? project?.date_realisation),
              relevance: this.cleanText(project?.niveau_pertinence ?? project?.relevance),
              keywords: this.asStringArray(project?.mots_cles_cibles ?? project?.keywords),
              bullets: this.asStringArray(
                project?.bullets
                ?? project?.taches_optimisees
                ?? project?.taches
                ?? project?.missions
                ?? project?.tasks
                ?? project?.responsibilities
                ?? project?.realisations
                ?? project?.description_optimisee
                ?? project?.description
              ),
            }))
          : [],
        sectionProjects,
        current.projects
      ),
      certifications: certifications.length > 0 ? this.asStringArray(certifications) : current.certifications,
      languages: languages.length > 0 ? this.asStringArray(languages) : current.languages,
      activities: activities.length > 0
        ? activities.map((activity: any) => ({
            title: this.cleanText(activity?.title ?? activity?.name ?? activity),
            role: this.cleanText(activity?.role ?? activity?.secondaryText) || null,
            description: this.cleanText(activity?.description) || null,
            startDate: this.toMonthValue(activity?.startDate ?? activity?.start ?? activity?.dateDebut ?? activity?.date_debut),
            endDate: this.toMonthValue(activity?.endDate ?? activity?.end ?? activity?.dateFin ?? activity?.date_fin),
          })).filter((activity: Activity) => !!activity.title || !!activity.description)
        : current.activities,
      accomplishments: Array.isArray(normalized?.accomplishments) ? normalized.accomplishments : current.accomplishments,
      atsScore: normalized?.atsScore ?? normalized?.ats_score ?? current.atsScore,
      matchingScore: normalized?.matchingScore ?? normalized?.matching_score ?? current.matchingScore,
      atsCoveragePct: normalized?.atsCoveragePct ?? normalized?.ats_coverage_pct ?? current.atsCoveragePct,
    }));

    this.hydrateSectionState(normalized?.sections);

    const hydrated = this.cvData();
    console.log('[CV-PIPELINE] ResumeEditor hydration applied', {
      candidateName: hydrated?.candidate?.name,
      experienceCount: Array.isArray(hydrated?.experience) ? hydrated.experience.length : 0,
      skillsCount: Array.isArray(hydrated?.skills) ? hydrated.skills.length : 0,
      languagesCount: Array.isArray(hydrated?.languages) ? hydrated.languages.length : 0,
      atsScore: hydrated?.atsScore,
      matchingScore: hydrated?.matchingScore,
    });
  }

  private toMonthValue(value: any): string {
    if (typeof value !== 'string') return '';
    const v = value.trim();
    if (!v) return '';
    if (v.toLowerCase() === 'present' || v.toLowerCase() === 'pr�sent' || v.toLowerCase() === 'pr�sent') return '';
    const m = v.match(/^(\d{4})-(\d{2})/);
    if (m) return `${m[1]}-${m[2]}`;
    if (/^\d{4}-\d{2}$/.test(v)) return v;
    return '';
  }

  private firstArray(source: any, keys: string[]): any[] {
    for (const key of keys) {
      const value = source?.[key];
      if (Array.isArray(value)) return value;
    }
    return [];
  }

  private asStringArray(value: any): string[] {
    const values = Array.isArray(value) ? value : [value];
    return values.map((item: any) => {
      if (typeof item === 'string') return item.trim();
      if (typeof item === 'number' || typeof item === 'boolean') return String(item);
      if (item && typeof item === 'object') {
        return this.cleanText(item.name ?? item.nom ?? item.label ?? item.title ?? item.titre ?? item.description ?? '');
      }
      return String(item ?? '').trim();
    }).filter(Boolean);
  }

  private extractProjectTechnologies(project: any): string[] {
    const raw =
      project?.technologies
      ?? project?.technologies_utilisees
      ?? project?.technologiesUtilisees
      ?? project?.technologiesUsed
      ?? project?.stack
      ?? project?.techStack
      ?? project?.outils;

    if (Array.isArray(raw)) {
      return raw
        .map((technology: any) => this.cleanText(technology))
        .filter(Boolean);
    }

    const text = this.cleanText(raw);
    if (!text) {
      return [];
    }

    return text
      .split(/[;,|]/g)
      .map((technology) => technology.trim())
      .filter(Boolean);
  }

  private extractProjectsFromSections(sections: any): Project[] {
    if (!Array.isArray(sections)) {
      return [];
    }

    return sections
      .filter((section: any) => this.normalizeSectionId(section?.id ?? section?.type) === 'projects')
      .flatMap((section: any) => Array.isArray(section?.items) ? section.items : [])
      .map((item: any) => ({
        title: this.cleanText(item?.primaryText ?? item?.primary_text),
        description: this.cleanText(item?.description),
        technologies: this.asStringArray(
          item?.secondaryText
          ?? item?.secondary_text
          ?? item?.technologies
          ?? item?.technologies_utilisees
          ?? item?.technologiesUsed
          ?? item?.tech_stack
          ?? item?.techStack
          ?? item?.stack
        ),
        dateRealisation: this.toMonthValue(item?.startDate ?? item?.start_date),
        bullets: this.asStringArray(item?.bullets ?? item?.bullet_points),
        relevance: '',
        keywords: [],
      }))
      .filter((project) => !!project.title || !!project.description || project.bullets.length > 0);
  }

  private extractExperienceFromSections(sections: any): Experience[] {
    if (!Array.isArray(sections)) {
      return [];
    }

    return sections
      .filter((section: any) => this.normalizeSectionId(section?.id ?? section?.type) === 'experience')
      .flatMap((section: any) => Array.isArray(section?.items) ? section.items : [])
      .map((item: any) => ({
        role: this.cleanText(item?.primaryText ?? item?.primary_text),
        company: this.cleanText(item?.secondaryText ?? item?.secondary_text),
        start: this.toMonthValue(item?.startDate ?? item?.start_date),
        end: this.toMonthValue(item?.endDate ?? item?.end_date),
        bullets: this.asStringArray(item?.bullets ?? item?.bullet_points),
        relevance: '',
        keywords: [],
      }))
      .filter((item) => !!item.role || !!item.company || item.bullets.length > 0);
  }

  private extractSkillsFromSections(sections: any): Skill[] {
    if (!Array.isArray(sections)) {
      return [];
    }

    return sections
      .filter((section: any) => {
        const normalizedId = this.normalizeSectionId(section?.id ?? section?.type);
        return normalizedId === 'skills' || normalizedId === 'softskills';
      })
      .flatMap((section: any) => {
        const normalizedId = this.normalizeSectionId(section?.id ?? section?.type);
        const category = normalizedId === 'softskills' ? 'Soft Skills' : 'Technical';
        return (Array.isArray(section?.items) ? section.items : []).map((item: any) => ({
          name: this.cleanText(item?.primaryText ?? item?.primary_text),
          level: Math.min(5, Math.max(1, Number(item?.level ?? 3))),
          isMatched: !!(item?.isMatched ?? item?.is_matched),
          category,
          typeCompetence: category,
        }));
      })
      .filter((item) => !!item.name);
  }

  private collectHydratedSkills(source: any): any[] {
    const technical = this.firstArray(source, [
      'skills',
      'technicalSkills',
      'technical_skills',
      'competences',
      'competences_reordonnees',
      'competences_mises_en_avant',
    ]).map((skill: any) => ({
      ...((skill && typeof skill === 'object') ? skill : { name: skill }),
      category: skill?.category ?? skill?.categorie ?? skill?.typeCompetence ?? skill?.type_competence ?? 'Technical',
      typeCompetence: skill?.typeCompetence ?? skill?.type_competence ?? skill?.category ?? skill?.categorie ?? 'Technical',
    }));

    const soft = this.firstArray(source, [
      'softSkills',
      'soft_skills',
      'softskills',
      'competences_comportementales',
      'soft_skills_reordonnees',
    ]).map((skill: any) => ({
      ...((skill && typeof skill === 'object') ? skill : { name: skill }),
      category: skill?.category ?? skill?.categorie ?? skill?.typeCompetence ?? skill?.type_competence ?? 'Soft Skills',
      typeCompetence: skill?.typeCompetence ?? skill?.type_competence ?? skill?.category ?? skill?.categorie ?? 'Soft Skills',
    }));

    return [...technical, ...soft];
  }

  private mergeHydratedExperience(
    primary: Experience[],
    fallback: Experience[],
    knownProjectTitles: string[],
    current: Experience[]
  ): Experience[] {
    const projectTitleSet = new Set(
      knownProjectTitles
        .map((title) => this.normalizeKey(title))
        .filter(Boolean)
    );
    const source = [...fallback, ...primary];
    const incoming = new Map<string, Experience>();

    for (const rawItem of source) {
      const item = this.normalizeExperienceEntry(rawItem);
      if (!item) {
        continue;
      }

      if (this.looksLikeProjectEntry(item, projectTitleSet)) {
        continue;
      }

      const key = this.getExperienceMergeKey(item);
      const existing = incoming.get(key);
      incoming.set(key, existing ? this.mergeExperienceEntry(existing, item) : item);
    }

    const values = [...incoming.values()];
    return values.length > 0 ? values : current;
  }

  private mergeHydratedSkills(primary: Skill[], fallback: Skill[], current: Skill[]): Skill[] {
    const incoming = [...fallback, ...primary].filter((item) => !!this.cleanText(item?.name));
    if (incoming.length === 0) {
      return current;
    }

    const merged = new Map<string, Skill>();
    for (const skill of incoming) {
      const sectionKey = this.isSoftSkillCategory(skill?.category ?? skill?.typeCompetence) ? 'softskills' : 'skills';
      const key = `${sectionKey}::${this.normalizeKey(skill?.name)}`;
      const existing = merged.get(key);
      merged.set(key, {
        ...existing,
        ...skill,
        name: this.cleanText(skill?.name) || existing?.name || '',
        level: Math.min(5, Math.max(1, Number(skill?.level ?? existing?.level ?? 3))),
        isMatched: !!(skill?.isMatched ?? existing?.isMatched),
        category: this.cleanText(skill?.category ?? skill?.typeCompetence) || existing?.category || (sectionKey === 'softskills' ? 'Soft Skills' : 'Technical'),
        typeCompetence: this.cleanText(skill?.typeCompetence ?? skill?.category) || existing?.typeCompetence || (sectionKey === 'softskills' ? 'Soft Skills' : 'Technical'),
      });
    }

    return [...merged.values()];
  }

  private mergeHydratedProjects(primary: Project[], fallback: Project[], current: Project[]): Project[] {
    const incoming = [...fallback, ...primary].filter((project) =>
      !!this.cleanText(project?.title) || !!this.cleanText(project?.description) || (project?.bullets?.length ?? 0) > 0
    );
    const sourceProjects = incoming.length > 0 ? incoming : current;
    const merged = new Map<string, Project>();

    for (const project of sourceProjects) {
      const key = this.cleanText(project?.title).toLowerCase();
      if (!key) {
        continue;
      }

      const existing = merged.get(key);
      merged.set(key, {
        ...existing,
        ...project,
        title: project.title || existing?.title || '',
        description: project.description || existing?.description,
        technologies: project.technologies?.length ? project.technologies : (existing?.technologies ?? []),
        dateRealisation: project.dateRealisation || existing?.dateRealisation,
        bullets: project.bullets?.length ? project.bullets : (existing?.bullets ?? []),
        relevance: project.relevance || existing?.relevance,
        keywords: project.keywords?.length ? project.keywords : (existing?.keywords ?? []),
      });
    }

    return Array.from(merged.values());
  }

  private normalizeExperienceEntry(item: Experience | null | undefined): Experience | null {
    const role = this.cleanText(item?.role);
    const company = this.cleanText(item?.company);
    const start = this.toMonthValue(item?.start);
    const end = this.toMonthValue(item?.end);
    const bullets = this.dedupeStrings(this.asStringArray(item?.bullets));
    const relevance = this.cleanText(item?.relevance);
    const keywords = this.dedupeStrings(this.asStringArray(item?.keywords));

    if (!role && !company && bullets.length === 0) {
      return null;
    }

    return {
      role,
      company,
      start,
      end,
      bullets,
      relevance,
      keywords,
    };
  }

  private looksLikeProjectEntry(item: Experience, projectTitleSet: Set<string>): boolean {
    const roleKey = this.normalizeKey(item.role);
    if (!roleKey || !projectTitleSet.has(roleKey)) {
      return false;
    }

    const hasCompany = !!this.cleanText(item.company);
    const hasDates = !!this.cleanText(item.start) || !!this.cleanText(item.end);
    return !hasCompany && !hasDates;
  }

  private getExperienceMergeKey(item: Experience): string {
    const role = this.normalizeKey(item.role);
    const company = this.normalizeKey(item.company);
    const start = this.cleanText(item.start).toLowerCase();
    const end = this.cleanText(item.end).toLowerCase();
    const bullets = this.dedupeStrings(item.bullets)
      .map((bullet: string) => this.normalizeKey(bullet))
      .filter(Boolean)
      .join('|');

    return [role, company, start, end, bullets].join('::');
  }

  private mergeExperienceEntry(existing: Experience, incoming: Experience): Experience {
    return {
      role: incoming.role || existing.role,
      company: incoming.company || existing.company,
      start: incoming.start || existing.start,
      end: incoming.end || existing.end,
      bullets: this.dedupeStrings([...(existing.bullets ?? []), ...(incoming.bullets ?? [])]),
      relevance: incoming.relevance || existing.relevance,
      keywords: this.dedupeStrings([...(existing.keywords ?? []), ...(incoming.keywords ?? [])]),
    };
  }

  private dedupeStrings(values: string[]): string[] {
    const seen = new Set<string>();
    return values.filter((value) => {
      const clean = this.cleanText(value);
      const key = this.normalizeKey(clean);
      if (!key || seen.has(key)) {
        return false;
      }

      seen.add(key);
      return true;
    });
  }

  private normalizeKey(value: any): string {
    return this.cleanText(value)
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, ' ')
      .trim();
  }

  private isSoftSkillCategory(value: any): boolean {
    const normalized = this.normalizeKey(value);
    return normalized === 'soft'
      || normalized === 'soft skill'
      || normalized === 'soft skills'
      || normalized === 'comportemental'
      || normalized === 'behavioral'
      || normalized === 'behavioural';
  }

  getSkillsBySection(section: 'technical' | 'soft', skills: Skill[]): Skill[] {
    return skills.filter((skill) => {
      const isSoft = this.isSoftSkillCategory(skill?.category ?? skill?.typeCompetence);
      return section === 'soft' ? isSoft : !isSoft;
    });
  }

  private cleanText(value: any): string {
    return String(value ?? '').trim().replace(/\s+/g, ' ');
  }

  private resolvePreferredCandidateName(candidate: any, profilePersonal: any, fallback: string): string {
    const candidateName = this.extractCandidateName(candidate);
    const profileName = this.extractCandidateName(profilePersonal);
    if (candidateName && !this.isGenericCandidateName(candidateName)) {
      return candidateName;
    }
    if (profileName) {
      return profileName;
    }
    return candidateName || fallback;
  }

  private resolvePreferredLocation(candidate: any, profilePersonal: any, fallback: string): string {
    const candidateLocation = this.cleanText(
      candidate?.location ?? [candidate?.ville ?? candidate?.city, candidate?.pays ?? candidate?.country].filter(Boolean).join(', ')
    );
    const profileLocation = this.cleanText(
      profilePersonal?.location ?? [profilePersonal?.ville ?? profilePersonal?.city, profilePersonal?.pays ?? profilePersonal?.country].filter(Boolean).join(', ')
    );
    return candidateLocation || profileLocation || fallback;
  }

  private extractCandidateName(source: any): string {
    return this.cleanText(
      source?.name
      ?? source?.nomComplet
      ?? source?.fullName
      ?? [source?.prenom ?? source?.firstName, source?.nom ?? source?.lastName].filter(Boolean).join(' ')
    );
  }

  private extractCandidatePhotoUrl(candidate: any, profilePersonal: any, profileSource?: any, generated?: any): string | null {
    const candidates = [
      candidate?.photoUrl,
      candidate?.photo_url,
      candidate?.profilePhoto,
      candidate?.profile_photo,
      candidate?.avatar,
      profilePersonal?.photoUrl,
      profilePersonal?.photo_url,
      profilePersonal?.profilePhoto,
      profilePersonal?.profile_photo,
      profilePersonal?.avatar,
      profileSource?.photoUrl,
      profileSource?.photo_url,
      profileSource?.profilePhoto,
      profileSource?.profile_photo,
      profileSource?.avatar,
      generated?.profileData?.photoUrl,
      generated?.profileData?.photo_url,
      generated?.profileData?.profilePhoto,
      generated?.profileData?.profile_photo,
    ];

    for (const value of candidates) {
      const clean = this.cleanText(value);
      if (clean) return clean;
    }

    return null;
  }

  private isGenericCandidateName(value: string): boolean {
    const normalized = this.cleanText(value).toLowerCase();
    return normalized === 'candidat' || normalized === 'candidate';
  }

  private normalizeTemplateId(templateId: string | null | undefined): SupportedEditorTemplate {
    const normalized = String(templateId ?? '').trim().toLowerCase();
    return normalized === 'latex' || normalized === 'tech-latex' || normalized === 'tech_latex'
      ? 'latex'
      : 'modern';
  }

  private hydrateSectionState(rawSections: any): void {
    if (!Array.isArray(rawSections) || rawSections.length === 0) {
      return;
    }

    const standardIds = new Set(this.sectionDefinitions.map(section => section.id));
    const ordered = [...rawSections]
      .filter(section => section && typeof section === 'object')
      .sort((a, b) => Number(a?.order ?? 0) - Number(b?.order ?? 0));

    const visibility = { ...this.sectionVisibility() };
    const order: string[] = [];
    const custom: CvSection[] = [];
    const titleOverrides: Record<string, string> = {};

    for (const section of ordered) {
      const rawId = String(section?.id ?? section?.type ?? '').trim();
      const id = this.normalizeSectionId(rawId);
      if (!id) continue;

      if (standardIds.has(id)) {
        visibility[id] = section?.isVisible !== false;
        if (!order.includes(id)) {
          order.push(id);
        }
        const title = this.cleanText(section?.title);
        if (title) {
          titleOverrides[id] = title;
        }
        continue;
      }

      custom.push({
        id,
        type: String(section?.type ?? 'custom'),
        title: String(section?.title ?? 'Custom Section'),
        placement: section?.placement === 'sidebar' ? 'sidebar' : 'main',
        isVisible: section?.isVisible !== false,
        order: custom.length,
        text: typeof section?.text === 'string' ? section.text : null,
        items: Array.isArray(section?.items)
          ? section.items.map((item: any) => ({
              primaryText: String(item?.primaryText ?? item?.primary_text ?? ''),
              secondaryText: String(item?.secondaryText ?? item?.secondary_text ?? ''),
              startDate: item?.startDate ?? item?.start_date ?? null,
              endDate: item?.endDate ?? item?.end_date ?? null,
              location: item?.location ?? null,
              description: item?.description ?? null,
              level: typeof item?.level === 'number' ? item.level : null,
              isMatched: !!(item?.isMatched ?? item?.is_matched),
              bullets: Array.isArray(item?.bullets)
                ? item.bullets.map((bullet: any) => String(bullet ?? ''))
                : Array.isArray(item?.bullet_points)
                  ? item.bullet_points.map((bullet: any) => String(bullet ?? ''))
                  : [],
            }))
          : [],
      });
    }

    const fallbackOrder = this.sectionDefinitions
      .map(section => section.id)
      .filter(id => !order.includes(id));

    this.sectionVisibility.set(visibility);
    this.sectionOrder.set([...order, ...fallbackOrder]);
    this.customSections.set(custom);
    this.sectionTitleOverrides.set(titleOverrides);
  }

  private hydrateDesignConfig(config: CvDesignConfig | null | undefined): void {
    const next = config ?? this.defaultDesignConfig(this.activeTemplate());
    this.fontFamily.set(next.fontFamily);
    this.fontSize.set(next.fontSize);
    this.lineSpacing.set(next.lineSpacing);
    this.sectionSpacing.set(next.sectionSpacing);
    this.sidebarWidth.set(next.sidebarWidth);
    this.activeAccentColor.set(this.colorIdFromHex(next.themeColor));
  }

  private emitDesignConfig(): void {
    this.designConfigChange.emit({
      themeColor: this.accentColorHex(),
      fontFamily: this.fontFamily(),
      fontSize: this.fontSize(),
      lineSpacing: this.lineSpacing(),
      sectionSpacing: this.sectionSpacing(),
      sidebarWidth: this.activeTemplate() === 'latex' ? '0%' : this.sidebarWidth()
    });
  }

  private colorIdFromHex(hex: string | null | undefined): string {
    const normalized = String(hex ?? '').trim().toLowerCase();
    const pairs: Record<string, string> = {
      '#6f7781': 'gray',
      '#1d3f91': 'navy',
      '#6f42c1': 'purple',
      '#2f9be5': 'blue',
      '#18a7a0': 'teal',
      '#11610c': 'green',
      '#b93317': 'red',
      '#0f172a': 'black',
      '#586c2f': 'olive',
      '#c65b1b': 'orange',
      '#334155': 'slate',
      '#be3b7b': 'pink',
      '#111827': 'black',
      '#2d3a8c': 'navy'
    };
    return pairs[normalized] ?? 'green';
  }

  private defaultDesignConfig(templateId: string): CvDesignConfig {
    return this.normalizeTemplateId(templateId) === 'latex'
      ? {
          themeColor: '#111827',
          fontFamily: "'IBM Plex Sans', 'Segoe UI', Arial, sans-serif",
          fontSize: '13px',
          lineSpacing: '1.38',
          sectionSpacing: '1rem',
          sidebarWidth: '0%'
        }
      : {
          themeColor: '#2d3a8c',
          fontFamily: "Inter, 'Segoe UI', Arial, sans-serif",
          fontSize: '14px',
          lineSpacing: '1.45',
          sectionSpacing: '1.2rem',
          sidebarWidth: '31%'
        };
  }

  // ── Drag-and-drop reorder ──
  dropExperience(event: CdkDragDrop<any[]>): void {
    const items = [...this.cvData().experience];
    moveItemInArray(items, event.previousIndex, event.currentIndex);
    this.cvData.update(d => ({ ...d, experience: items }));
  }

  dropProjects(event: CdkDragDrop<any[]>): void {
    const items = [...this.cvData().projects];
    moveItemInArray(items, event.previousIndex, event.currentIndex);
    this.cvData.update(d => ({ ...d, projects: items }));
  }

  dropEducation(event: CdkDragDrop<any[]>): void {
    const items = [...this.cvData().education];
    moveItemInArray(items, event.previousIndex, event.currentIndex);
    this.cvData.update(d => ({ ...d, education: items }));
  }

  dropSkills(event: CdkDragDrop<any[]>): void {
    const items = [...this.cvData().skills];
    moveItemInArray(items, event.previousIndex, event.currentIndex);
    this.cvData.update(d => ({ ...d, skills: items }));
  }

  // ── Section visibility toggles ──
  toggleSectionVisibility(key: string): void {
    const normalizedKey = this.normalizeSectionId(key);
    this.sectionVisibility.update(v => ({ ...v, [normalizedKey]: !v[normalizedKey] }));
  }

  // ── Bullet management ──
  addExperienceBullet(expIndex: number): void {
    this.cvData.update(data => {
      const exp = [...data.experience];
      exp[expIndex] = { ...exp[expIndex], bullets: [...exp[expIndex].bullets, 'Nouvelle réalisation...'] };
      return { ...data, experience: exp };
    });
  }

  removeExperienceBullet(expIndex: number, bulletIndex: number): void {
    this.cvData.update(data => {
      const exp = [...data.experience];
      exp[expIndex] = { ...exp[expIndex], bullets: exp[expIndex].bullets.filter((_, i) => i !== bulletIndex) };
      return { ...data, experience: exp };
    });
  }

  addProjectBullet(projIndex: number): void {
    this.cvData.update(data => {
      const projs = [...data.projects];
      projs[projIndex] = { ...projs[projIndex], bullets: [...projs[projIndex].bullets, 'Nouvelle description...'] };
      return { ...data, projects: projs };
    });
  }

  removeProjectBullet(projIndex: number, bulletIndex: number): void {
    this.cvData.update(data => {
      const projs = [...data.projects];
      projs[projIndex] = { ...projs[projIndex], bullets: projs[projIndex].bullets.filter((_, i) => i !== bulletIndex) };
      return { ...data, projects: projs };
    });
  }

  // ── Duplicate entry ──
  duplicateExperience(index: number): void {
    this.cvData.update(data => {
      const exp = [...data.experience];
      exp.splice(index + 1, 0, { ...exp[index], role: exp[index].role + ' (copie)' });
      return { ...data, experience: exp };
    });
  }

  duplicateEducation(index: number): void {
    this.cvData.update(data => {
      const edu = [...data.education];
      edu.splice(index + 1, 0, { ...edu[index], degree: edu[index].degree + ' (copie)' });
      return { ...data, education: edu };
    });
  }

  duplicateProject(index: number): void {
    this.cvData.update(data => {
      const projs = [...data.projects];
      projs.splice(index + 1, 0, { ...projs[index], title: projs[index].title + ' (copie)' });
      return { ...data, projects: projs };
    });
  }

  // ── Photo upload ──
  onPhotoUpload(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = () => {
      this.cvData.update(d => ({
        ...d,
        candidate: { ...d.candidate, photoUrl: reader.result as string }
      }));
    };
    reader.readAsDataURL(file);
  }

  removePhoto(): void {
    this.cvData.update(d => ({
      ...d,
      candidate: { ...d.candidate, photoUrl: null }
    }));
  }

  // ── Reset draft ──
  resetDraft(): void {
    localStorage.removeItem('nextstep_cv_draft');
    localStorage.removeItem('nextstep_cv_visibility');
    window.location.reload();
  }

  // SIMULATE AI WORKFLOW ENHANCEMENT
  improveSummaryWithAi() {
    if (this.isImproving()) return;
    this.isImproving.set(true);

    setTimeout(() => {
      this.cvData.update(data => ({
        ...data,
        summary: "Développeur Full Stack spécialisé en Python, FastAPI et architectures basées sur React/TypeScript. Expert dans l'intégration d'IA via l'approche RAG (Retrieval-Augmented Generation) et dans la création de pipelines d'automatisation avec n8n. Fortement orienté Clean Architecture, CI/CD et méthodologies Agiles pour concevoir des applications web hautement performantes et robustes.",
        matchingScore: 98,
        atsScore: 98
      }));
      this.isImproving.set(false);
    }, 1500);
  }

  exportPdf() {
    this.download.emit();
  }

  requestFinish(): void {
    this.finishRequested.emit();
    this.continue.emit();
  }

  zoomIn(): void {
    this.previewZoom.update((value) => Math.min(value + this.previewZoomStep, this.maxPreviewZoom));
    this.schedulePreviewOverlayRefresh();
  }

  zoomOut(): void {
    this.previewZoom.update((value) => Math.max(value - this.previewZoomStep, this.minPreviewZoom));
    this.schedulePreviewOverlayRefresh();
  }

  onPreviewWheel(event: WheelEvent): void {
    if (event.ctrlKey) {
      event.preventDefault();
    }
  }

  getBubbleGradient(): string {
    return 'linear-gradient(135deg, #1A91F0 0%, #0d5ca1 100%)';
  }

  formatDate(date: string): string {
    if (!date || date === 'Présent') return date;
    // Handle YYYY-MM format
    const parts = date.split('-');
    if (parts.length === 2) {
      const months = ['Jan','Fév','Mar','Avr','Mai','Juin','Juil','Aoû','Sep','Oct','Nov','Déc'];
      return months[parseInt(parts[1]) - 1] + ' ' + parts[0];
    }
    return date;
  }

  previewCounterLabel(order: number): string {
    return String(order).padStart(2, '0');
  }

  openOverlaySection(sectionId: string): void {
    this.openSectionInspector(sectionId);
  }

  private attachPreviewObservers(): void {
    const content = this.renderedPreviewContentRef?.nativeElement;
    if (!content) {
      return;
    }

    if (typeof ResizeObserver !== 'undefined') {
      this.previewResizeObserver?.disconnect();
      this.previewResizeObserver = new ResizeObserver(() => this.schedulePreviewOverlayRefresh());
      this.previewResizeObserver.observe(content);
    }

    if (typeof MutationObserver !== 'undefined') {
      this.previewMutationObserver?.disconnect();
      this.previewMutationObserver = new MutationObserver(() => this.schedulePreviewOverlayRefresh());
      this.previewMutationObserver.observe(content, { childList: true, subtree: true, attributes: true });
    }
  }

  private schedulePreviewOverlayRefresh(): void {
    if (typeof window === 'undefined') {
      return;
    }

    if (this.overlayRefreshFrame !== null) {
      cancelAnimationFrame(this.overlayRefreshFrame);
    }

    this.overlayRefreshFrame = window.requestAnimationFrame(() => {
      this.overlayRefreshFrame = null;
      this.refreshPreviewOverlays();
    });
  }

  private refreshPreviewOverlays(): void {
    const content = this.renderedPreviewContentRef?.nativeElement;
    if (!content) {
      this.previewSectionOverlays.set([]);
      return;
    }

    const taggedNodes = Array.from(content.querySelectorAll<HTMLElement>('[data-editor-section-id]'));
    const fallbackNodes = taggedNodes.length > 0
      ? taggedNodes
      : Array.from(content.querySelectorAll<HTMLElement>('.cv-identity-stack, .cv-latex-header, .cv-section, .cv-meta-block'));
    const sectionNodes = fallbackNodes.filter((node) => node.getBoundingClientRect().width > 0 && node.getBoundingClientRect().height > 0);

    if (sectionNodes.length === 0) {
      this.previewSectionOverlays.set([]);
      return;
    }

    const contentRect = content.getBoundingClientRect();
    const contentWidth = Math.max(1, content.offsetWidth || content.clientWidth || 1);
    const contentHeight = Math.max(1, content.offsetHeight || content.clientHeight || 1);
    const scaleX = contentRect.width > 0 ? contentRect.width / contentWidth : 1;
    const scaleY = contentRect.height > 0 ? contentRect.height / contentHeight : 1;

    const grouped = new Map<string, {
      id: string;
      title: string;
      minTop: number;
      minLeft: number;
      maxRight: number;
      maxBottom: number;
      sortTop: number;
      sortLeft: number;
    }>();

    for (const node of sectionNodes) {
      const rect = node.getBoundingClientRect();
      const id = this.normalizeSectionId(node.dataset['editorSectionId']?.trim() || this.inferPreviewSectionId(node));
      if (!id) {
        continue;
      }

      const title = node.dataset['editorSectionTitle']?.trim() || this.inferPreviewSectionTitle(node, id);
      const relativeTop = (rect.top - contentRect.top) / scaleY;
      const relativeLeft = (rect.left - contentRect.left) / scaleX;
      const relativeRight = (rect.right - contentRect.left) / scaleX;
      const relativeBottom = (rect.bottom - contentRect.top) / scaleY;
      const current = grouped.get(id);

      if (!current) {
        grouped.set(id, {
          id,
          title,
          minTop: relativeTop,
          minLeft: relativeLeft,
          maxRight: relativeRight,
          maxBottom: relativeBottom,
          sortTop: relativeTop,
          sortLeft: relativeLeft,
        });
        continue;
      }

      current.minTop = Math.min(current.minTop, relativeTop);
      current.minLeft = Math.min(current.minLeft, relativeLeft);
      current.maxRight = Math.max(current.maxRight, relativeRight);
      current.maxBottom = Math.max(current.maxBottom, relativeBottom);
      current.sortTop = Math.min(current.sortTop, relativeTop);
      current.sortLeft = Math.min(current.sortLeft, relativeLeft);
      if (!current.title && title) {
        current.title = title;
      }
    }

    const overlays = Array.from(grouped.values())
      .map((entry) => {
        const sectionWidth = Math.max(1, entry.maxRight - entry.minLeft);
        const sectionHeight = Math.max(1, entry.maxBottom - entry.minTop);
        const adaptivePadding = Math.max(6, Math.min(14, Math.round(Math.min(sectionWidth, sectionHeight) * 0.08)));
        const top = Math.max(0, entry.minTop - adaptivePadding);
        const left = Math.max(0, entry.minLeft - adaptivePadding);
        const maxWidth = Math.max(1, contentWidth - left);
        const maxHeight = Math.max(1, contentHeight - top);
        const width = Math.max(40, Math.min(maxWidth, sectionWidth + adaptivePadding * 2));
        const height = Math.max(32, Math.min(maxHeight, sectionHeight + adaptivePadding * 2));

        return {
          id: entry.id,
          title: entry.title,
          top,
          left,
          width,
          height,
          sortTop: entry.sortTop,
          sortLeft: entry.sortLeft,
        };
      })
      .sort((a, b) => (a.sortTop - b.sortTop) || (a.sortLeft - b.sortLeft))
      .map((overlay, index) => ({
        id: overlay.id,
        title: overlay.title,
        top: overlay.top,
        left: overlay.left,
        width: overlay.width,
        height: overlay.height,
        order: index + 1,
      }));

    this.previewSectionOverlays.set(overlays);
  }

  private attachPreviewAssetListeners(): void {
    const content = this.renderedPreviewContentRef?.nativeElement;
    if (!content) {
      return;
    }

    this.detachPreviewAssetListeners();
    const images = Array.from(content.querySelectorAll<HTMLImageElement>('img'));
    for (const image of images) {
      const onLoadOrError = () => this.schedulePreviewOverlayRefresh();
      image.addEventListener('load', onLoadOrError);
      image.addEventListener('error', onLoadOrError);
      this.previewAssetCleanup.push(() => {
        image.removeEventListener('load', onLoadOrError);
        image.removeEventListener('error', onLoadOrError);
      });
    }
  }

  private detachPreviewAssetListeners(): void {
    for (const dispose of this.previewAssetCleanup) {
      dispose();
    }
    this.previewAssetCleanup = [];
  }

  private inferPreviewSectionId(node: HTMLElement): string | null {
    if (node.classList.contains('cv-identity-stack') || node.classList.contains('cv-latex-header')) {
      return 'header';
    }

    const heading = node.querySelector('h2, h3');
    const normalizedTitle = this.cleanText(heading?.textContent ?? '').toLowerCase();
    const titleMap: Record<string, string> = {
      header: 'header',
      resume: 'summary',
      summary: 'summary',
      experiences: 'experience',
      experience: 'experience',
      projects: 'projects',
      project: 'projects',
      education: 'education',
      skills: 'skills',
      'soft skills': 'softskills',
      certifications: 'certifications',
      languages: 'languages',
      activities: 'activities',
      accomplishments: 'accomplishments',
    };

    return this.normalizeSectionId(titleMap[normalizedTitle] ?? null);
  }

  private inferPreviewSectionTitle(node: HTMLElement, sectionId: string): string {
    const heading = this.cleanText(node.querySelector('h2, h3')?.textContent ?? '');
    return heading || (sectionId === 'header' ? 'Header' : this.sectionLabel(sectionId));
  }

  private normalizeSectionId(sectionId: string | null | undefined): string {
    const normalized = this.cleanText(sectionId).toLowerCase();
    const aliases: Record<string, string> = {
      header: 'header',
      summary: 'summary',
      resume: 'summary',
      experience: 'experience',
      experiences: 'experience',
      project: 'projects',
      projects: 'projects',
      education: 'education',
      skill: 'skills',
      skills: 'skills',
      softskills: 'softskills',
      'soft-skills': 'softskills',
      soft_skills: 'softskills',
      certification: 'certifications',
      certifications: 'certifications',
      language: 'languages',
      languages: 'languages',
      activity: 'activities',
      activities: 'activities',
      achievement: 'accomplishments',
      achievements: 'accomplishments',
      accomplishments: 'accomplishments',
    };
    return aliases[normalized] ?? normalized;
  }
}

