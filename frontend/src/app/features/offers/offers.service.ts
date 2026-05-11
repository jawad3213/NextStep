import { Injectable, signal, computed } from '@angular/core';
import { firstValueFrom, Observable, of } from 'rxjs';
import { delay } from 'rxjs/operators';

export interface JobOffer {
  id: string;
  title: string;
  company: string;
  location: string;
  contractType: string; // 'CDI' | 'CDD' | 'Stage' | 'Freelance'
  matchingScore?: number; // 0-100 or undefined
  tags: string[];
  status: 'non_traitee' | 'analyse_en_cours' | 'analysee' | 'cv_genere';
  daysLeft?: number;
  urgent?: boolean;
  rawText?: string;
  url?: string;
  analyzedDetails?: {
    keywords: string[];
    hardSkills: string[];
    softSkills: string[];
    summary: string;
  };
  matchingResults?: {
    matchScore: number;
    skillsGap: { missing: string[]; present: string[] };
    recommendations: string[];
  };
  generatedCvUrl?: string;
}

@Injectable({
  providedIn: 'root'
})
export class OffersService {
  // Mock Data aligned with the screenshot provided by user
  private readonly defaultOffers: JobOffer[] = [
    {
      id: '1',
      title: 'Full Stack Stage',
      company: 'Capgemini Maroc',
      location: 'Casablanca',
      contractType: 'Stage',
      matchingScore: 84,
      tags: ['React', 'Node.js', 'PostgreSQL'],
      status: 'cv_genere',
      daysLeft: 10,
      rawText: 'Recherche stagiaire Full Stack pour participer au développement d\'une application SaaS de recrutement intelligent. Stack technique : React, Node.js, PostgreSQL.',
      analyzedDetails: {
        keywords: ['SaaS', 'Full Stack', 'Recrutement', 'React', 'Node.js', 'PostgreSQL'],
        hardSkills: ['React', 'Node.js', 'PostgreSQL', 'SQL', 'REST API'],
        softSkills: ['Autonomie', 'Curiosité', 'Travail d\'équipe'],
        summary: 'Opportunité de stage de fin d\'études chez Capgemini Maroc pour travailler sur des technologies web modernes.'
      },
      matchingResults: {
        matchScore: 84,
        skillsGap: {
          present: ['React', 'PostgreSQL', 'SQL'],
          missing: ['Node.js', 'REST API']
        },
        recommendations: [
          'Mettez en avant vos projets universitaires utilisant PostgreSQL et SQL.',
          'Ajoutez une section sur vos bases théoriques en backend (Node.js/Express).'
        ]
      }
    },
    {
      id: '2',
      title: 'Développeur Java Spring',
      company: 'CGI',
      location: 'Rabat',
      contractType: 'CDI',
      tags: ['Java', 'Spring Boot'],
      status: 'non_traitee',
      rawText: 'Nous recrutons un Développeur Java Spring Boot expérimenté pour rejoindre nos équipes projets à Rabat. Vous participerez aux phases de conception, développement et tests unitaires.',
      analyzedDetails: undefined,
      matchingResults: undefined
    },
    {
      id: '3',
      title: 'DevOps Stage',
      company: 'OCP Group',
      location: 'Jorf Lasfar',
      contractType: 'Stage',
      matchingScore: 71,
      tags: ['Docker', 'Kubernetes'],
      status: 'analysee',
      rawText: 'Intégrez la direction digitale de l\'OCP à Jorf Lasfar en tant que stagiaire DevOps. Missions : mise en place de pipelines CI/CD, conteneurisation des services existants sous Docker et orchestration via Kubernetes.',
      analyzedDetails: {
        keywords: ['DevOps', 'CI/CD', 'Conteneurisation', 'Docker', 'Kubernetes'],
        hardSkills: ['Docker', 'Kubernetes', 'CI/CD', 'GitLab CI', 'Linux'],
        softSkills: ['Communication', 'Rigueur', 'Résolution de problèmes'],
        summary: 'Stage formateur au sein d\'une équipe DevOps expérimentée chez OCP.'
      },
      matchingResults: {
        matchScore: 71,
        skillsGap: {
          present: ['Docker', 'Linux', 'GitLab CI'],
          missing: ['Kubernetes']
        },
        recommendations: [
          'Suivez un cours rapide sur l\'architecture Kubernetes avant de lancer votre candidature.',
          'Détaillez vos compétences en scripts Linux et configuration de fichiers Dockerfile.'
        ]
      }
    },
    {
      id: '4',
      title: 'Data Engineer CDI',
      company: 'Maroc Telecom',
      location: 'Rabat',
      contractType: 'CDI',
      matchingScore: 58,
      tags: ['Python', 'Spark', 'Azure'],
      status: 'analysee',
      daysLeft: 2,
      urgent: true,
      rawText: 'Maroc Telecom recherche son nouveau Data Engineer senior pour piloter l\'architecture analytique cloud. Expertise requise : Python, Spark, Azure Data Lake, et architectures Big Data.',
      analyzedDetails: {
        keywords: ['Data Engineer', 'Big Data', 'Analytique', 'Spark', 'Azure', 'Python'],
        hardSkills: ['Python', 'Spark', 'Azure', 'ETL', 'Databricks'],
        softSkills: ['Leadership', 'Force de proposition', 'Esprit d\'analyse'],
        summary: 'Rôle hautement stratégique chez l\'opérateur historique national pour centraliser les flux de données.'
      },
      matchingResults: {
        matchScore: 58,
        skillsGap: {
          present: ['Python', 'ETL'],
          missing: ['Spark', 'Azure', 'Databricks']
        },
        recommendations: [
          'Mettez en avant vos connaissances théoriques des architectures distribuées (Hadoop/Spark).',
          'Mentionnez toute expérience même minime sur AWS ou GCP, facilement transférable sur Azure.'
        ]
      }
    }
  ];

