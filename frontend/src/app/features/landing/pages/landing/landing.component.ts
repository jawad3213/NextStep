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
      location: 'Paris (Hybride)',
      salary: '65k - 75k€',
      matchScore: 94,
      category: 'Développement Web',
      matchedSkills: ['Angular 19', 'C# / .NET 10', 'TypeScript', 'PostgreSQL', 'REST API', 'Docker'],
      missingSkills: ['Kubernetes', 'RabbitMQ'],
      recommendation: 'Profil très fort. Mettez en avant vos architectures modulaires et vos microservices.'
    },
    {
      role: 'DevOps & Cloud Architect',
      company: 'PayFit',
      location: 'Full Remote',
      salary: '70k - 85k€',
      matchScore: 89,
      category: 'Infrastructure Cloud',
      matchedSkills: ['Docker', 'CI/CD Pipelines', 'Linux', 'Terraform', 'Git', 'Monitoring'],
      missingSkills: ['ArgoCD', 'Prometheus'],
      recommendation: 'Score ATS optimal. Mentionnez la réduction des temps de déploiement et la haute disponibilité.'
    },
    {
      role: 'AI / Data Solutions Engineer',
      company: 'Mistral Partner',
      location: 'Paris 9e',
      salary: '72k - 88k€',
      matchScore: 91,
      category: 'Data & IA',
      matchedSkills: ['Python', 'FastAPI', 'LangGraph', 'SQL', 'Embeddings', 'Git'],
      missingSkills: ['MLflow', 'Triton Server'],
      recommendation: 'Excellente adéquation avec les agents intelligents. Ajoutez vos métriques de latence/coûts LLM.'
    },
    {
      role: 'Tech Lead Frontend & Design Systems',
      company: 'Doctolib',
      location: 'Nantes / Remote',
      salary: '75k - 90k€',
      matchScore: 96,
      category: 'Frontend & UX',
      matchedSkills: ['Angular', 'Design System', 'TailwindCSS', 'SCSS', 'Web Performance', 'Micro-Frontends'],
      missingSkills: ['Storybook', 'Cypress'],
      recommendation: 'Match quasiment parfait ! Insistez sur votre gouvernance de composants réutilisables.'
    }
  ];

  readonly selectedPreset = signal<RolePreset>(this.rolePresets[0]);
  readonly extraSkills = signal<string[]>(['Docker', 'PostgreSQL', 'TypeScript', 'TailwindCSS']);

  // Pipeline steps definition with dedicated 3D visualization images
  readonly pipelineSteps: PipelineStep[] = [
    {
      id: 1,
      title: '01. Sourcing Offres',
      badge: 'Scraping Multi-Sources',
      shortDesc: 'LinkedIn, Indeed & Glassdoor réunis',
      headline: 'Centralisez toutes les opportunités sans changer d\'onglet',
      description: 'NextStep extrait et normalise en continu les offres depuis LinkedIn, Indeed et Glassdoor. Filtrez par technologies précises, fourchette salariale et télétravail, puis promouvez l\'offre en 1 clic dans votre pipeline actif.',
      image: '/pipeline-step-1.jpg',
      metrics: [
        { label: 'Sources connectées', value: 'LinkedIn + Indeed' },
        { label: 'Gain de temps sourcing', value: '75%' },
        { label: 'Statuts de suivi', value: 'Kanban temps réel' }
      ],
      highlights: [
        'Agrégation multi-plateformes unifiée sans doublons',
        'Filtres avancés par mots-clés, séniorité et remote',
        'Classification automatique : Sauvegardé, Sélectionné, Archivé',
        'Promotion instantanée vers le moteur d\'analyse IA'
      ]
    },
    {
      id: 2,
      title: '02. Matching ATS',
      badge: 'Analyse Sémantique',
      shortDesc: 'Score de match et compétences manquantes',
      headline: 'Connaissez votre taux de match exact avant de postuler',
      description: 'L\'algorithme sémantique décompose la fiche de poste et la confronte avec votre profil. Il calcule le score ATS réel, détecte les compétences requises manquantes et formule des suggestions concrètes pour combler les écarts.',
      image: '/pipeline-step-2.jpg',
      metrics: [
        { label: 'Précision du matching', value: '98%' },
        { label: 'Détection des lacunes', value: 'Instantanée' },
        { label: 'Pistes d\'amélioration', value: 'Actionnables' }
      ],
      highlights: [
        'Calcul du score de pertinence globale (%)',
        'Cartographie des compétences maîtrisées vs attendues',
        'Indicateur visuel de gravité (Perfect / Minor / Critical)',
        'Recommandations de ponts de compétences et certifications'
      ]
    },
    {
      id: 3,
      title: '03. Studio CV',
      badge: 'LaTeX & Modern PDF',
      shortDesc: 'Templates Modern & LaTeX optimisés',
      headline: 'Générez un CV haute fidélité taillé sur-mesure pour chaque poste',
      description: 'Fini le CV générique ignoré par les recruteurs. NextStep réécrit et réordonne vos points d\'impact pour correspondre précisément aux mots-clés de l\'offre ciblée. Exportez en LaTeX ou Modern PDF haute résolution hébergé sur MinIO.',
      image: '/pipeline-step-3.jpg',
      metrics: [
        { label: 'Format conforme ATS', value: '100%' },
        { label: 'Templates professionnels', value: 'Modern & LaTeX' },
        { label: 'Export & Stockage', value: 'PDF MinIO S3' }
      ],
      highlights: [
        'Formulation orientée impact et métriques quantifiées',
        'Alignement automatique des mots-clés pour passer les filtres ATS',
        'Prévisualisation temps réel du document',
        'Historique complet des CV générés téléchargeables à tout moment'
      ]
    },
    {
      id: 4,
      title: '04. Envoi Gmail',
      badge: 'OAuth2 & Polling',
      shortDesc: 'Emails personnalisés et tracking des réponses',
      headline: 'Envoyez vos candidatures et suivez les réponses directement',
      description: 'Rédigez des emails de motivation personnalisés et percutants grâce à l\'IA. Connectez votre compte Gmail en OAuth2, expédiez votre candidature avec CV attaché et laissez les agents classifier les réponses reçues (entretien, refus, relance requise).',
      image: '/pipeline-step-4.jpg',
      metrics: [
        { label: 'Taux de réponse moyen', value: '+78%' },
        { label: 'Intégration directe', value: 'Gmail OAuth2' },
        { label: 'Classification des retours', value: 'IA automatique' }
      ],
      highlights: [
        'Génération d\'emails d\'accroche et de relance ciblés',
        'Envoi direct avec pièce jointe sécurisée depuis votre propre adresse',
        'Détection automatique des invitations à un entretien dans vos threads',
        'Rappels et notifications de relance avant expiration du délai'
      ]
    },
    {
      id: 5,
      title: '05. Coach Entretien',
      badge: 'Simulateur d\'Entretien',
      shortDesc: 'Entraînement questions-réponses et négociation',
      headline: 'Préparez vos entretiens et négociez votre salaire avec l\'IA',
      description: 'Entraînez-vous face à un simulateur d\'entretien IA qui adapte ses questions au poste ciblé et à l\'entreprise. Bénéficiez d\'un Salary Coach pour négocier votre rémunération et d\'un module Company Intelligence pour décoder la culture de l\'employeur.',
      image: '/pipeline-step-5.jpg',
      metrics: [
        { label: 'Simulation interactive', value: 'Temps réel' },
        { label: 'Coach salarial', value: 'Inclus' },
        { label: 'Company Intelligence', value: 'Deep Insights' }
      ],
      highlights: [
        'Simulateur d\'entretien technique et comportemental',
        'Feedback instantané sur la pertinence de vos réponses',
        'Salary Coach : arguments et fourchettes de rémunération',
        'Company Intel : signaux d\'entreprise, culture et attentes clés'
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
