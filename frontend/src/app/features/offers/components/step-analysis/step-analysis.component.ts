import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { Router } from '@angular/router';
import { PipelineResult, PipelineStateService } from '../../data-access/pipeline-state.service';
import { ProfileApiService } from '@features/profile/data-access/profile-api.service';
import { extractApiError } from '@core/http/extract-api-error';
import { CompanyIntelApiService } from '@features/company-intel/data-access/company-intel-api.service';
import { saveCompanyAnalysis } from '@features/company-intel/data-access/company-history';

type SkillStatus = 'matched' | 'partial' | 'missing';
type SkillCategory = 'technical' | 'soft';
type KeywordTone = 'matched' | 'missing' | 'neutral';
type KeywordSize = 'sm' | 'md' | 'lg';

interface AgentStage {
  key: string;
  title: string;
  detail: string;
}

interface SkillInsight {
  name: string;
  status: SkillStatus;
  category: SkillCategory;
}

interface KeywordChip {
  label: string;
  tone: KeywordTone;
  size: KeywordSize;
}

interface RecommendationInsight {
  title: string;
  accent: 'amber' | 'red' | 'blue';
  text: string;
}

@Component({
  selector: 'app-step-analysis',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './step-analysis.component.html',
  styleUrl: './step-analysis.component.scss'
})
export class StepAnalysisComponent {
  readonly pipeline = inject(PipelineStateService);
  private readonly companyIntelApi = inject(CompanyIntelApiService);
  private readonly router = inject(Router);
  private readonly profileApi = inject(ProfileApiService);

  skillsOpen = true;
  softSkillsOpen = true;
  keywordsOpen = true;
  recommendationsOpen = true;
  companyInfoOpen = true;
  customCompanyName = '';
  companyIntelLoading = false;
  companyIntelError: string | null = null;
  companyIntelProgressPercent = 5;
  private companyIntelProgressInterval: any = null;

  readonly companyIntelStages = [
    {
      key: 'collect',
      title: 'C1 - Data Collection',
      detail: 'Web search, recent news, public company information.'
    },
    {
      key: 'culture',
      title: 'C2 - Cultural & HR Analysis',
      detail: 'Assessment of company values, work environment, and compensation.'
    },
    {
      key: 'prep',
      title: 'C3 - Interview Preparation',
      detail: 'Generation of typical interview questions (technical and behavioral) for the role.'
    }
  ];

  getCompanyIntelStageStatus(stageKey: string): 'done' | 'running' | 'todo' {
    if (!this.companyIntelLoading) return 'todo';
    const percent = this.companyIntelProgressPercent;
    if (stageKey === 'collect') {
      return percent >= 35 ? 'done' : 'running';
    }
    if (stageKey === 'culture') {
      if (percent < 35) return 'todo';
      return percent >= 70 ? 'done' : 'running';
    }
    if (stageKey === 'prep') {
      if (percent < 70) return 'todo';
      return percent >= 100 ? 'done' : 'running';
    }
    return 'todo';
  }

  get companyIntelProgressLabel(): string {
    const percent = this.companyIntelProgressPercent;
    if (percent < 35) return "Searching public company information...";
    if (percent < 70) return "Analyzing company culture and compensation...";
    return "Generating interview questions...";
  }

  readonly agentStages: AgentStage[] = [
    {
      key: 'offer_analyzer',
      title: 'A1 - Job Offer Analysis',
      detail: 'Parsing job posting, extracting role, company and requirements.'
    },
    {
      key: 'profile_retriever',
      title: 'A2 - Profile Retrieval',
      detail: 'Reading your profile to match skills and experiences.'
    },
    {
      key: 'skill_gap',
      title: 'A3 - Skill Gap Analysis',
      detail: 'Calculating match score, missing skills and recommendations.'
    }
  ];

  get result(): PipelineResult | null {
    return this.pipeline.pipelineResult();
  }

  get progress() {
    return this.pipeline.currentAgentProgress();
  }

  get error(): string | null {
    return this.pipeline.pipelineError();
  }

  get score(): number {
    const direct = this.result?.matchScore ?? 0;
    if (direct > 0) return direct;

    const total = this.totalSkillCount;
    if (!total) return 0;
    return Math.round((this.matchingSkillsCount / total) * 100);
  }

  get atsScore(): number {
    return this.result?.atsScore ?? 0;
  }

  get scoreLabel(): string {
    if (this.score >= 80) return 'Excellent';
    if (this.score >= 65) return 'Strong';
    if (this.score >= 45) return 'Needs Focus';
    return 'Critical';
  }

