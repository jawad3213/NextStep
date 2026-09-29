import { Component, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { AuthService } from '@core/auth/auth.service';

interface PipelineStep {
  id: number;
  title: string;
  badge: string;
  shortDesc: string;
  headline: string;
  description: string;
  image: string;
  metrics: { label: string; value: string }[];
  highlights: string[];
}

interface RolePreset {
  role: string;
  company: string;
  location: string;
  salary: string;
  matchScore: number;
  category: string;
  matchedSkills: string[];
  missingSkills: string[];
  recommendation: string;
}

@Component({
  selector: 'app-landing',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule],
  templateUrl: './landing.component.html',
  styleUrl: './landing.component.scss'
})
export class LandingComponent {
  readonly authService = inject(AuthService);
  readonly router = inject(Router);

  // Hero interactive cockpit active tab
  readonly heroTab = signal<'offer' | 'skillgap' | 'cv' | 'gmail' | 'arena'>('offer');

  // CV template toggle
  readonly cvPreviewTemplate = signal<'modern' | 'latex'>('modern');

  // Active step in interactive pipeline showcase
  readonly activeStep = signal<number>(1);

  // Interactive ATS Simulator state
  readonly rolePresets: RolePreset[] = [
    {
      role: 'Full Stack Engineer (.NET 10 / Angular 19)',
      company: 'Qonto Tech',
      location: 'Paris (Hybrid)',
      salary: '$65k - $75k',
      matchScore: 94,
      category: 'Web Development',
      matchedSkills: ['Angular 19', 'C# / .NET 10', 'TypeScript', 'PostgreSQL', 'REST API', 'Docker'],
      missingSkills: ['Kubernetes', 'RabbitMQ'],
      recommendation: 'Very strong profile. Highlight your modular architectures and microservices.'
    },
    {
      role: 'DevOps & Cloud Architect',
      company: 'PayFit',
      location: 'Full Remote',
      salary: '$70k - $85k',
      matchScore: 89,
      category: 'Cloud Infrastructure',
      matchedSkills: ['Docker', 'CI/CD Pipelines', 'Linux', 'Terraform', 'Git', 'Monitoring'],
      missingSkills: ['ArgoCD', 'Prometheus'],
      recommendation: 'Optimal ATS score. Highlight deployment time reduction and high availability.'
    },
    {
      role: 'AI / Data Solutions Engineer',
      company: 'Mistral Partner',
      location: 'New York (Hybrid)',
      salary: '$72k - $88k',
      matchScore: 91,
      category: 'Data & AI',
      matchedSkills: ['Python', 'FastAPI', 'LangGraph', 'SQL', 'Embeddings', 'Git'],
      missingSkills: ['MLflow', 'Triton Server'],
      recommendation: 'Excellent fit for intelligent agents. Include your LLM latency and cost metrics.'
    },
    {
      role: 'Tech Lead Frontend & Design Systems',
      company: 'Doctolib',
      location: 'Remote',
      salary: '$75k - $90k',
      matchScore: 96,
      category: 'Frontend & UX',
      matchedSkills: ['Angular', 'Design System', 'TailwindCSS', 'SCSS', 'Web Performance', 'Micro-Frontends'],
      missingSkills: ['Storybook', 'Cypress'],
      recommendation: 'Near-perfect match! Emphasize your governance of reusable components.'
    }
  ];

  readonly selectedPreset = signal<RolePreset>(this.rolePresets[0]);
  readonly extraSkills = signal<string[]>(['Docker', 'PostgreSQL', 'TypeScript', 'TailwindCSS']);

