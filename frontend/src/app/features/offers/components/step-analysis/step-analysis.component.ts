import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { Router } from '@angular/router';
import { PipelineResult, PipelineStateService } from '../../../../services/pipeline-state.service';
import { environment } from '../../../../../environments/environment';
import { ProfileService } from '../../../../services/profile.service';

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
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly profileService = inject(ProfileService);
  private readonly agentsBaseUrl = environment.agentsBaseUrl;

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
      title: 'C1 - Collecte de données',
      detail: 'Recherche web, actualités récentes, données publiques de l\'entreprise.'
    },
    {
      key: 'culture',
      title: 'C2 - Analyse culturelle & RH',
      detail: 'Évaluation des valeurs de l\'entreprise, environnement de travail et salaires.'
    },
    {
      key: 'prep',
      title: 'C3 - Préparation d\'entretien',
      detail: 'Génération des questions types (techniques et RH) pour le poste.'
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
    if (percent < 35) return "Recherche d'informations publiques sur l'entreprise...";
    if (percent < 70) return "Analyse de la culture d'entreprise et des salaires...";
    return "Génération des questions d'entretien...";
  }

  readonly agentStages: AgentStage[] = [
    {
      key: 'offer_analyzer',
      title: 'A1 - Analyse de l offre',
      detail: 'Lecture de l annonce, extraction du poste, de l entreprise et des attentes.'
    },
    {
      key: 'profile_retriever',
      title: 'A2 - Recuperation du profil',
      detail: 'Lecture de votre profil pour rapprocher les competences et les experiences.'
    },
    {
      key: 'skill_gap',
      title: 'A3 - Analyse du skill gap',
      detail: 'Calcul du matching, des manques prioritaires et des recommandations.'
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
    if (this.score >= 65) return 'Solide';
    if (this.score >= 45) return 'A renforcer';
    return 'Critique';
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
    return this.result?.offerTitle || 'Poste analyse';
  }

  get headlineCompany(): string {
    const val = this.result?.companyName;
    return (val && val !== 'null') ? val : 'Non spécifié';
  }

  get headlineLocation(): string {
    const val = this.result?.location;
    return (val && val !== 'null') ? val : 'Non spécifié';
  }

  get contractType(): string {
    return this.result?.contractType || 'Non spécifié';
  }

  get experienceLabel(): string {
    const years = this.result?.experienceYears;
    if (years == null) return 'Non spécifié';
    return `${years} an${years > 1 ? 's' : ''}`;
  }

  get educationLabel(): string {
    return this.result?.educationLevel || '';
  }

  get workModeLabel(): string {
    const data = this.result as unknown as Record<string, unknown> | null;
    const camel = typeof data?.['modeTravail'] === 'string' ? data['modeTravail'] as string : '';
    const snake = typeof data?.['mode_travail'] === 'string' ? data['mode_travail'] as string : '';
    return camel || snake || 'Non specifie';
  }

  get salaryLabel(): string {
    const min = this.result?.companySalaryMin;
    const max = this.result?.companySalaryMax;
    if (!min && !max) return '';
    if (min && max) return `${min}k€ - ${max}k€`;
    return `${min || max}k€`;
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
        title: r.priority === 'haute' ? 'Prioritaire' : r.priority === 'moyenne' ? 'Recommandé' : 'Optionnel',
        accent: r.priority === 'haute' ? 'red' as const : r.priority === 'moyenne' ? 'amber' as const : 'blue' as const,
        text: r.text
      }));
    }

    const recs = data?.recommendations?.length
      ? data.recommendations
      : this.buildFallbackRecommendations();

    const titles = [
      { title: 'Valoriser', accent: 'amber' as const },
      { title: 'Ajouter', accent: 'red' as const },
      { title: 'Adapter', accent: 'blue' as const }
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
      `Cette offre cible un profil ${this.headlineTitle.toLowerCase()}`,
      data.companyName ? `chez ${data.companyName}` : '',
      data.location ? `base a ${data.location}` : '',
      topMatches.length ? `avec deja un bon alignement sur ${topMatches.join(', ')}` : '',
      topMissing.length ? `et des points a renforcer sur ${topMissing.join(', ')}` : ''
    ].filter(Boolean);

    return `${fragments.join(' ')}. Le score actuel est de ${this.score}% avec un ATS a ${this.atsScore}%.`;
  }

  get companySupportTitle(): string {
    if (this.result?.companyName) return `A propos de ${this.result.companyName}`;
    return 'Etape suivante';
  }

  get companySupportText(): string {
    if ((this.result?.companyNews?.length ?? 0) > 0) {
      return 'Les premiers signaux de l entreprise ont ete recuperes. Vous pourrez les exploiter lors de la generation finale.';
    }

    return 'Cette etape se concentre uniquement sur les 3 premiers agents: analyse de l offre, recuperation du profil et skill gap. Les donnees entreprise detaillees arrivent ensuite.';
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
    const company = (res?.companyName && res?.companyName !== 'Non spécifié')
      ? res?.companyName
      : this.customCompanyName;

    if (!company || company === '' || company === 'Non spécifié') {
      this.companyIntelError = "Nom d'entreprise manquant pour lancer l'analyse detaillee.";
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

    this.pipeline.setLoading(true, "Recherche detaillee de l'entreprise en cours...");

    try {
      const fullProfile = await firstValueFrom(this.profileService.getFullProfile());
      const payload = {
        company_name: company,
        user_id: 0,
        profile_data: fullProfile ?? {},
        offer_data: {
          titre: this.headlineTitle || 'Poste',
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
        this.http.post<any>(`${this.agentsBaseUrl}/company/analyze-company`, payload)
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
        jobTitle: this.headlineTitle || 'Poste'
      }));
      this.persistCompanyHistory(apiRes, company, this.headlineTitle || 'Poste');

      await this.router.navigate(['/offers/company-analysis'], {
        state: {
          companyIntelPayload: apiRes,
          companyName: company,
          jobTitle: this.headlineTitle || 'Poste'
        }
      });
    } catch (e: any) {
      this.companyIntelError = e?.error?.detail || "Echec de l'analyse entreprise.";
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
    try {
      const raw = localStorage.getItem('nextstep.company.history');
      const current = raw ? JSON.parse(raw) : [];
      const normalized = Array.isArray(current) ? current : [];
      const company = (companyName || '').trim();
      const title = (jobTitle || '').trim();
      if (!company) return;

      const entry = {
        id: `${Date.now()}`,
        companyName: company,
        jobTitle: title,
        analyzedAt: new Date().toISOString(),
        data: {
          nom: apiRes?.intelligence?.nom ?? company,
          summary: apiRes?.intelligence?.summary ?? '',
          compatibilityScore: apiRes?.score ?? 0
        },
        rawPayload: apiRes
      };

      const deduped = normalized.filter((h: any) =>
        !(
          String(h?.companyName || '').toLowerCase() === company.toLowerCase() &&
          String(h?.jobTitle || '').toLowerCase() === title.toLowerCase()
        )
      );

      localStorage.setItem('nextstep.company.history', JSON.stringify([entry, ...deduped].slice(0, 20)));
    } catch {}
  }

  private buildFallbackRecommendations(): string[] {
    if (this.missingSkills.length > 0) {
      return this.missingSkills.slice(0, 3).map((skill) =>
        `Mettez en avant une experience, un projet ou une formation liee a ${skill}.`
      );
    }

    if (this.matchingSkills.length > 0) {
      return this.matchingSkills.slice(0, 3).map((skill) =>
        `Valorisez concretement ${skill} dans le CV avec un resultat ou un projet associe.`
      );
    }

    return ['Ajoutez des exemples concrets de vos competences principales pour renforcer la candidature.'];
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