  get scoreAccent(): 'green' | 'amber' | 'red' {
    if (this.score >= 80) return 'green';
    if (this.score >= 60) return 'amber';
    return 'red';
  }

  get scoreRingOffset(): number {
    const radius = 70;
    const circumference = 2 * Math.PI * radius;
    return circumference - (this.score / 100) * circumference;
  }

  get headlineTitle(): string {
    return this.result?.offerTitle || 'Analyzed Role';
  }

  get headlineCompany(): string {
    const val = this.result?.companyName;
    return (val && val !== 'null') ? val : 'Not specified';
  }

  get headlineLocation(): string {
    const val = this.result?.location;
    return (val && val !== 'null') ? val : 'Not specified';
  }

  get contractType(): string {
    return this.result?.contractType || 'Not specified';
  }

  get experienceLabel(): string {
    const years = this.result?.experienceYears;
    if (years == null) return 'Not specified';
    return `${years} year${years > 1 ? 's' : ''}`;
  }

  get educationLabel(): string {
    return this.result?.educationLevel || '';
  }

  get workModeLabel(): string {
    const data = this.result as unknown as Record<string, unknown> | null;
    const camel = typeof data?.['modeTravail'] === 'string' ? data['modeTravail'] as string : '';
    const snake = typeof data?.['mode_travail'] === 'string' ? data['mode_travail'] as string : '';
    return camel || snake || 'Not specified';
  }

  get salaryLabel(): string {
    const min = this.result?.companySalaryMin;
    const max = this.result?.companySalaryMax;
    if (!min && !max) return '';
    if (min && max) return `$${min}k - $${max}k`;
    return `$${min || max}k`;
  }

  get profileStrengthsList(): string[] {
    return this.result?.profileStrengthsList ?? [];
  }

  get matchingSkillsCount(): number {
    return this.matchingSkills.length;
  }

  get missingSkillsCount(): number {
    return this.missingSkills.length;
  }

  get partialSkillsCount(): number {
    return this.skillInsights.filter((skill) => skill.status === 'partial').length;
  }

  get skillsCoveragePercent(): number {
    const total = this.totalSkillCount;
    if (!total) return this.score;
    return Math.round((this.matchingSkillsCount / total) * 100);
  }

  get totalSkillCount(): number {
    return this.skillInsights.length;
  }

  get matchingSkills(): string[] {
    const data = this.result;
    if (!data) return [];
    return this.unique([...(data.matchingSkills ?? [])]);
  }

  get missingSkills(): string[] {
    const data = this.result;
    if (!data) return [];

    const explicitMissing = this.unique([...(data.missingSkills ?? [])]);
    if (explicitMissing.length > 0) return explicitMissing;

    const matchedSet = new Set(this.normalizeList(this.matchingSkills));
    const inferred = this.unique((data.requiredSkills ?? []).filter((skill) => !matchedSet.has(this.normalize(skill))));
    return inferred;
  }

  get technicalSkills(): SkillInsight[] {
    return this.skillInsights.filter((skill) => skill.category === 'technical');
  }

  get softSkills(): SkillInsight[] {
    return this.skillInsights.filter((skill) => skill.category === 'soft');
  }

  get matchedSkillsList(): SkillInsight[] {
    return this.skillInsights.filter((s) => s.status === 'matched');
  }

  get partialSkillsList(): SkillInsight[] {
    return this.skillInsights.filter((s) => s.status === 'partial');
  }

  get missingSkillsList(): SkillInsight[] {
    return this.skillInsights.filter((s) => s.status === 'missing');
  }

  // Categorized getters for Technical Skills
  get matchedTechnicalSkills(): SkillInsight[] {
    return this.matchedSkillsList.filter(s => s.category === 'technical');
  }
  get partialTechnicalSkills(): SkillInsight[] {
    return this.partialSkillsList.filter(s => s.category === 'technical');
  }
  get missingTechnicalSkills(): SkillInsight[] {
    return this.missingSkillsList.filter(s => s.category === 'technical');
  }

  // Categorized getters for Soft Skills
  get matchedSoftSkills(): SkillInsight[] {
    return this.matchedSkillsList.filter(s => s.category === 'soft');
  }
  get partialSoftSkills(): SkillInsight[] {
    return this.partialSkillsList.filter(s => s.category === 'soft');
  }
  get missingSoftSkills(): SkillInsight[] {
    return this.missingSkillsList.filter(s => s.category === 'soft');
  }

