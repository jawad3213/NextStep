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
  templateUrl: './company-intel.component.html',
  styleUrl: './company-intel.component.scss'
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
