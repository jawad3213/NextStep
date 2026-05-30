import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { firstValueFrom } from 'rxjs';
import { Router } from '@angular/router';
import { PipelineStateService } from '../../services/pipeline-state.service';

interface SalaryInfo {
  jobTitle: string;
  location: string;
  minSalary: number;
  maxSalary: number;
  avgSalary: number;
  currency: string;
  source: string;
  seniority?: string;
}

interface CompanyIntel {
  nom: string;
  summary: string;
  sector: string;
  hqLocation: string;
  linkedinUrl: string;
  logoUrl?: string;
  culture: { cultureScore: number; turnoverRate: string; workLifeBalance: number; glassdoorRating: number; keyValues: string[]; topReviews: string[] };
  salaries: SalaryInfo[];
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
    <div class="analysis-shell animate-fade-in">
      <!-- HEADER -->
      <header class="mb-8">
        <button (click)="returnToOffer()" class="flex items-center gap-2 text-primary-brand hover:text-primary-brand font-bold text-sm mb-4 transition-colors">
          <span class="material-symbols-outlined text-lg">arrow_back</span>
          Retour à l'offre
        </button>
        <h1 class="font-outfit text-3xl font-black text-slate-900 mb-1">Company Intelligence</h1>
        <p class="text-slate-500 text-sm font-medium">Analyse approfondie des entreprises, culture, salaires et actualités.</p>
      </header>

      <!-- SEARCH INFO -->
      <div class="bg-white rounded-2xl p-6 shadow-sm border border-slate-200 mb-6">
        <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
          <div class="flex flex-col gap-1.5">
            <label class="text-[10px] font-black uppercase tracking-widest text-slate-400">Nom de l'entreprise</label>
            <input type="text" [ngModel]="companyName()" class="bg-slate-50 border border-slate-200 rounded-xl px-4 py-2.5 text-sm font-bold text-slate-700 outline-none" readonly />
          </div>
          <div class="flex flex-col gap-1.5">
            <label class="text-[10px] font-black uppercase tracking-widest text-slate-400">Intitule du poste</label>
            <input type="text" [ngModel]="jobTitle()" class="bg-slate-50 border border-slate-200 rounded-xl px-4 py-2.5 text-sm font-bold text-slate-700 outline-none" readonly />
          </div>
        </div>
      </div>