  // State Management via Signals
  offers = signal<JobOffer[]>(this.defaultOffers);
  activeOfferId = signal<string | null>(null);
  
  // UI Panel State
  searchTerm = signal<string>('');
  filterContract = signal<string>('');
  filterStatus = signal<string>('');
  viewMode = signal<'list' | 'grid'>('list');
  isSyncing = signal<boolean>(false);

  // Pipeline-Specific State (Legacy/Simulated - can be removed later)
  pipelineRunning = signal<boolean>(false);
  activePipelineStep = signal<number>(0); // 0: Idle, 1: Poste, 2: CV, 3: Analyse, 4: Optimisation (Complete!)
  pipelineLog = signal<{ message: string; type: 'info' | 'success' | 'warning' }[]>([]);
  runningOfferId = signal<string | null>(null);

  // Computed signals
  selectedOffer = computed(() => {
    const id = this.activeOfferId();
    return this.offers().find(o => o.id === id) || null;
  });

  constructor() {}

  // ==========================================================
  // Future API Integration methods
  // These make it extremely simple to connect with backend later.
  // Just replace "this.mock..." with "this.http.get/post/put/delete"
  // ==========================================================
  
  /**
   * Fetches job offers. Easily bindable to GET /api/offers
   */
  async getOffersApi(): Promise<JobOffer[]> {
    // Simulated API call with RxJS & Promises
    return firstValueFrom(of(this.offers()).pipe(delay(200)));
  }

  /**
   * Creates a new job offer. Easily bindable to POST /api/offers
   */
  async createOfferApi(newOffer: Omit<JobOffer, 'id' | 'status'>): Promise<JobOffer> {
    const created: JobOffer = {
      ...newOffer,
      id: Math.random().toString(36).substring(2, 9),
      status: 'non_traitee'
    };
    
    // Simulate API POST
    this.offers.update(prev => [created, ...prev]);
    return firstValueFrom(of(created).pipe(delay(300)));
  }

  /**
   * Triggers the full AI agent pipeline. Easily bindable to POST /api/pipeline/run
   */
  async runPipelineApi(offerId: string): Promise<JobOffer> {
    // Under the hood, this will talk to the FastAPI backend agents /pipeline endpoint
    return new Promise(resolve => setTimeout(() => {
      this.offers.update(prev => 
        prev.map(o => o.id === offerId ? { ...o, status: 'cv_genere', matchingScore: 87 } : o)
      );
      const updated = this.offers().find(o => o.id === offerId)!;
      resolve(updated);
    }, 500));
  }

