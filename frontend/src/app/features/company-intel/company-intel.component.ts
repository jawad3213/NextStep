import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { firstValueFrom } from 'rxjs';
import { Router } from '@angular/router';

interface CompanyIntel {
  nom: string;
  summary: string;
  sector: string;
  hqLocation: string;
  linkedinUrl: string;
  culture: { cultureScore: number; turnoverRate: string; workLifeBalance: number; glassdoorRating: number; keyValues: string[]; topReviews: string[] };
  salaries: { jobTitle: string; location: string; minSalary: number; maxSalary: number; avgSalary: number; currency: string; source: string }[];
  actualites: { title: string; date: string; source: string; url: string }[];
  interviewDifficulty: string;
  interviewQuestions: string[];
  pros: string[];
  cons: string[];
  careerOpportunities: string[];
  compatibilityScore: number;
  recommendations: string[];
}

@Component({
  selector: 'app-company-intel',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="intel-shell">
      <header class="page-header">
        <div class="header-left">
          <h1 class="page-title">Company Intelligence</h1>
          <p class="page-subtitle">Analyse approfondie des entreprises, culture, salaires et actualites.</p>
        </div>
      </header>

      <div class="search-section">
        <div class="search-row">
          <div class="search-group">
            <label class="input-label">Nom de l'entreprise</label>
            <input type="text" [(ngModel)]="companyName" class="search-input" placeholder="ex: Capgemini, OCP, CGI..." (keyup.enter)="analyzeCompany()" />
          </div>
          <div class="search-group">
            <label class="input-label">Intitule du poste</label>
            <input type="text" [(ngModel)]="jobTitle" class="search-input" placeholder="ex: Full Stack Developer" (keyup.enter)="analyzeCompany()" />
          </div>
          <button class="btn-analyze" (click)="analyzeCompany()" [disabled]="!companyName() || loading()">
            @if (loading()) {
              <span class="spinner-sm"></span> Analyse...
            } @else {
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="11" cy="11" r="8"/><path d="m21 21-4.3-4.3"/></svg>
              Analyser
            }
          </button>
        </div>
      </div>

      @if (result(); as r) {
        <div class="results">
          <div class="company-header-card">
            <div class="company-brand">
              <div class="company-avatar">{{ getInitials(r.nom) }}</div>
              <div class="company-info">
                <h2>{{ r.nom }}</h2>
                <p class="company-meta">{{ r.sector }} • {{ r.hqLocation }}</p>
              </div>
            </div>
            <div class="compatibility-score" [class.high]="r.compatibilityScore >= 70" [class.mid]="r.compatibilityScore >= 40 && r.compatibilityScore < 70" [class.low]="r.compatibilityScore < 40">
              <span class="score-value">{{ r.compatibilityScore }}%</span>
              <span class="score-label">Compatibilite</span>
            </div>
          </div>

          <div class="grid-2col">
            <div class="info-card">
              <h3><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 20h9"/><path d="M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4L16.5 3.5z"/></svg> Resume</h3>
              <p>{{ r.summary }}</p>
            </div>
            <div class="info-card">
              <h3><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M6 2L3 6v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V6l-3-4z"/><line x1="3" y1="6" x2="21" y2="6"/><path d="M16 10a4 4 0 0 1-8 0"/></svg> Culture</h3>
              <div class="culture-metrics">
                <div class="metric"><span class="metric-label">Score culture</span><span class="metric-value">{{ r.culture.cultureScore }}/10</span></div>
                <div class="metric"><span class="metric-label">Turnover</span><span class="metric-value">{{ r.culture.turnoverRate }}</span></div>
                <div class="metric"><span class="metric-label">Work/Life</span><span class="metric-value">{{ r.culture.workLifeBalance }}/5</span></div>
                <div class="metric"><span class="metric-label">Glassdoor</span><span class="metric-value">{{ r.culture.glassdoorRating }}/5</span></div>
              </div>
              @if (r.culture.keyValues.length > 0) {
                <div class="tags">
                  @for (v of r.culture.keyValues; track v) { <span class="tag">{{ v }}</span> }
                </div>
              }
            </div>
          </div>

          @if (r.salaries.length > 0) {
            <div class="info-card">
              <h3><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="12" y1="1" x2="12" y2="23"/><path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"/></svg> Salaires</h3>
              <div class="salary-grid">
                @for (s of r.salaries; track s.jobTitle + s.location) {
                  <div class="salary-item">
                    <strong>{{ s.jobTitle }}</strong>
                    <p>{{ s.location }}: {{ s.minSalary }} - {{ s.maxSalary }} {{ s.currency }}</p>
                    <div class="salary-bar"><div class="salary-fill" [style.width.%]="((s.avgSalary - s.minSalary) / (s.maxSalary - s.minSalary || 1)) * 100"></div></div>
                    <span class="salary-avg">Moyenne: {{ s.avgSalary }} {{ s.currency }}</span>
                  </div>
                }
              </div>
            </div>
          }

          <div class="grid-2col">
            @if (r.pros.length > 0) {
              <div class="info-card pros">
                <h3><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M14 9V5a3 3 0 0 0-3-3l-4 9v11h11.28a2 2 0 0 0 2-1.7l1.38-9a2 2 0 0 0-2-2.3H14z"/></svg> Points forts</h3>
                <ul>@for (p of r.pros; track p) { <li>{{ p }}</li> }</ul>
              </div>
            }
            @if (r.cons.length > 0) {
              <div class="info-card cons">
                <h3><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M10 15v4a3 3 0 0 0 3 3l4-9V2H5.72a2 2 0 0 0-2 1.7l-1.38 9a2 2 0 0 0 2 2.3H10z"/></svg> Points faibles</h3>
                <ul>@for (c of r.cons; track c) { <li>{{ c }}</li> }</ul>
              </div>
            }
          </div>

          @if (r.actualites.length > 0) {
            <div class="info-card">
              <h3><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M4 22h16a2 2 0 0 0 2-2V4a2 2 0 0 0-2-2H8a2 2 0 0 0-2 2v16a2 2 0 0 1-2 2zm0 0a2 2 0 0 1-2-2v-9h2"/><path d="M18 14h-8"/><path d="M15 18h-5"/><path d="M10 6h8v4h-8V6z"/></svg> Actualites recentes</h3>
              <div class="news-list">
                @for (n of r.actualites; track n.title) {
                  <div class="news-item">
                    <div class="news-content">
                      <strong>{{ n.title }}</strong>
                      <span class="news-meta">{{ n.date }} • {{ n.source }}</span>
                    </div>
                  </div>
                }
              </div>
            </div>
          }

          @if (r.recommendations.length > 0) {
            <div class="info-card">
              <h3><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"/><polyline points="22 4 12 14.01 9 11.01"/></svg> Recommandations</h3>
              <ul>@for (rec of r.recommendations; track rec) { <li>{{ rec }}</li> }</ul>
            </div>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    :host { display: flex; flex-direction: column; flex: 1; height: 100%; min-height: 0; overflow-y: auto; }
    .intel-shell { display: flex; flex-direction: column; padding: 24px 40px; gap: 20px; }
    .page-header { display: flex; justify-content: space-between; align-items: flex-start; }
    .header-left { display: flex; flex-direction: column; gap: 4px; }
    .page-title { font-size: 24px; font-weight: 700; color: #212121; margin: 0; font-family: 'Lato', sans-serif; }
    .page-subtitle { font-size: 14px; color: #616161; margin: 0; }

    .search-section { background: white; border: 1px solid #E0E0E0; border-radius: 12px; padding: 20px; }
    .search-row { display: flex; gap: 12px; align-items: flex-end; }
    .search-group { flex: 1; display: flex; flex-direction: column; gap: 4px; }
    .input-label { font-size: 12px; font-weight: 700; color: #616161; }
    .search-input { height: 40px; padding: 0 12px; border: 1px solid #E0E0E0; border-radius: 4px; background: #F5F5F5; font-size: 13px; &:focus { outline: none; border-color: #1A91F0; background: white; box-shadow: 0 0 0 3px rgba(26,145,240,0.15); } }

    .btn-analyze { display: flex; align-items: center; gap: 8px; padding: 10px 24px; background: #0C1986; color: white; border: none; border-radius: 8px; font-size: 14px; font-weight: 600; cursor: pointer; transition: all 0.2s; white-space: nowrap; svg { width: 18px; height: 18px; } &:hover { background: #091361; } &:disabled { background: #BDBDBD; cursor: not-allowed; } }
    .spinner-sm { width: 16px; height: 16px; border: 2px solid rgba(255,255,255,0.3); border-top-color: white; border-radius: 50%; animation: spin 0.8s linear infinite; display: inline-block; }
    @keyframes spin { 100% { transform: rotate(360deg); } }

    .results { display: flex; flex-direction: column; gap: 16px; }
    .company-header-card { display: flex; justify-content: space-between; align-items: center; background: white; border: 1px solid #E0E0E0; border-radius: 12px; padding: 24px; }
    .company-brand { display: flex; align-items: center; gap: 16px; }
    .company-avatar { width: 56px; height: 56px; background: linear-gradient(135deg, #0C1986, #1A91F0); border-radius: 14px; display: flex; align-items: center; justify-content: center; color: white; font-weight: 800; font-size: 20px; }
    .company-info h2 { margin: 0; font-size: 22px; font-weight: 700; color: #212121; }
    .company-meta { margin: 4px 0 0; font-size: 13px; color: #616161; }
    .compatibility-score { text-align: center; padding: 12px 20px; border-radius: 100px; &.high { background: #E6F4EA; color: #34A853; } &.mid { background: #FFF8E6; color: #F59B00; } &.low { background: #FCE8E6; color: #D93025; } }
    .score-value { display: block; font-size: 24px; font-weight: 800; line-height: 1; }
    .score-label { font-size: 11px; font-weight: 600; text-transform: uppercase; letter-spacing: 0.05em; }

    .grid-2col { display: grid; grid-template-columns: 1fr 1fr; gap: 16px; }
    .info-card { background: white; border: 1px solid #E0E0E0; border-radius: 12px; padding: 20px; h3 { margin: 0 0 12px; font-size: 15px; font-weight: 700; color: #212121; display: flex; align-items: center; gap: 8px; svg { width: 18px; height: 18px; stroke: #0C1986; } } p { margin: 0; font-size: 13px; color: #424242; line-height: 1.6; } ul { margin: 0; padding-left: 20px; li { font-size: 13px; color: #424242; margin-bottom: 6px; } } }

    .culture-metrics { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; margin-bottom: 12px; }
    .metric { display: flex; flex-direction: column; gap: 2px; }
    .metric-label { font-size: 11px; color: #616161; }
    .metric-value { font-size: 16px; font-weight: 700; color: #212121; }
    .tags { display: flex; flex-wrap: wrap; gap: 6px; }
    .tag { padding: 3px 8px; background: #F1F5F9; border-radius: 4px; font-size: 11px; font-weight: 600; color: #475569; }

    .salary-grid { display: grid; gap: 12px; }
    .salary-item { background: #FAFAFA; border-radius: 8px; padding: 12px; strong { font-size: 13px; display: block; margin-bottom: 4px; } p { margin: 0 0 8px; font-size: 12px; color: #616161; } }
    .salary-bar { height: 6px; background: #E0E0E0; border-radius: 100px; overflow: hidden; margin-bottom: 4px; }
    .salary-fill { height: 100%; background: linear-gradient(90deg, #34A853, #0C1986); border-radius: 100px; }
    .salary-avg { font-size: 11px; color: #616161; }

    .news-list { display: flex; flex-direction: column; gap: 8px; }
    .news-item { display: flex; gap: 8px; padding: 8px 0; border-bottom: 1px solid #F1F5F9; &:last-child { border: none; } }
    .news-content strong { display: block; font-size: 13px; color: #212121; margin-bottom: 2px; }
    .news-meta { font-size: 11px; color: #616161; }

    .info-card.pros ul li { color: #34A853; }
    .info-card.cons ul li { color: #D93025; }
  `]
})
export class CompanyIntelComponent implements OnInit {
  private http = inject(HttpClient);
  private router = inject(Router);
  private baseUrl = environment.apiBaseUrl;

  companyName = signal('');
  jobTitle = signal('');
  loading = signal(false);
  result = signal<CompanyIntel | null>(null);

  ngOnInit(): void {
    const navState = this.router.getCurrentNavigation()?.extras?.state as any;
    const historyState = (window.history?.state ?? {}) as any;
    let payload = navState?.['companyIntelPayload'] ?? historyState?.['companyIntelPayload'];
    let companyName = navState?.['companyName'] ?? historyState?.['companyName'];
    let jobTitle = navState?.['jobTitle'] ?? historyState?.['jobTitle'];

    if (!payload) {
      try {
        const cached = sessionStorage.getItem('nextstep.company.last_payload');
        if (cached) {
          const parsed = JSON.parse(cached);
          payload = parsed?.companyIntelPayload ?? payload;
          companyName = parsed?.companyName ?? companyName;
          jobTitle = parsed?.jobTitle ?? jobTitle;
        }
      } catch {}
    }

    if (companyName) this.companyName.set(companyName);
    if (jobTitle) this.jobTitle.set(jobTitle);

    if (payload) {
      const mapped = this.mapApiResponse(payload);
      this.result.set(mapped);
      localStorage.setItem('nextstep.company.interview_questions', JSON.stringify(mapped.interviewQuestions ?? []));
    }
  }

  async analyzeCompany() {
    if (!this.companyName()) return;
    this.loading.set(true);
    try {
      const res = await firstValueFrom(this.http.post<any>(`${environment.agentsBaseUrl}/company/analyze-company`, {
        company_name: this.companyName(),
        user_id: 0,
        profile_data: {},
        offer_data: {
          titre: this.jobTitle() || 'Developpeur',
          entreprise: this.companyName(),
          competencesRequises: [],
          competencesSouhaitees: [],
          keywordsAts: []
        }
      }));
      const mapped = this.mapApiResponse(res);
      this.result.set(mapped);
      localStorage.setItem('nextstep.company.interview_questions', JSON.stringify(mapped.interviewQuestions ?? []));
    } catch {
      this.result.set(mockIntel);
    } finally {
      this.loading.set(false);
    }
  }

  private mapApiResponse(res: any): CompanyIntel {
    const intel = res?.intelligence ?? {};
    const culture = intel?.culture ?? {};
    return {
      nom: intel?.nom ?? this.companyName() ?? '',
      summary: intel?.summary ?? res?.summary ?? '',
      sector: intel?.sector ?? '',
      hqLocation: intel?.hq_location ?? intel?.hqLocation ?? '',
      linkedinUrl: intel?.linkedin_url ?? intel?.linkedinUrl ?? '',
      culture: {
        cultureScore: culture?.culture_score ?? culture?.cultureScore ?? 0,
        turnoverRate: culture?.turnover_rate ?? culture?.turnoverRate ?? '',
        workLifeBalance: culture?.work_life_balance ?? culture?.workLifeBalance ?? 0,
        glassdoorRating: culture?.glassdoor_rating ?? culture?.glassdoorRating ?? 0,
        keyValues: culture?.key_values ?? culture?.keyValues ?? [],
        topReviews: culture?.top_reviews ?? culture?.topReviews ?? []
      },
      salaries: (intel?.salaries ?? []).map((s: any) => ({
        jobTitle: s?.job_title ?? s?.jobTitle ?? '',
        location: s?.location ?? '',
        minSalary: s?.min_salary ?? s?.minSalary ?? 0,
        maxSalary: s?.max_salary ?? s?.maxSalary ?? 0,
        avgSalary: s?.avg_salary ?? s?.avgSalary ?? 0,
        currency: s?.currency ?? 'EUR',
        source: s?.source ?? ''
      })),
      actualites: (intel?.actualites ?? []).map((n: any) =>
        typeof n === 'string'
          ? { title: n, date: '', source: '', url: '' }
          : {
              title: n?.title ?? n?.titre ?? '',
              date: n?.date ?? '',
              source: n?.source ?? '',
              url: n?.url ?? ''
            }
      ),
      interviewDifficulty: intel?.interview_difficulty ?? intel?.interviewDifficulty ?? 'medium',
      interviewQuestions: intel?.interview_questions ?? intel?.interviewQuestions ?? [],
      pros: intel?.pros ?? [],
      cons: intel?.cons ?? [],
      careerOpportunities: intel?.career_opportunities ?? intel?.careerOpportunities ?? [],
      compatibilityScore: res?.score ?? res?.compatibilityScore ?? 0,
      recommendations: res?.recommendations ?? []
    };
  }

  getInitials(name: string): string {
    return name.split(' ').map(w => w[0]).join('').toUpperCase().substring(0, 2);
  }
}

const mockIntel: CompanyIntel = {
  nom: 'Capgemini Maroc',
  summary: 'Capgemini est l\'un des plus grands groupes de conseil et de services du numérique au monde. Présent au Maroc depuis 2007, le groupe emploie plus de 3000 collaborateurs répartis entre Casablanca, Rabat et Tanger. Reconnu pour ses pratiques innovantes en matière de transformation digitale.',
  sector: 'Tech / Consulting',
  hqLocation: 'Casablanca, Maroc',
  linkedinUrl: 'linkedin.com/company/capgemini',
  culture: { cultureScore: 7.8, turnoverRate: '12%', workLifeBalance: 3.5, glassdoorRating: 4.1, keyValues: ['Innovation', 'Collaboration', 'Integrite', 'Liberte'], topReviews: ['Bonne ambiance', 'Projets internationaux'] },
  salaries: [
    { jobTitle: 'Full Stack Developer', location: 'Casablanca', minSalary: 12000, maxSalary: 22000, avgSalary: 16000, currency: 'MAD', source: 'Glassdoor' },
    { jobTitle: 'Consultant Senior', location: 'Rabat', minSalary: 20000, maxSalary: 35000, avgSalary: 27000, currency: 'MAD', source: 'LinkedIn' },
  ],
  actualites: [
    { title: 'Capgemini remporte le contrat de transformation digitale de l\'ONCF', date: '2026-04-15', source: 'Le Matin', url: '' },
    { title: 'Capgemini Maroc lance son nouveau centre d\'innovation a Casablanca', date: '2026-03-20', source: 'Medias24', url: '' },
  ],
  interviewDifficulty: 'Moderee',
  interviewQuestions: ['Parlez-nous de votre experience avec Angular', 'Comment gereriez-vous un conflit d\'equipe ?'],
  pros: ['Projets internationaux', 'Bonne formation', 'Ambiance jeune'],
  cons: ['Salaire debutant modeste', 'Procedure interne lourde'],
  careerOpportunities: ['Evolution vers Consulting Senior', 'Mobilitie interne internationale'],
  compatibilityScore: 78,
  recommendations: ['Mettez en avant vos competences Angular et Node.js', 'Preparez des exemples concrets de travail en equipe agile'],
};