  // Pipeline steps definition with dedicated 3D visualization images
  readonly pipelineSteps: PipelineStep[] = [
    {
      id: 1,
      title: '01. Job Sourcing',
      badge: 'Multi-Source Scraping',
      shortDesc: 'LinkedIn, Indeed & Glassdoor unified',
      headline: 'Centralize all opportunities without switching tabs',
      description: 'NextStep continuously extracts and normalizes job openings from LinkedIn, Indeed, and Glassdoor. Filter by target technologies, salary range, and remote flexibility, then promote postings into your active pipeline with one click.',
      image: '/pipeline-step-1.jpg',
      metrics: [
        { label: 'Connected Sources', value: 'LinkedIn + Indeed' },
        { label: 'Sourcing Time Saved', value: '75%' },
        { label: 'Tracking Statuses', value: 'Real-time Kanban' }
      ],
      highlights: [
        'Unified multi-platform aggregation without duplicates',
        'Advanced filters by keywords, seniority, and remote status',
        'Automatic classification: Saved, Selected, Archived',
        'Instant promotion to the AI analysis engine'
      ]
    },
    {
      id: 2,
      title: '02. ATS Matching',
      badge: 'Semantic Analysis',
      shortDesc: 'Match score and skill gap analysis',
      headline: 'Know your exact match rate before applying',
      description: 'The semantic engine analyzes the job description against your profile. It calculates the real ATS compatibility score, pinpoints missing required skills, and generates actionable advice to bridge skill gaps.',
      image: '/pipeline-step-2.jpg',
      metrics: [
        { label: 'Matching Accuracy', value: '98%' },
        { label: 'Gap Detection', value: 'Instant' },
        { label: 'Actionable Advice', value: 'Actionable' }
      ],
      highlights: [
        'Overall relevance and match score calculation (%)',
        'Mastered skills vs required skills mapping',
        'Visual severity indicator (Perfect / Minor / Critical)',
        'Skill bridge recommendations and suggested certifications'
      ]
    },
    {
      id: 3,
      title: '03. Resume Studio',
      badge: 'LaTeX & Modern PDF',
      shortDesc: 'Optimized Modern & LaTeX templates',
      headline: 'Generate high-impact resumes tailored to each job description',
      description: 'Say goodbye to generic resumes ignored by recruiters. NextStep rewrites and reprioritizes your bullet points to align with target keywords. Export in LaTeX or high-resolution Modern PDF hosted on MinIO.',
      image: '/pipeline-step-3.jpg',
      metrics: [
        { label: 'ATS-Compliant Format', value: '100%' },
        { label: 'Professional Templates', value: 'Modern & LaTeX' },
        { label: 'Export & Storage', value: 'PDF MinIO S3' }
      ],
      highlights: [
        'Impact-focused phrasing with quantified metrics',
        'Automatic keyword alignment to pass ATS filters',
        'Real-time live document preview',
        'Full history of generated resumes available to download anytime'
      ]
    },
    {
      id: 4,
      title: '04. Gmail Delivery',
      badge: 'OAuth2 & Polling',
      shortDesc: 'Personalized outreach and reply tracking',
      headline: 'Send applications and track responses automatically',
      description: 'Craft personalized cover emails powered by AI. Connect your Gmail account via OAuth2, send applications with attached resumes directly from your own email, and let automated agents classify incoming replies (interview, rejection, follow-up needed).',
      image: '/pipeline-step-4.jpg',
      metrics: [
        { label: 'Avg. Response Rate', value: '+78%' },
        { label: 'Direct Integration', value: 'Gmail OAuth2' },
        { label: 'Reply Classification', value: 'Automated AI' }
      ],
      highlights: [
        'Targeted outreach and follow-up email generation',
        'Direct delivery with secure attachment from your inbox',
        'Automatic interview invitation detection in email threads',
        'Reminders and follow-up alerts before deadlines pass'
      ]
    },
    {
      id: 5,
      title: '05. Interview Coach',
      badge: 'Interview Simulator',
      shortDesc: 'Mock interviews and salary negotiation',
      headline: 'Practice interviews and negotiate your salary with AI',
      description: 'Train with an AI mock interview simulator tailored to the target role and company. Leverage the Salary Coach to negotiate your compensation and access Company Intelligence to understand company culture.',
      image: '/pipeline-step-5.jpg',
      metrics: [
        { label: 'Interactive Simulation', value: 'Real-time' },
        { label: 'Salary Coach', value: 'Included' },
        { label: 'Company Intelligence', value: 'Deep Insights' }
      ],
      highlights: [
        'Technical and behavioral interview simulations',
        'Instant STAR feedback on your answers',
        'Salary Coach: compensation benchmarks and talking points',
        'Company Intel: business signals, culture, and key expectations'
      ]
    }
  ];

  selectHeroTab(tab: 'offer' | 'skillgap' | 'cv' | 'gmail' | 'arena'): void {
    this.heroTab.set(tab);
  }

  selectCvTemplate(template: 'modern' | 'latex'): void {
    this.cvPreviewTemplate.set(template);
  }

  selectStep(stepId: number): void {
    this.activeStep.set(stepId);
  }

  selectPreset(preset: RolePreset): void {
    this.selectedPreset.set(preset);
  }

  get currentStep(): PipelineStep {
    return this.pipelineSteps.find(s => s.id === this.activeStep()) || this.pipelineSteps[0];
  }

  navigateToApp(): void {
    this.router.navigate(['/dashboard']);
  }

  navigateToOffers(): void {
    this.router.navigate(['/offers']);
  }

  navigateToAuth(): void {
    if (this.authService.isAuthenticated()) {
      this.router.navigate(['/dashboard']);
    } else {
      this.authService.login();
    }
  }

  scrollToSection(sectionId: string): void {
    const el = document.getElementById(sectionId);
    if (el) {
      el.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
  }
}
