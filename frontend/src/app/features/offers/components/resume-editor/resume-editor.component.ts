import { Component, Input, Output, EventEmitter, signal, effect, OnInit, OnChanges, OnDestroy, SimpleChanges, inject } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CdkDragDrop, moveItemInArray, DragDropModule } from '@angular/cdk/drag-drop';
import { SafeResourceUrl } from '@angular/platform-browser';
import { SidebarService } from '../../../../shared/services/sidebar.service';

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
}

export interface Project {
  title: string;
  description?: string;
  technologies?: string[];
  dateRealisation?: string;
  bullets: string[];
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
  activities: string[];
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
export class ResumeEditorComponent implements OnInit, OnChanges {
  @Input() templateId: string = 'modern';
  @Input() offerId: string | null = null;
  @Input() initialData: any = null;
  @Input() previewImageUrl: string | null = null;
  @Input() previewUrl: SafeResourceUrl | null = null;
  @Input() isRenderingPreview: boolean = false;
  @Input() previewError: string | null = null;
  @Output() back = new EventEmitter<void>();
  @Output() continue = new EventEmitter<void>();
  @Output() download = new EventEmitter<void>();
  @Output() dataChange = new EventEmitter<CvGeneratedSchema>();
  @Output() templateChange = new EventEmitter<SupportedEditorTemplate>();
  private readonly sidebarService = inject(SidebarService);

  // DYNAMIC CHOSEN MODEL
  readonly activeTemplate = signal<string>('modern');
  readonly supportedTemplateIds: SupportedEditorTemplate[] = ['modern', 'latex'];
  readonly activePanelTab = signal<EditorPanelTab>('templates');
  readonly isPanelOpen = signal<boolean>(true);
  readonly activeAccentColor = signal<string>('green');
  readonly documentTitle = signal<string>('EL HAIL JAOUAD_Resume_4');
  readonly isEditingDocumentTitle = signal<boolean>(false);
  readonly draftDocumentTitle = signal<string>('EL HAIL JAOUAD_Resume_4');
  readonly activePreviewSection = signal<string | null>('summary');
  readonly editingSectionTitle = signal<string | null>(null);
  readonly sectionTitleOverrides = signal<Record<string, string>>({});
  readonly previewZoom = signal<number>(100);
  readonly minPreviewZoom = 70;
  readonly maxPreviewZoom = 130;
  private readonly previewZoomStep = 10;

  // SIMULATED OPTIMIZATION LOADING
  readonly isImproving = signal<boolean>(false);