  get skillInsights(): SkillInsight[] {
    const data = this.result;
    if (!data) return [];

    if (data.skillDetails && data.skillDetails.length > 0) {
      return data.skillDetails.map(s => ({
        name: s.name,
        status: s.status === 'correspond' ? 'matched' as const
              : s.status === 'partiel' ? 'partial' as const
              : 'missing' as const,
        category: s.category === 'technique' ? 'technical' as const
                : s.category === 'soft' ? 'soft' as const
                : 'technical' as const
      }));
    }

    const matched = new Set(this.normalizeList(data.matchingSkills));
    const missing = new Set(this.normalizeList(data.missingSkills));
    const source = [
      ...(data.requiredSkills ?? []),
      ...(data.preferredSkills ?? []),
      ...(data.matchingSkills ?? []),
      ...(data.missingSkills ?? [])
    ];

    const uniqueSkills = this.unique(source);

    return uniqueSkills.map((skill) => {
      const normalized = this.normalize(skill);
      let status: SkillStatus = 'partial';

      if (matched.has(normalized)) status = 'matched';
      else if (missing.has(normalized)) status = 'missing';

      return {
        name: skill,
        status,
        category: this.isSoftSkill(skill) ? 'soft' : 'technical'
      };
    });
  }

  get keywordCloud(): KeywordChip[] {
    const data = this.result;
    if (!data) return [];

    if (data.keywordWeights && data.keywordWeights.length > 0) {
      const sorted = [...data.keywordWeights].sort((a, b) => b.weight - a.weight);
      const matched = new Set(this.normalizeList(data.keywordsPresent));
      const missing = new Set(this.normalizeList(data.keywordsMissing));

      return sorted.slice(0, 16).map(k => ({
        label: k.word,
        tone: matched.has(this.normalize(k.word)) ? 'matched' as const
            : missing.has(this.normalize(k.word)) ? 'missing' as const
            : 'neutral' as const,
        size: 'md' as const
      }));
    }

    const matched = this.unique(data.keywordsPresent || []);
    const missing = this.unique(data.keywordsMissing || []);
    const neutral = this.unique(
      (data.requiredSkills || []).filter((skill) => {
        const normalized = this.normalize(skill);
        return !matched.some((item) => this.normalize(item) === normalized)
          && !missing.some((item) => this.normalize(item) === normalized);
      })
    );

    return [
      ...matched.slice(0, 6).map(label => ({
        label,
        tone: 'matched' as const,
        size: 'md' as const
      })),
      ...missing.slice(0, 4).map(label => ({
        label,
        tone: 'missing' as const,
        size: 'md' as const
      })),
      ...neutral.slice(0, 6).map(label => ({
        label,
        tone: 'neutral' as const,
        size: 'md' as const
      }))
    ];
  }

  get matchedKeywordCloud(): KeywordChip[] {
    return this.keywordCloud.filter((k) => k.tone === 'matched');
  }

  get missingKeywordCloud(): KeywordChip[] {
    return this.keywordCloud.filter((k) => k.tone === 'missing');
  }

  get neutralKeywordCloud(): KeywordChip[] {
    return this.keywordCloud.filter((k) => k.tone === 'neutral');
  }

  get matchedKeywordPercent(): number {
    return this.keywordPercent(this.matchedKeywordCloud.length);
  }

  get missingKeywordPercent(): number {
    return this.keywordPercent(this.missingKeywordCloud.length);
  }

  get neutralKeywordPercent(): number {
    return this.keywordPercent(this.neutralKeywordCloud.length);
  }

  get recommendationInsights(): RecommendationInsight[] {
    const data = this.result;

    if (data?.recommendationsWithPriority && data.recommendationsWithPriority.length > 0) {
      return data.recommendationsWithPriority.map(r => ({
        title: r.priority === 'haute' ? 'High Priority' : r.priority === 'moyenne' ? 'Recommended' : 'Optional',
        accent: r.priority === 'haute' ? 'red' as const : r.priority === 'moyenne' ? 'amber' as const : 'blue' as const,
        text: r.text
      }));
    }

    const recs = data?.recommendations?.length
      ? data.recommendations
      : this.buildFallbackRecommendations();

    const titles = [
      { title: 'Highlight', accent: 'amber' as const },
      { title: 'Add', accent: 'red' as const },
      { title: 'Tailor', accent: 'blue' as const }
    ];

    return recs.slice(0, 3).map((text, index) => ({
      title: titles[index % titles.length].title,
      accent: titles[index % titles.length].accent,
      text
    }));
  }

