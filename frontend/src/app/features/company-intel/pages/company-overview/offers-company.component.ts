import { Component, ElementRef, OnDestroy, ViewChild, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { extractApiError } from '@core/http/extract-api-error';
import { ToastService } from '@core/notifications/toast.service';
import { CompanyIntelApiService } from '../../data-access/company-intel-api.service';
import {
  CompanyHistoryItem,
  loadCompanyHistory,
  removeCompanyAnalysis,
  saveCompanyAnalysis,
} from '../../data-access/company-history';

/** What the research pipeline is doing, shown while the (single) request runs. */
const RESEARCH_STAGES = [
  { label: 'Searching the web for the company', icon: 'travel_explore', afterMs: 0 },
  { label: 'Collecting reviews, salaries and news', icon: 'reviews', afterMs: 6000 },
  { label: 'Analysing culture and fit with your profile', icon: 'psychology', afterMs: 16000 },
  { label: 'Writing the company report', icon: 'edit_note', afterMs: 28000 },
] as const;

@Component({
  selector: 'app-offers-company',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="history-container animate-fade-in">
      <div class="history-header">
        <div>
          <h2 class="history-title">Company Intelligence</h2>
          <p class="history-subtitle">Research any company with the AI pipeline, or reopen your previous analyses.</p>
        </div>
        @if (!formOpen() && !researching()) {
          <button type="button" class="btn-research" (click)="openForm()">
            <span class="material-symbols-outlined text-[18px]">manage_search</span>
            Start company research
          </button>
        }
      </div>

      @if (formOpen() || researching()) {
        <section class="research-panel" aria-live="polite">
          @if (!researching()) {
            <form class="research-form" (ngSubmit)="startResearch()">
              <div class="research-fields">
                <label class="research-field">
                  <span>Company name *</span>
                  <input #companyInput name="company" [(ngModel)]="companyName" required maxlength="120"
                    placeholder="e.g. Capgemini, OCP Group, Orange Business" autocomplete="organization" />
                </label>
                <label class="research-field">
                  <span>Target role <em>(optional)</em></span>
                  <input name="role" [(ngModel)]="targetRole" maxlength="120"
                    placeholder="e.g. Software Engineer" />
                </label>
              </div>
              <p class="research-hint">
                The agents search the web (company overview, reviews, salaries, news) and write a report.
                This usually takes 10–60 seconds; a company analysed in the last 7 days is returned instantly.
              </p>
              @if (researchError()) {
                <p class="research-error">
                  <span class="material-symbols-outlined text-[16px]">error</span>{{ researchError() }}
                </p>
              }
              <div class="research-actions">
                <button type="button" class="btn-ghost" (click)="closeForm()">Cancel</button>
                <button type="submit" class="btn-research" [disabled]="!companyName.trim()">
                  <span class="material-symbols-outlined text-[18px]">rocket_launch</span>
                  Run research
                </button>
              </div>
            </form>
          } @else {
            <div class="research-progress">
              <div class="research-progress-head">
                <div class="company-logo">{{ getInitials(activeCompany()) }}</div>
                <div>
                  <h3 class="company-name">Researching {{ activeCompany() }}</h3>
                  <p class="research-hint">{{ elapsedSeconds() }}s elapsed · you can keep this page open</p>
                </div>
                <button type="button" class="btn-ghost ml-auto" (click)="cancelResearch()">Cancel</button>
              </div>
              <ol class="stage-list">
                @for (stage of stages; track stage.label; let i = $index) {
                  <li class="stage" [class.done]="i < stageIndex()" [class.active]="i === stageIndex()">
                    <span class="material-symbols-outlined stage-icon">
                      {{ i < stageIndex() ? 'check_circle' : stage.icon }}
                    </span>
                    {{ stage.label }}
                  </li>
                }
              </ol>
            </div>
          }
        </section>
      }

      @if (history().length === 0) {
        @if (!formOpen() && !researching()) {
          <div class="empty-state">
            <span class="material-symbols-outlined empty-icon">analytics</span>
            <h3>No company analysis yet</h3>
            <p>Start a company research above, or analyse a company from an offer.</p>
          </div>
        }
      } @else {
        <div class="history-grid">
          @for (item of history(); track item.id) {
            <div class="history-card">
              <div class="card-body">
                <div class="company-brand">
                  <div class="company-logo">{{ getInitials(item.companyName) }}</div>
                  <div class="company-meta">
                    <h3 class="company-name">{{ item.companyName }}</h3>
                    <div class="job-badge">
                      <span class="material-symbols-outlined text-[14px]">work</span>
                      {{ item.jobTitle || 'No specific role' }}
                    </div>
                  </div>
                  <button type="button" class="btn-icon" title="Remove from history" (click)="remove(item)">
                    <span class="material-symbols-outlined text-[18px]">delete</span>
                  </button>
                </div>

                <div class="card-details">
                  <p class="summary-text">{{ item.data.summary || 'No summary available.' }}</p>

                  <div class="card-footer">
                    <div class="date-info">
                      <span class="material-symbols-outlined text-[14px]">calendar_today</span>
                      Analysed {{ item.analyzedAt | date:'d MMM yyyy, HH:mm' }}
                    </div>

                    <div class="footer-actions">
                      @if (item.data.compatibilityScore > 0) {
                        <div class="score-pill" [class.high]="item.data.compatibilityScore >= 70">
                          <span class="score-dot"></span>
                          {{ item.data.compatibilityScore }}% Match
                        </div>
                      } @else {
                        <span></span>
                      }
                      <div class="flex items-center gap-2">
                        <button type="button" class="btn-ghost btn-sm" [disabled]="researching()"
                          title="Run the research again" (click)="rerun(item)">
                          <span class="material-symbols-outlined text-[16px]">refresh</span>
                        </button>
                        <button class="btn-details" (click)="openDetails(item)">
                          Details
                          <span class="material-symbols-outlined text-[16px]">arrow_forward</span>
                        </button>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .history-container {
      padding: 32px 40px;
      max-width: 1200px;
      margin: 0 auto;
    }

    @media (max-width: 640px) {
      .history-container { padding: 20px 16px; }
    }

    .history-header {
      margin-bottom: 24px;
      display: flex;
      align-items: flex-end;
      justify-content: space-between;
      gap: 16px;
      flex-wrap: wrap;
    }

    .history-title {
      font-size: 24px;
      font-weight: 800;
      color: #0f172a;
      margin: 0 0 4px 0;
      font-family: 'Lato', sans-serif;
    }

    .history-subtitle {
      font-size: 14px;
      color: #64748b;
      margin: 0;
    }

    .btn-research {
      display: inline-flex;
      align-items: center;
      gap: 8px;
      padding: 10px 18px;
      background: #465fff;
      color: white;
      border: none;
      border-radius: 12px;
      font-size: 14px;
      font-weight: 700;
      cursor: pointer;
      transition: all 0.2s ease;
    }

    .btn-research:hover:not(:disabled) {
      background: #3641f5;
      box-shadow: 0 6px 16px rgba(70, 95, 255, 0.3);
    }

    .btn-research:disabled {
      background: #cbd5e1;
      cursor: not-allowed;
    }

    .btn-ghost {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      padding: 9px 16px;
      background: white;
      color: #475569;
      border: 1px solid #e2e8f0;
      border-radius: 10px;
      font-size: 13px;
      font-weight: 600;
      cursor: pointer;
      transition: all 0.2s ease;
    }

    .btn-ghost:hover:not(:disabled) { border-color: #cbd5e1; background: #f8fafc; }
    .btn-ghost:disabled { opacity: 0.5; cursor: not-allowed; }
    .btn-sm { padding: 5px 8px; border-radius: 8px; }

    .btn-icon {
      margin-left: auto;
      align-self: flex-start;
      display: inline-flex;
      padding: 4px;
      border: none;
      background: transparent;
      color: #94a3b8;
      border-radius: 8px;
      cursor: pointer;
    }

    .btn-icon:hover { color: #dc2626; background: #fef2f2; }

    .research-panel {
      background: white;
      border: 1px solid #c2d6ff;
      border-radius: 16px;
      padding: 20px 24px;
      margin-bottom: 28px;
      box-shadow: 0 8px 24px rgba(70, 95, 255, 0.06);
    }

    .research-fields {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 16px;
    }

    @media (max-width: 640px) {
      .research-fields { grid-template-columns: 1fr; }
    }

    .research-field span {
      display: block;
      margin-bottom: 6px;
      font-size: 12px;
      font-weight: 700;
      color: #475569;
      text-transform: uppercase;
      letter-spacing: 0.03em;
    }

    .research-field em { font-style: normal; font-weight: 500; text-transform: none; color: #94a3b8; }

    .research-field input {
      width: 100%;
      padding: 10px 14px;
      border: 1px solid #e2e8f0;
      border-radius: 10px;
      background: #f8fafc;
      font-size: 14px;
      outline: none;
      transition: all 0.15s ease;
    }

    .research-field input:focus { border-color: #465fff; background: white; box-shadow: 0 0 0 3px rgba(70, 95, 255, 0.12); }

    .research-hint { margin: 12px 0 0; font-size: 12.5px; color: #64748b; line-height: 1.5; }

    .research-error {
      display: flex;
      align-items: center;
      gap: 6px;
      margin: 12px 0 0;
      padding: 8px 12px;
      border-radius: 10px;
      background: #fef2f2;
      color: #b91c1c;
      font-size: 13px;
    }

    .research-actions { display: flex; justify-content: flex-end; gap: 10px; margin-top: 16px; }

    .research-progress-head { display: flex; align-items: center; gap: 12px; }
    .research-progress-head .research-hint { margin-top: 2px; }

    .stage-list { list-style: none; margin: 18px 0 0; padding: 0; display: grid; gap: 10px; }

    .stage {
      display: flex;
      align-items: center;
      gap: 10px;
      font-size: 14px;
      color: #94a3b8;
      transition: color 0.3s ease;
    }

    .stage-icon { font-size: 20px; }
    .stage.done { color: #16a34a; }
    .stage.active { color: #465fff; font-weight: 700; }
    .stage.active .stage-icon { animation: pulse 1.2s ease-in-out infinite; }

    @keyframes pulse {
      0%, 100% { opacity: 1; }
      50% { opacity: 0.35; }
    }

    .history-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
      gap: 24px;
    }

    @media (max-width: 400px) {
      .history-grid { grid-template-columns: 1fr; }
    }

    .history-card {
      background: white;
      border: 1px solid #e2e8f0;
      border-radius: 16px;
      transition: all 0.25s cubic-bezier(0.4, 0, 0.2, 1);
      display: flex;
      flex-direction: column;
      height: 100%;
      box-shadow: 0 1px 3px rgba(0, 0, 0, 0.02);
      overflow: hidden;
    }

    .history-card:hover {
      border-color: #c2d6ff;
      box-shadow: 0 8px 24px rgba(70, 95, 255, 0.08);
      transform: translateY(-3px);
    }

    .card-body {
      padding: 24px;
      display: flex;
      flex-direction: column;
      flex-grow: 1;
      height: 100%;
    }

    .company-brand {
      display: flex;
      align-items: center;
      gap: 12px;
      margin-bottom: 16px;
    }

    .company-logo {
      width: 44px;
      height: 44px;
      background: linear-gradient(135deg, #ecf3ff 0%, #dde9ff 100%);
      color: #465fff;
      display: flex;
      align-items: center;
      justify-content: center;
      border-radius: 12px;
      font-weight: 800;
      font-size: 15px;
      border: 1px solid #c2d6ff;
      flex-shrink: 0;
    }

    .company-meta {
      display: flex;
      flex-direction: column;
      min-width: 0;
      flex-grow: 1;
    }

    .company-name {
      font-size: 16px;
      font-weight: 700;
      color: #0f172a;
      margin: 0;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }

    .job-badge {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      padding: 2px 8px;
      background: #ecf3ff;
      color: #465fff;
      border-radius: 6px;
      font-size: 10.5px;
      font-weight: 700;
      margin-top: 4px;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
      max-width: 100%;
    }

    .card-details {
      display: flex;
      flex-direction: column;
      flex-grow: 1;
      justify-content: space-between;
    }

    .summary-text {
      font-size: 13px;
      color: #475569;
      line-height: 1.5;
      margin: 0 0 20px 0;
      display: -webkit-box;
      -webkit-line-clamp: 3;
      -webkit-box-orient: vertical;
      overflow: hidden;
      flex-grow: 1;
    }

    .card-footer {
      display: flex;
      flex-direction: column;
      gap: 12px;
      padding-top: 16px;
      border-top: 1px solid #f1f5f9;
      margin-top: auto;
    }

    .date-info {
      display: flex;
      align-items: center;
      gap: 6px;
      font-size: 11px;
      color: #94a3b8;
      font-weight: 500;
    }

    .footer-actions {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 8px;
    }

    .score-pill {
      display: flex;
      align-items: center;
      gap: 6px;
      padding: 4px 10px;
      background: #fef2f2;
      color: #dc2626;
      border-radius: 100px;
      font-size: 11px;
      font-weight: 700;
    }

    .score-pill.high {
      background: #f0fdf4;
      color: #16a34a;
    }

    .score-dot {
      width: 5px;
      height: 5px;
      background: currentColor;
      border-radius: 50%;
    }

    .btn-details {
      display: flex;
      align-items: center;
      gap: 4px;
      padding: 6px 14px;
      background: #465fff;
      color: white;
      border: none;
      border-radius: 8px;
      font-size: 11px;
      font-weight: 700;
      cursor: pointer;
      transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1);
    }

    .btn-details:hover {
      background: #3641f5;
      box-shadow: 0 4px 12px rgba(70, 95, 255, 0.3);
    }

    .empty-state {
      background: white;
      border: 1px dashed #cbd5e1;
      border-radius: 24px;
      padding: 60px 40px;
      text-align: center;
    }

    .empty-icon {
      font-size: 48px;
      color: #94a3b8;
      margin-bottom: 16px;
    }

    .empty-state h3 {
      font-size: 18px;
      color: #1e293b;
      margin: 0 0 8px 0;
    }

    .empty-state p {
      font-size: 14px;
      color: #64748b;
      margin: 0;
    }

    .animate-fade-in {
      animation: fadeIn 0.4s ease-out;
    }

    @keyframes fadeIn {
      from { opacity: 0; transform: translateY(10px); }
      to { opacity: 1; transform: translateY(0); }
    }
  `]
})
export class OffersCompanyComponent implements OnDestroy {
  private readonly router = inject(Router);
  private readonly companyIntelApi = inject(CompanyIntelApiService);
  private readonly toast = inject(ToastService);

  @ViewChild('companyInput') private companyInput?: ElementRef<HTMLInputElement>;

  readonly stages = RESEARCH_STAGES;
  readonly history = signal<CompanyHistoryItem[]>(loadCompanyHistory());

  readonly formOpen = signal(false);
  readonly researching = signal(false);
  readonly researchError = signal<string | null>(null);
  readonly activeCompany = signal('');
  readonly elapsedSeconds = signal(0);
  readonly stageIndex = computed(() => {
    const elapsedMs = this.elapsedSeconds() * 1000;
    let index = 0;
    RESEARCH_STAGES.forEach((stage, i) => { if (elapsedMs >= stage.afterMs) index = i; });
    return index;
  });

  companyName = '';
  targetRole = '';

  private researchSub: Subscription | null = null;
  private ticker: ReturnType<typeof setInterval> | null = null;

  ngOnDestroy(): void {
    // Leaving the page cancels the research request (the HTTP call is aborted).
    this.stopResearch();
  }

  openForm(prefillCompany = '', prefillRole = ''): void {
    this.companyName = prefillCompany;
    this.targetRole = prefillRole;
    this.researchError.set(null);
    this.formOpen.set(true);
    setTimeout(() => this.companyInput?.nativeElement.focus());
  }

  closeForm(): void {
    this.formOpen.set(false);
    this.researchError.set(null);
  }

  rerun(item: CompanyHistoryItem): void {
    this.openForm(item.companyName, item.jobTitle);
  }

  startResearch(): void {
    const company = this.companyName.trim();
    const role = this.targetRole.trim();
    if (!company || this.researching()) return;

    this.researchError.set(null);
    this.activeCompany.set(company);
    this.researching.set(true);
    this.elapsedSeconds.set(0);
    this.ticker = setInterval(() => this.elapsedSeconds.update(s => s + 1), 1000);

    // The research pipeline does not use the candidate profile, so none is sent (no personal data
    // leaves for nothing). The role only steers the salary lookup.
    this.researchSub = this.companyIntelApi.analyzeCompany({
      company_name: company,
      user_id: 0,
      profile_data: {},
      offer_data: {
        titre: role || 'Unknown',
        entreprise: company,
        competencesRequises: [],
        competencesSouhaitees: [],
        keywordsAts: [],
      },
    }).subscribe({
      next: (result) => this.onResearchDone(result, company, role),
      error: (err) => {
        this.stopResearch();
        this.researchError.set(extractApiError(err).message || 'The company research failed. Please try again.');
      },
    });
  }

  cancelResearch(): void {
    this.stopResearch();
    this.toast.info('Company research cancelled.');
  }

  getInitials(name: string): string {
    if (!name) return '??';
    return name.split(' ').filter(Boolean).map(n => n[0]).join('').toUpperCase().substring(0, 2);
  }

  remove(item: CompanyHistoryItem): void {
    this.history.set(removeCompanyAnalysis(item.id));
  }

  openDetails(item: CompanyHistoryItem): void {
    // The full agent payload holds culture, salaries, news...; the fallback only has the summary.
    const payload = item.rawPayload ?? {
      intelligence: { nom: item.companyName, summary: item.data.summary ?? '' },
      score: item.data.compatibilityScore,
    };
    this.navigateToAnalysis(payload, item.companyName, item.jobTitle);
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any -- raw agent payload
  private onResearchDone(result: any, company: string, role: string): void {
    this.stopResearch();
    if (result?.intelligence?.data_available === false) {
      // Honest empty report from the agents: keep the form open instead of showing a blank page.
      this.researchError.set(
        `No reliable information could be found about "${company}" right now. Check the spelling or try again later.`
      );
      return;
    }
    this.formOpen.set(false);
    this.history.set(saveCompanyAnalysis(result, company, role));
    if (result?.intelligence?.interview_questions) {
      localStorage.setItem('nextstep.company.interview_questions', JSON.stringify(result.intelligence.interview_questions));
    }
    this.toast.success(`Research on ${company} is ready.`);
    this.navigateToAnalysis(result, company, role);
  }

  private stopResearch(): void {
    this.researchSub?.unsubscribe();
    this.researchSub = null;
    if (this.ticker) clearInterval(this.ticker);
    this.ticker = null;
    this.researching.set(false);
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  private navigateToAnalysis(payload: any, companyName: string, jobTitle: string): void {
    this.router.navigate(['/offers/company-analysis'], {
      state: { companyIntelPayload: payload, companyName, jobTitle, origin: 'company-intel' },
    });
  }
}