  // ==========================================================
  // Interactive Simulation Loop (Wow Factor & Client Demo)
  // ==========================================================

  async triggerInteractivePipeline(offerId: string, onComplete?: () => void) {
    const target = this.offers().find(o => o.id === offerId);
    if (!target) return;

    this.runningOfferId.set(offerId);
    this.pipelineRunning.set(true);
    this.activePipelineStep.set(1);
    this.pipelineLog.set([]);

    const log = (msg: string, type: 'info' | 'success' | 'warning' = 'info') => {
      this.pipelineLog.update(prev => [...prev, { message: msg, type }]);
    };

    // --- STEP 1: OFFER ANALYZER NODE (Poste) ---
    log('Connexion aux agents d\'intelligence artificielle NextStep...', 'info');
    await this.delay(600);
    log('Node initialized: [offer_analyzer_node] actif', 'info');
    await this.delay(800);
    log('Lecture de la description du poste...', 'info');
    await this.delay(700);
    log('Extraction et normalisation des compétences requises...', 'info');
    await this.delay(900);
    log('Mots-clés ATS clés identifiés : ' + (target.tags.join(', ') || 'Java, Angular'), 'success');
    log('Analyse de l\'offre finalisée !', 'success');

    // --- STEP 2: PROFILE RETRIEVER NODE (CV) ---
    await this.delay(500);
    this.activePipelineStep.set(2);
    log('Node initialized: [profile_retriever_node] actif', 'info');
    await this.delay(800);
    log('Récupération de la dernière version du profil candidat...', 'info');
    await this.delay(700);
    log('Parsing des sections : Éducation, Expérience, Certifications...', 'info');
    await this.delay(900);
    log('Profil candidat récupéré avec succès !', 'success');

    // --- STEP 3: SKILL GAP NODE (Analyse) ---
    await this.delay(500);
    this.activePipelineStep.set(3);
    log('Node initialized: [skill_gap_node] actif', 'info');
    await this.delay(800);
    log('Lancement du modèle d\'évaluation sémantique de compétences...', 'info');
    await this.delay(700);
    log('Analyse d\'écarts (Skills Gap Analysis)...', 'info');
    await this.delay(900);
    
    // Simulate some scores
    const randomScore = Math.floor(Math.random() * 25) + 65; // between 65% and 90%
    log(`Calcul du score de compatibilité terminé : ${randomScore}% Match Score !`, 'success');

    // --- STEP 4: OPTIMISATION (CV Generation) ---
    await this.delay(500);
    this.activePipelineStep.set(4);
    log('Lancement de la phase d\'optimisation de candidature...', 'info');
    await this.delay(800);
    log('Génération de recommandations personnalisées pour maximiser le score...', 'info');
    await this.delay(1000);
    log('Génération d\'un CV sur-mesure aligné aux exigences ATS...', 'success');
    await this.delay(1000);

    // Update in-memory state with complete analysis
    this.offers.update(prev => 
      prev.map(o => {
        if (o.id === offerId) {
          return {
            ...o,
            status: 'cv_genere',
            matchingScore: randomScore,
            analyzedDetails: {
              keywords: [...o.tags, 'ATS Optimized', 'Architecture'],
              hardSkills: [...o.tags, 'Git', 'CI/CD'],
              softSkills: ['Adaptabilité', 'Communication', 'Rigueur'],
              summary: 'Profil optimisé par l\'IA pour correspondre aux critères du poste.'
            },
            matchingResults: {
              matchScore: randomScore,
              skillsGap: {
                present: [...o.tags],
                missing: ['Exigences avancées', 'Outils secondaires']
              },
              recommendations: [
                'Réorganisez vos compétences clés pour mettre l\'accent sur la stack technique demandée.',
                'Valorisez vos projets connexes dans la section correspondante de votre profil.'
              ]
            }
          };
        }
        return o;
      })
    );

    log('Candidature optimisée et enregistrée !', 'success');
    await this.delay(1200);

    // Finish pipeline
    this.pipelineRunning.set(false);
    this.activePipelineStep.set(0);
    this.runningOfferId.set(null);

    if (onComplete) {
      onComplete();
    }
  }

  private delay(ms: number): Promise<void> {
    return new Promise(resolve => setTimeout(resolve, ms));
  }
}