      @if (result(); as r) {
        <div class="grid grid-cols-12 gap-6 pb-20">
          
          <!-- HERO & SCORE -->
          <div class="col-span-12 lg:col-span-8">
            <div class="bg-white rounded-2xl p-6 shadow-sm border border-slate-200 h-full flex flex-col justify-center">
              <div class="flex items-center gap-6">
                <div class="w-16 h-16 rounded-xl bg-white flex items-center justify-center shadow-sm border border-slate-100 shrink-0 overflow-hidden">
                  <img *ngIf="r.logoUrl" [src]="r.logoUrl" (error)="r.logoUrl = undefined" class="w-full h-full object-contain p-2" [alt]="r.nom" />
                  <span *ngIf="!r.logoUrl" class="text-xl font-black text-slate-400">{{ getInitials(r.nom) }}</span>
                </div>
                <div class="flex-1">
                  <div class="flex items-center gap-4">
                    <h2 class="font-outfit text-2xl font-bold text-slate-900 leading-tight">{{ r.nom }}</h2>
                    @if (r.linkedinUrl) {
                      <a [href]="r.linkedinUrl" target="_blank" class="flex items-center gap-2 px-4 py-1.5 bg-[#0077B5] text-white rounded-full text-[9px] font-black uppercase tracking-widest transition-all hover:bg-[#005E93] shadow-sm">
                        <svg class="w-3 h-3 fill-current" viewBox="0 0 24 24"><path d="M19 0h-14c-2.761 0-5 2.239-5 5v14c0 2.761 2.239 5 5 5h14c2.762 0 5-2.239 5-5v-14c0-2.761-2.238-5-5-5zm-11 19h-3v-11h3v11zm-1.5-12.268c-.966 0-1.75-.79-1.75-1.764s.784-1.764 1.75-1.764 1.75.79 1.75 1.764-.783 1.764-1.75 1.764zm13.5 12.268h-3v-5.604c0-3.368-4-3.113-4 0v5.604h-3v-11h3v1.765c1.396-2.586 7-2.777 7 2.476v6.759z"/></svg>
                        LinkedIn
                      </a>
                    }
                  </div>
                  <div class="flex flex-wrap items-center gap-x-4 gap-y-2 text-slate-500 text-xs mt-3">
                    <span class="flex items-center gap-1.5">
                      <span class="material-symbols-outlined text-sm text-primary-brand">apartment</span>
                      {{ r.sector }}
                    </span>
                    <span class="text-slate-300">•</span>
                    <span class="flex items-center gap-1.5">
                      <span class="material-symbols-outlined text-sm text-primary-brand">location_on</span>
                      {{ r.hqLocation }}
                    </span>
                    <span class="text-slate-300">•</span>
                    <div class="flex items-center gap-1.5 px-2.5 py-0.5 rounded-full bg-green-50 text-green-700 border border-green-100">
                      <span class="material-symbols-outlined text-[14px]">check_circle</span>
                      <span class="font-medium">Analysée</span>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <div class="col-span-12 lg:col-span-4">
            <div class="bg-white rounded-2xl p-6 shadow-sm border border-slate-200 text-center relative overflow-hidden h-full flex flex-col justify-center min-h-[180px]">
              <h4 class="font-outfit font-bold text-base mb-4 text-slate-900">Matching Score</h4>
              <div class="relative w-28 h-28 mx-auto mb-2">
                <svg class="w-full h-full -rotate-90" viewBox="0 0 128 128">
                  <circle cx="64" cy="64" r="56" fill="none" stroke="#f1f5f9" stroke-width="10"/>
                  <circle cx="64" cy="64" r="56" fill="none" stroke-width="10" stroke-linecap="round"
                    stroke-dasharray="351.85"
                    [style.stroke-dashoffset]="351.85 - (r.compatibilityScore / 100) * 351.85"
                    [class.stroke-emerald-500]="r.compatibilityScore >= 70"
                    [class.stroke-amber-500]="r.compatibilityScore >= 40 && r.compatibilityScore < 70"
                    [class.stroke-red-500]="r.compatibilityScore < 40"
                    class="transition-all duration-1000 ease-out"
                  />
                </svg>
                <div class="absolute inset-0 flex flex-col items-center justify-center">
                  <span class="text-xl font-black font-outfit text-slate-900">{{ r.compatibilityScore }}%</span>
                  <span class="text-[7px] uppercase font-bold tracking-wider text-slate-400">Compatibilité</span>
                </div>
              </div>
            </div>
          </div>

          <!-- RESUME & CULTURE -->
          <div class="col-span-12 lg:col-span-7">
            <div class="bg-white rounded-2xl p-6 shadow-sm border border-slate-200 h-full">
              <div class="flex items-center gap-3 mb-6">
                <span class="material-symbols-outlined text-primary-brand">edit_note</span>
                <h3 class="font-outfit text-lg font-bold text-slate-900">Resume</h3>
              </div>
              <p class="text-slate-600 text-sm leading-relaxed">{{ r.summary }}</p>
            </div>
          </div>

          <div class="col-span-12 lg:col-span-5">
            <div class="bg-white rounded-2xl p-6 shadow-sm border border-slate-200 h-full">
              <div class="flex items-center gap-3 mb-6">
                <span class="material-symbols-outlined text-primary-brand">assignment</span>
                <h3 class="font-outfit text-lg font-bold text-slate-900">Culture</h3>
              </div>
              <div class="grid grid-cols-2 gap-y-4 gap-x-6 mb-6">
                <div class="flex flex-col">
                  <span class="text-[10px] font-bold text-slate-400 uppercase">Score culture</span>
                  <span class="text-lg font-black text-slate-800">{{ r.culture.cultureScore }}/10</span>
                </div>
                <div class="flex flex-col">
                  <span class="text-[10px] font-bold text-slate-400 uppercase">Turnover</span>
                  <span class="text-lg font-black text-slate-800">{{ r.culture.turnoverRate }}</span>
                </div>
                <div class="flex flex-col">
                  <span class="text-[10px] font-bold text-slate-400 uppercase">Work/Life</span>
                  <span class="text-lg font-black text-slate-800">{{ r.culture.workLifeBalance }}/5</span>
                </div>
                <div class="flex flex-col">
                  <span class="text-[10px] font-bold text-slate-400 uppercase">Glassdoor</span>
                  <span class="text-lg font-black text-slate-800">{{ r.culture.glassdoorRating }}/5</span>
                </div>
              </div>
              <div class="flex flex-wrap gap-2">
                @for (v of r.culture.keyValues; track v) {
                  <span class="px-2.5 py-1 bg-slate-100 text-slate-600 rounded-lg text-[11px] font-bold">{{ v }}</span>
                }
              </div>
            </div>
          </div>

          <!-- SALARIES -->
          <div class="col-span-12">
            <div class="bg-white rounded-2xl p-6 shadow-sm border border-slate-200">
              <div class="flex items-center gap-3 mb-6">
                <span class="material-symbols-outlined text-primary-brand">payments</span>
                <h3 class="font-outfit text-lg font-bold text-slate-900">Estimation Salariale</h3>
              </div>
              <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                @for (s of r.salaries; track s.avgSalary) {
                  <div class="bg-slate-50 p-5 rounded-xl border border-slate-100 relative group overflow-hidden">
                    <div class="flex items-start justify-between mb-4">
                      <p class="text-[10px] font-black uppercase tracking-widest text-slate-400 max-w-[70%]">{{ s.jobTitle }}</p>
                      <span class="px-2 py-0.5 rounded-full text-[9px] font-black uppercase tracking-tighter" 
                        [ngClass]="getSeniorityClass(s)">
                        {{ getSeniorityLabel(s) }}
                      </span>
                    </div>
                    <div class="flex items-baseline gap-2 mb-4">
                      <span class="text-2xl font-black text-slate-900">{{ s.avgSalary }}</span>
                      <span class="text-xs font-bold text-slate-400 uppercase tracking-tight">{{ s.currency }} / AN</span>
                    </div>
                    <div class="flex justify-between pt-4 border-t border-slate-200/60">
                      <div class="flex flex-col">
                        <span class="text-[9px] font-black text-slate-400 uppercase">Min</span>
                        <span class="text-xs font-bold text-slate-600">{{ s.minSalary }}</span>
                      </div>
                      <div class="flex flex-col text-right">
                        <span class="text-[9px] font-black text-slate-400 uppercase">Max</span>
                        <span class="text-xs font-bold text-slate-600">{{ s.maxSalary }}</span>
                      </div>
                    </div>
                  </div>
                }
              </div>
            </div>
          </div>

          <!-- PROS & CONS -->
          <div class="col-span-12 lg:col-span-6">
            <div class="bg-emerald-50/50 p-6 rounded-2xl border border-emerald-200/50 h-full">
              <div class="flex items-center gap-2 mb-4">
                <span class="material-symbols-outlined text-emerald-600">thumb_up</span>
                <h4 class="font-bold text-sm text-emerald-800 uppercase tracking-tight">Points Forts</h4>
              </div>
              <ul class="flex flex-col gap-3 p-0 m-0 list-none">
                @for (p of r.pros; track p) {
                  <li class="flex items-center gap-2 text-sm text-emerald-900 font-semibold">
                    <span class="material-symbols-outlined text-emerald-500 text-lg">check_circle</span>
                    {{ p }}
                  </li>
                }
              </ul>
            </div>
          </div>

          <div class="col-span-12 lg:col-span-6">
            <div class="bg-amber-50/50 p-6 rounded-2xl border border-amber-200/50 h-full">
              <div class="flex items-center gap-2 mb-4">
                <span class="material-symbols-outlined text-amber-600">priority_high</span>
                <h4 class="font-bold text-sm text-amber-800 uppercase tracking-tight">Points de Vigilance</h4>
              </div>
              <ul class="flex flex-col gap-3 p-0 m-0 list-none">
                @for (c of r.cons; track c) {
                  <li class="flex items-center gap-2 text-sm text-amber-900 font-semibold">
                    <span class="material-symbols-outlined text-amber-500 text-lg">warning</span>
                    {{ c }}
                  </li>
                }
              </ul>
            </div>
          </div>

          <!-- ACTUALITES -->
          @if (r.actualites.length > 0) {
            <div class="col-span-12">
              <div class="bg-white rounded-2xl p-6 shadow-sm border border-slate-200">
                <div class="flex items-center gap-3 mb-6">
                  <span class="material-symbols-outlined text-primary-brand">newspaper</span>
                  <h3 class="font-outfit text-lg font-bold text-slate-900">Actualités & Signaux</h3>
                </div>
                <div class="space-y-3">
                  @for (n of r.actualites; track n.title) {
                    <div class="flex items-center gap-4 p-4 hover:bg-slate-50 rounded-xl transition-all border border-transparent hover:border-slate-100">
                      <div class="w-10 h-10 rounded-lg bg-slate-50 flex items-center justify-center text-slate-300">
                        <span class="material-symbols-outlined">article</span>
                      </div>
                      <div class="flex-1">
                        <h4 class="text-sm font-bold text-slate-800 m-0">{{ n.title }}</h4>
                        @if (n.date || n.source) {
                          <p class="text-[10px] font-black text-slate-400 uppercase tracking-widest mt-1">
                            {{ n.date }}{{ n.date && n.source ? ' • ' : '' }}{{ n.source }}
                          </p>
                        }
                      </div>
                    </div>
                  }
                </div>
              </div>
            </div>
          }

          <!-- AI STRATEGY -->
          <div class="col-span-12">
            <div class="bg-gradient-to-br from-primary-brand/10 to-transparent p-6 rounded-2xl border border-primary-brand/20">
              <div class="flex items-center gap-3 mb-4">
                <span class="material-symbols-outlined text-primary-brand">auto_awesome</span>
                <h3 class="font-outfit text-lg font-bold text-slate-900">Conseils Stratégiques</h3>
              </div>
              <div class="flex flex-col gap-4">
                @for (rec of r.recommendations; track rec) {
                  <div class="flex items-start gap-3">
                    <span class="text-primary-brand font-bold mt-0.5">→</span>
                    <p class="text-slate-700 text-sm leading-relaxed italic font-medium m-0">{{ rec }}</p>
                  </div>
                }
              </div>
            </div>
          </div>

        </div>
      }
    </div>
  `,
  styles: [`
    :host { display: block; height: 100%; overflow-y: auto; background: #f8fafc; font-family: 'Lato', sans-serif; }
    .analysis-shell { padding: 32px 40px; max-width: 1200px; margin: 0 auto; }
    .font-outfit { font-family: 'Outfit', sans-serif; }
    .animate-fade-in { animation: fadeIn 0.4s ease-out; }
    @keyframes fadeIn { from { opacity: 0; transform: translateY(10px); } to { opacity: 1; transform: translateY(0); } }
    
    .stroke-emerald-500 { stroke: #10b981; }
    .stroke-amber-500 { stroke: #f59e0b; }
    .stroke-red-500 { stroke: #ef4444; }

    @media (max-width: 1024px) {
      .analysis-shell { padding: 20px; }
    }
  `]
})
export class CompanyIntelComponent implements OnInit {
  private http = inject(HttpClient);
  private router = inject(Router);
  private pipeline = inject(PipelineStateService);

  companyName = signal('');
  jobTitle = signal('');
  loading = signal(false);
  result = signal<CompanyIntel | null>(null);

  getSeniorityLabel(s: SalaryInfo): string {
    if (s.seniority) return s.seniority;
    const title = s.jobTitle.toLowerCase();
    if (title.includes('senior') || title.includes('lead') || title.includes('architect') || s.avgSalary > 28000) return 'Senior';
    if (title.includes('junior') || s.avgSalary < 18000) return 'Junior';
    return 'Intermédiaire';
  }

  getSeniorityClass(s: SalaryInfo): string {
    const label = this.getSeniorityLabel(s);
    if (label === 'Senior') return 'bg-primary-brand/10 text-primary-brand';
    if (label === 'Junior') return 'bg-slate-100 text-slate-500';
    return 'bg-blue-50 text-blue-600';
  }

  private getLogoUrl(name: string, linkedinUrl?: string): string {
    if (!name) return '';
    let domain = '';
    
    if (linkedinUrl && linkedinUrl.includes('/company/')) {
      domain = linkedinUrl.split('/company/')[1].split('/')[0].split('?')[0];
    } else {
      domain = name.toLowerCase().replace(/[^a-z0-9]/g, '');
    }
    
    if (!domain.includes('.')) domain += '.com';
    
    // Google V2 Favicon Service - Stable, High-Res (128px), and Clean
    return `https://t2.gstatic.com/faviconV2?client=SOCIAL&type=FAVICON&fallback_opts=TYPE,SIZE,URL&url=http://${domain}&size=128`;
  }

  returnToOffer() {
    const offerId = this.pipeline.currentOfferId();
    if (offerId) {
      this.router.navigate(['/offers/analyze'], { queryParams: { offerId } });
    } else {
      this.router.navigate(['/offers']);
    }
  }

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
      } catch { }
    }

    if (companyName) this.companyName.set(companyName);
    if (jobTitle) this.jobTitle.set(jobTitle);

    if (payload) {
      const mapped = this.mapApiResponse(payload);
      this.result.set(mapped);
      localStorage.setItem('nextstep.company.interview_questions', JSON.stringify(mapped.interviewQuestions ?? []));
      localStorage.setItem('nextstep.company.interview_difficulty', mapped.interviewDifficulty);

      if (!mapped.culture.cultureScore && !mapped.salaries.length && companyName && !this.loading()) {
        this.analyzeCompany();
      }
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
      localStorage.setItem('nextstep.company.interview_difficulty', mapped.interviewDifficulty);
    } catch { } finally {
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
      logoUrl: this.getLogoUrl(intel?.nom ?? this.companyName() ?? '', intel?.linkedin_url ?? intel?.linkedinUrl),
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
        source: s?.source ?? '',
        seniority: s?.seniority
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
    if (!name) return '??';
    return name.split(' ').map(w => w[0]).join('').toUpperCase().substring(0, 2);
  }
}
