import { Component, inject, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PipelineStateService, PipelineResult } from '../../../../services/pipeline-state.service';
import { OfferApiService, OfferAnalysisResponse } from '../../services/offer-api.service';
import { SidebarService } from '../../../../shared/services/sidebar.service';

@Component({
  selector: 'app-step-submit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './step-submit.component.html',
  styleUrl: './step-submit.component.scss'
})
export class StepSubmitComponent implements OnDestroy {
  pipeline = inject(PipelineStateService);
  private offerApiService = inject(OfferApiService);
  sidebarService = inject(SidebarService);

  mode: 'url' | 'text' = 'url';
  urlValue  = '';
  textValue = '';
  error     = '';

  get canSubmit(): boolean {
    if (this.pipeline.isLoading()) return false;
    if (this.mode === 'url')  return this.urlValue.trim().length > 10;
    if (this.mode === 'text') return this.textValue.trim().length > 50;
    return false;
  }

  get wordCount(): number {
    const trimmed = this.textValue.trim();
    if (!trimmed) return 0;
    return trimmed.split(/\s+/).length;
  }

  submit(): void {
    this.error = '';
    this.pipeline.pipelineError.set(null);
    this.pipeline.setLoading(true, 'Soumission en cours...');
    this.pipeline.offerUrl.set(this.urlValue);
    this.pipeline.offerText.set(this.textValue);

    const payload = {
      rawText: this.mode === 'text' ? this.textValue : this.urlValue,
      templateId: 1
    };

    // 1. Save the offer to get a real ID
    this.offerApiService.submitOffer(payload).subscribe({
      next: (response) => {
        this.pipeline.currentOfferId.set(response.offerId);
        this.pipeline.markStepDone(0);
        this.pipeline.goToStep(2);

        // Visual Progress Simulator
        const analysisStartTs = Date.now();
        const minAnalysisUxMs = 7200;
        const progressStages = [
          { agent: 'offer_analyzer', label: 'Analyse de la description du poste (Agent 1)...', percent: 15 },
          { agent: 'offer_analyzer', label: 'Extraction des compétences et mots-clés...', percent: 35 },
          { agent: 'profile_retriever', label: 'Récupération de votre CV (Agent 2)...', percent: 60 },
          { agent: 'skill_gap', label: 'Analyse des écarts et matching (Agent 3)...', percent: 85 }
        ];

        const updateProgress = (stageIdx: number) => {
          if (stageIdx < progressStages.length) {
            const stage = progressStages[stageIdx];
            this.pipeline.currentAgentProgress.set({
              step: 'analysis',
              agentName: stage.agent,
              label: stage.label,
              status: 'running',
              progressPercent: stage.percent
            });
          }
        };

        // Start simulator
        updateProgress(0);
        const timers: any[] = [];
        timers.push(setTimeout(() => updateProgress(1), 1800));
        timers.push(setTimeout(() => updateProgress(2), 3800));
        timers.push(setTimeout(() => updateProgress(3), 6500));

        // 2. Fire ONLY the 3 agents via the synchronous endpoint
        this.offerApiService.analyzeSync(response.offerId, payload.templateId).subscribe({
          next: (analysis) => {
            // Clear simulation timers
            timers.forEach(t => clearTimeout(t));

            const elapsedMs = Date.now() - analysisStartTs;
            const waitMs = Math.max(0, minAnalysisUxMs - elapsedMs);

            // Keep the 3-agent progression visible before showing final card.
            this.pipeline.currentAgentProgress.set({
              step: 'analysis',
              agentName: 'skill_gap',
              label: 'Finalisation de l analyse (Agent 3)...',
              status: 'running',
              progressPercent: 95
            });

            setTimeout(() => {
              const result = this.mapAnalysisToResult(analysis);
              this.pipeline.setResult(result);
              this.pipeline.setLoading(false);
              this.pipeline.markStepDone(1);
              this.pipeline.currentAgentProgress.set({
                step: 'analysis',
                agentName: 'skill_gap',
                label: 'Analyse terminee',
                status: 'done',
                progressPercent: 100
              });
              setTimeout(() => this.pipeline.currentAgentProgress.set(null), 450);
            }, waitMs);
          },
          error: (err) => {
            // Clear simulation timers
            timers.forEach(t => clearTimeout(t));

            const message = err?.status === 404
              ? 'Le endpoint d analyse /analyze-sync est introuvable sur le backend en cours. Redemarrez l API .NET pour charger la nouvelle route.'
              : err?.error?.error || err.message || 'Erreur lors de l\'analyse';

            this.pipeline.setLoading(false);
            this.pipeline.pipelineError.set(message);
            this.error = message;
            this.pipeline.currentAgentProgress.set(null);
          }
        });
      },
      error: (err) => {
        this.pipeline.setLoading(false);
        this.pipeline.pipelineError.set(err.message || 'Erreur de soumission');
        this.error = `Erreur : ${err.message || 'Service indisponible'}`;
      }
    });
  }

  /**
   * Map the backend OfferAnalysisDto (French fields) to the PipelineResult interface.
   */
  private mapAnalysisToResult(dto: OfferAnalysisResponse): PipelineResult {
    return {
      // Agent 1 — Offer Analysis
      offerTitle: dto.titre,
      companyName: dto.entreprise ?? '',
      contractType: dto.typeContrat ?? '',
      location: dto.localisation ?? undefined,
      requiredSkills: dto.competencesRequises ?? [],
      preferredSkills: dto.competencesSouhaitees ?? [],
      experienceYears: dto.anneesExperience ?? undefined,
      educationLevel: dto.niveauEtudes ?? undefined,

      // Agent 3 — Skill Gap & Matching
      matchScore: dto.scoreMatching,
      atsScore: dto.scoreAts,
      matchBreakdown: { skills: 0, experience: 0, location: 0 },
      keywordsPresent: dto.keywordsPresents ?? [],
      keywordsMissing: dto.keywordsManquants ?? [],
      matchingSkills: dto.competencesMatching ?? [],
      missingSkills: dto.competencesManquantes ?? [],
      recommendations: dto.recommandations ?? [],

      // Company Intel (not in Step 1 — defaults)
      companyCultureScore: dto.companyCultureScore ?? 0,
      companySalaryMin: dto.companySalaryMin ?? 0,
      companySalaryMax: dto.companySalaryMax ?? 0,
      companySize: dto.companySize ?? '',
      companyNews: dto.companyNews ?? [],

      // Later steps — defaults
      profileStrengths: [],
      skillGaps: [],
      cvPdfPath: '',
      atsImprovements: [],
      emailSubject: '',
      emailBody: '',
      recruiterName: '',
      coverLetterContent: '',

      // Enrichissements v2
      skillDetails: dto.competencesAvecDetails?.map(s => ({
        name: s.nom,
        category: s.categorie,
        status: s.statut
      })),
      recommendationsWithPriority: dto.recommandationsAvecPriorite?.map(r => ({
        text: r.texte,
        priority: r.priorite
      })),
      keywordWeights: dto.keywordsAvecPoids?.map(k => ({
        word: k.mot,
        weight: k.poids
      })),
      profileStrengthsList: dto.forcesProfil ?? [],
    };
  }

  ngOnDestroy(): void {
    this.pipeline.pipelineError.set(null);
  }
}