  get analysisSummary(): string {
    const data = this.result;
    if (!data) return '';

    const topMatches = this.matchingSkills.slice(0, 3);
    const topMissing = this.missingSkills.slice(0, 2);

    const fragments = [
      `This offer targets a ${this.headlineTitle.toLowerCase()} role`,
      data.companyName ? `at ${data.companyName}` : '',
      data.location ? `based in ${data.location}` : '',
      topMatches.length ? `with strong alignment on ${topMatches.join(', ')}` : '',
      topMissing.length ? `and growth opportunities in ${topMissing.join(', ')}` : ''
    ].filter(Boolean);

    return `${fragments.join(' ')}. The current match score is ${this.score}% with an ATS compatibility of ${this.atsScore}%.`;
  }

  get companySupportTitle(): string {
    if (this.result?.companyName) return `About ${this.result.companyName}`;
    return 'Next Step';
  }

  get companySupportText(): string {
    if ((this.result?.companyNews?.length ?? 0) > 0) {
      return 'Initial company insights collected. You will be able to leverage them during final generation.';
    }

    return 'This stage focuses on the first 3 agents: offer analysis, profile retrieval, and skill gap assessment. Detailed company intel follows.';
  }

  get companyNews() {
    return this.result?.companyNews ?? [];
  }

  get hasCompanyIntelSnapshot(): boolean {
    return !!this.getCompanyIntelSnapshot();
  }

  get companyIntelSummary(): string {
    const snap = this.getCompanyIntelSnapshot();
    return snap?.companyIntelPayload?.intelligence?.summary || '';
  }

  async showCompanyIntelDetails(): Promise<void> {
    const snap = this.getCompanyIntelSnapshot();
    if (!snap) return;
    await this.router.navigate(['/offers/company-analysis'], {
      state: {
        companyIntelPayload: snap.companyIntelPayload,
        companyName: snap.companyName,
        jobTitle: snap.jobTitle
      }
    });
  }

  getAgentStatus(agentName: string): 'done' | 'running' | 'todo' {
    const current = this.progress?.agentName;
    if (!current) return 'todo';

    const order = this.agentStages.map((agent) => agent.key);
    const currentIndex = order.indexOf(current);
    const targetIndex = order.indexOf(agentName);

    if (targetIndex < currentIndex) return 'done';
    if (targetIndex === currentIndex) return 'running';
    return 'todo';
  }

  getSkillIcon(status: SkillStatus): string {
    if (status === 'matched') return 'OK';
    if (status === 'partial') return 'MID';
    return 'GAP';
  }

  goBack(): void {
    this.pipeline.pipelineError.set(null);
    this.pipeline.goToStep(1);
  }

  continueToTemplate(): void {
    this.pipeline.goToStep(3);
  }

  retry(): void {
    this.pipeline.pipelineError.set(null);
    this.pipeline.goToStep(1);
  }

