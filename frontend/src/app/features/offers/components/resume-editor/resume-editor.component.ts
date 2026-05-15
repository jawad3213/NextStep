import { Component, Input, Output, EventEmitter, signal, effect } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CdkDragDrop, moveItemInArray, DragDropModule } from '@angular/cdk/drag-drop';

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
}

export interface Skill {
  name: string;
  level: number;
  isMatched: boolean;
}

export interface Project {
  title: string;
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
  atsScore: number;
  matchingScore: number;
  atsCoveragePct: number;
}

@Component({
  selector: 'app-resume-editor',
  standalone: true,
  imports: [CommonModule, FormsModule, DragDropModule],
  providers: [DatePipe],
  templateUrl: './resume-editor.component.html',
  styleUrls: ['./resume-editor.component.scss']
})
export class ResumeEditorComponent {
  @Input() templateId: string = 'modern';
  @Output() back = new EventEmitter<void>();
  @Output() continue = new EventEmitter<void>();

  // DYNAMIC CHOSEN MODEL
  readonly activeTemplate = signal<string>('modern');

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
  });

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
        start: "2025-07-01",
        end: "2025-08-01",
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
    atsScore: 85,
    matchingScore: 85,
    atsCoveragePct: 85.0
  });

  // ACTIVE ACCORDION SECTION STATE
  readonly activeSection = signal<string | null>('coordonnees');

  toggleSection(section: string) {
    if (this.activeSection() === section) {
      this.activeSection.set(null); // Close if already open
    } else {
      this.activeSection.set(section); // Open the clicked one
    }
  }

  setTemplate(id: string) {
    this.activeTemplate.set(id);
  }

  onBack() {
    this.back.emit();
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
        { role: 'Nouveau Poste', company: 'Nouvelle Entreprise', start: '2026-01-01', end: 'Présent', bullets: ['Responsabilité clé ou réalisation technique accomplie.'] }
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
        { title: 'Nouveau Projet', bullets: ['Description de l\'architecture technique et de l\'impact du projet.'] }
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

  // ── Auto-save to localStorage ──
  private autoSave = effect(() => {
    this.cvData();
    clearTimeout(this.saveTimeout);
    this.saveTimeout = setTimeout(() => {
      localStorage.setItem('nextstep_cv_draft', JSON.stringify(this.cvData()));
      localStorage.setItem('nextstep_cv_visibility', JSON.stringify(this.sectionVisibility()));
      this.lastSaved.set(new Date().toLocaleTimeString());
    }, 1000);
  });

  ngOnInit() {
    this.activeTemplate.set(this.templateId);
    const saved = localStorage.getItem('nextstep_cv_draft');
    if (saved) {
      try {
        const parsed = JSON.parse(saved);
        this.cvData.set(parsed);
      } catch {}
    }
    const savedVis = localStorage.getItem('nextstep_cv_visibility');
    if (savedVis) {
      try {
        this.sectionVisibility.set(JSON.parse(savedVis));
      } catch {}
    }
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
    window.print();
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