  // Section visibility toggles
  readonly sectionVisibility = signal<any>({
    summary: true,
    experience: true,
    projects: true,
    education: true,
    skills: true,
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
      "Hackathon ENSA Winner",
      "Competitive Programming Club"
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
    { id: 'skills', label: 'Skills', placement: 'sidebar' as const },
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

  isSectionVisible(sectionId: string): boolean {
    return !!this.sectionVisibility()[sectionId];
  }

  setActivePreviewSection(sectionId: string): void {
    this.activePreviewSection.set(sectionId);
  }

  selectedSection(): CvSection | null {
    return this.orderedSections.find(section => section.id === this.activePreviewSection()) ?? null;
  }

  openSectionInspector(sectionId: string): void {
    this.setActivePreviewSection(sectionId);
    this.activePanelTab.set('sections');
    this.isPanelOpen.set(true);
  }

  startSectionTitleEdit(sectionId: string): void {
    this.editingSectionTitle.set(sectionId);
    this.activePreviewSection.set(sectionId);
  }

  stopSectionTitleEdit(): void {
    this.editingSectionTitle.set(null);
  }

  updateSectionTitle(sectionId: string, value: string): void {
    this.sectionTitleOverrides.update((titles) => ({
      ...titles,
      [sectionId]: value.trimStart(),
    }));
  }

  hidePreviewSection(sectionId: string): void {
    if (sectionId.startsWith('custom-')) {
      this.toggleCustomSectionVisibility(sectionId);
    } else {
      this.toggleSectionVisibility(sectionId);
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
      summary: 'description',
      experience: 'work',
      projects: 'deployed_code',
      education: 'school',
      skills: 'bolt',
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
        return {
          id,
          type: id,
          title: this.sectionLabel(id),
          placement: def.placement,
          isVisible: !!visibility[id],
          order,
          items: data.skills.map(skill => ({
            primaryText: skill.name,
            secondaryText: '',
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
            primaryText: activity,
            secondaryText: '',
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

  updateSkill(index: number, field: keyof Skill, value: any) {
    this.cvData.update(data => {
      const s = [...data.skills];
      s[index] = { ...s[index], [field]: value };
      return { ...data, skills: s };
    });
  }

  addSkill() {
    this.cvData.update(data => ({
      ...data,
      skills: [
        ...data.skills,
        { name: 'Nouveau Skill', level: 3, isMatched: true }
      ]
    }));
  }

  removeSkill(index: number) {
    this.cvData.update(data => ({
      ...data,
      skills: data.skills.filter((_, i) => i !== index)
    }));
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
      activities: [...data.activities, 'Nouvelle activite']
    }));
  }

  updateActivity(index: number, value: string): void {
    this.cvData.update(data => ({
      ...data,
      activities: data.activities.map((activity, activityIndex) => activityIndex === index ? value : activity)
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
    const override = this.sectionTitleOverrides()[sectionId]?.trim();
    return override || (this.sectionDefinitions.find(section => section.id === sectionId)?.label ?? sectionId);
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
  }

  ngOnDestroy(): void {
    this.sidebarService.setEditorFocusMode(false);
  }

  private hydrateFromGenerated(generated: any): void {
    const normalized = generated?.cvData ?? generated?.cv_data ?? generated;
    if (!normalized || typeof normalized !== 'object') {
      console.warn('[CV-PIPELINE] ResumeEditor hydrate skipped: invalid payload', { generated });
      return;
    }

    console.log('[CV-PIPELINE] ResumeEditor hydrating generated CV', {
      hasCandidate: !!normalized?.candidate,
      experienceCount: Array.isArray(normalized?.experience) ? normalized.experience.length : 0,
      skillsCount: Array.isArray(normalized?.skills) ? normalized.skills.length : 0,
      languagesCount: Array.isArray(normalized?.languages) ? normalized.languages.length : 0,
      hasSummary: !!normalized?.summary,
      keys: Object.keys(normalized),
    });

    this.cvData.update(current => ({
      ...current,
      candidate: {
        ...current.candidate,
        name: normalized?.candidate?.name ?? current.candidate.name,
        email: normalized?.candidate?.email ?? current.candidate.email,
        phone: normalized?.candidate?.phone ?? current.candidate.phone,
        location: normalized?.candidate?.location ?? current.candidate.location,
        title: normalized?.candidate?.title ?? current.candidate.title,
        photoUrl: normalized?.candidate?.photoUrl ?? current.candidate.photoUrl,
        linkedIn: normalized?.candidate?.linkedIn ?? current.candidate.linkedIn,
        gitHub: normalized?.candidate?.gitHub ?? current.candidate.gitHub,
        portfolio: normalized?.candidate?.portfolio ?? current.candidate.portfolio,
      },
      summary: normalized?.summary ?? current.summary,
      experience: Array.isArray(normalized?.experience) ? normalized.experience.map((e: any) => ({ ...e, start: this.toMonthValue(e?.start), end: this.toMonthValue(e?.end) })) : current.experience,
      education: Array.isArray(normalized?.education)
        ? normalized.education.map((education: any) => ({
            degree: String(education?.degree ?? ''),
            institution: String(education?.institution ?? ''),
            year: String(education?.year ?? ''),
            startYear: String(education?.startYear ?? ''),
            endYear: String(education?.endYear ?? ''),
          }))
        : current.education,
      skills: Array.isArray(normalized?.skills) ? normalized.skills : current.skills,
      projects: Array.isArray(normalized?.projects)
        ? normalized.projects.map((project: any) => ({
            title: String(project?.title ?? ''),
            description: String(project?.description ?? ''),
            technologies: Array.isArray(project?.technologies)
              ? project.technologies.map((technology: any) => String(technology ?? '')).filter(Boolean)
              : [],
            dateRealisation: this.toMonthValue(project?.dateRealisation),
            bullets: Array.isArray(project?.bullets)
              ? project.bullets.map((bullet: any) => String(bullet ?? '')).filter(Boolean)
              : [],
          }))
        : current.projects,
      certifications: Array.isArray(normalized?.certifications) ? normalized.certifications : current.certifications,
      languages: Array.isArray(normalized?.languages) ? normalized.languages : current.languages,
      activities: Array.isArray(normalized?.activities)
        ? normalized.activities.map((activity: any) => String(activity?.title ?? activity ?? '')).filter(Boolean)
        : current.activities,
      accomplishments: Array.isArray(normalized?.accomplishments) ? normalized.accomplishments : current.accomplishments,
      atsScore: normalized?.atsScore ?? current.atsScore,
      matchingScore: normalized?.matchingScore ?? current.matchingScore,
      atsCoveragePct: normalized?.atsCoveragePct ?? current.atsCoveragePct,
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

    for (const section of ordered) {
      const id = String(section?.id ?? section?.type ?? '').trim();
      if (!id) continue;

      if (standardIds.has(id)) {
        visibility[id] = section?.isVisible !== false;
        order.push(id);
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
              primaryText: String(item?.primaryText ?? ''),
              secondaryText: String(item?.secondaryText ?? ''),
              startDate: item?.startDate ?? null,
              endDate: item?.endDate ?? null,
              location: item?.location ?? null,
              description: item?.description ?? null,
              level: typeof item?.level === 'number' ? item.level : null,
              isMatched: !!item?.isMatched,
              bullets: Array.isArray(item?.bullets) ? item.bullets.map((bullet: any) => String(bullet ?? '')) : [],
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
    this.sectionVisibility.update(v => ({ ...v, [key]: !v[key] }));
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

  zoomIn(): void {
    this.previewZoom.update((value) => Math.min(value + this.previewZoomStep, this.maxPreviewZoom));
  }

  zoomOut(): void {
    this.previewZoom.update((value) => Math.max(value - this.previewZoomStep, this.minPreviewZoom));
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
}