  async searchCompanyDetailed(): Promise<void> {
    const res = this.result;
    const company = (res?.companyName && res?.companyName !== 'Non spécifié' && res?.companyName !== 'Not specified')
      ? res?.companyName
      : this.customCompanyName;

    if (!company || company === '' || company === 'Non spécifié' || company === 'Not specified') {
      this.companyIntelError = "Company name is required to start detailed analysis.";
      return;
    }

    this.companyIntelLoading = true;
    this.companyIntelError = null;
    this.companyIntelProgressPercent = 5;

    if (this.companyIntelProgressInterval) {
      clearInterval(this.companyIntelProgressInterval);
    }
    this.companyIntelProgressInterval = setInterval(() => {
      if (this.companyIntelProgressPercent < 95) {
        this.companyIntelProgressPercent += Math.floor(Math.random() * 3) + 1;
        if (this.companyIntelProgressPercent > 95) {
          this.companyIntelProgressPercent = 95;
        }
      }
    }, 200);

    this.pipeline.setLoading(true, "Detailed company search in progress...");

    try {
      const fullProfile = await firstValueFrom(this.profileApi.getFullProfile());
      const payload = {
        company_name: company,
        user_id: 0,
        profile_data: fullProfile ?? {},
        offer_data: {
          titre: this.headlineTitle || 'Role',
          entreprise: company,
          typeContrat: this.result?.contractType || '',
          localisation: this.result?.location || '',
          competencesRequises: this.result?.requiredSkills || [],
          competencesSouhaitees: this.result?.preferredSkills || [],
          keywordsAts: [
            ...(this.result?.keywordsPresent || []),
            ...(this.result?.keywordsMissing || []),
          ],
          anneesExperience: this.result?.experienceYears ?? 0,
          niveauEtudes: this.result?.educationLevel || '',
          modeTravail: this.workModeLabel || '',
          descriptionPoste: '',
        }
      };

      const apiRes = await firstValueFrom(
        this.companyIntelApi.analyzeCompany(payload)
      );

      this.companyIntelProgressPercent = 100;
      await new Promise((resolve) => setTimeout(resolve, 500));

      const interviewQuestions = apiRes?.intelligence?.interview_questions
        ?? apiRes?.intelligence?.interviewQuestions
        ?? [];
      localStorage.setItem('nextstep.company.interview_questions', JSON.stringify(interviewQuestions));
      sessionStorage.setItem('nextstep.company.last_payload', JSON.stringify({
        companyIntelPayload: apiRes,
        companyName: company,
        jobTitle: this.headlineTitle || 'Role'
      }));
      this.persistCompanyHistory(apiRes, company, this.headlineTitle || 'Role');

      await this.router.navigate(['/offers/company-analysis'], {
        state: {
          companyIntelPayload: apiRes,
          companyName: company,
          jobTitle: this.headlineTitle || 'Role'
        }
      });
    } catch (e: any) {
      this.companyIntelError = extractApiError(e).message || "Company analysis failed.";
    } finally {
      if (this.companyIntelProgressInterval) {
        clearInterval(this.companyIntelProgressInterval);
        this.companyIntelProgressInterval = null;
      }
      this.companyIntelLoading = false;
      this.pipeline.setLoading(false);
    }
  }

  private getCompanyIntelSnapshot():
    | { companyIntelPayload: any; companyName: string; jobTitle: string }
    | null {
    try {
      const raw = sessionStorage.getItem('nextstep.company.last_payload');
      if (!raw) return null;
      const parsed = JSON.parse(raw);
      if (!parsed?.companyIntelPayload) return null;

      // Ensure the snapshot belongs to the company currently displayed
      const targetCompany = (this.result?.companyName && this.result?.companyName !== 'Non spécifié')
        ? this.result.companyName
        : this.customCompanyName;

      if (targetCompany && targetCompany.trim().toLowerCase() !== parsed.companyName?.trim().toLowerCase()) {
        return null;
      }

      return parsed;
    } catch {
      return null;
    }
  }

  private persistCompanyHistory(apiRes: any, companyName: string, jobTitle: string): void {
    saveCompanyAnalysis(apiRes, companyName, jobTitle);
  }

  private buildFallbackRecommendations(): string[] {
    if (this.missingSkills.length > 0) {
      return this.missingSkills.slice(0, 3).map((skill) =>
        `Highlight an experience, project, or training related to ${skill}.`
      );
    }

    if (this.matchingSkills.length > 0) {
      return this.matchingSkills.slice(0, 3).map((skill) =>
        `Showcase ${skill} in your resume with tangible results or an associated project.`
      );
    }

    return ['Add concrete examples of your core competencies to strengthen your application.'];
  }

  keywordChipSizeClass(size: KeywordSize): string {
    if (size === 'lg') return 'px-3.5 py-2 text-base';
    if (size === 'md') return 'px-3 py-1.5 text-sm';
    return 'px-2.5 py-1 text-xs';
  }

  private keywordPercent(count: number): number {
    const total = this.keywordCloud.length;
    if (!total) return 0;
    return Math.round((count / total) * 100);
  }

  private isSoftSkill(skill: string): boolean {
    const value = this.normalize(skill);
    return [
      'communication',
      'esprit',
      'team',
      'collaboration',
      'leadership',
      'agile',
      'scrum',
      'autonomie',
      'anglais',
      'adaptabilite',
      'organisation',
      'rigueur',
      'problem',
      'analyse',
      'travail'
    ].some((token) => value.includes(token));
  }

  private unique(values: string[]): string[] {
    const seen = new Set<string>();
    const result: string[] = [];

    for (const value of values) {
      const trimmed = value?.trim();
      if (!trimmed) continue;

      const key = this.normalize(trimmed);
      if (seen.has(key)) continue;

      seen.add(key);
      result.push(trimmed);
    }

    return result;
  }

  private normalizeList(values: string[]): string[] {
    return values.map((value) => this.normalize(value));
  }

  private normalize(value: string): string {
    return value
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .toLowerCase()
      .trim();
  }
}

