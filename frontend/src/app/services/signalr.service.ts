import { Injectable, inject, signal, effect } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import Keycloak from 'keycloak-js';
import { environment } from '../../environments/environment';
import { PipelineStateService } from './pipeline-state.service';

export interface PipelineProgress {
  offerId: string;
  step: string;
  status: string;
  progressPercent: number;
  message?: string;
  agentName?: string;
}

export interface PipelineCompleted {
  offerId: string;
  status: 'completed' | 'error';
  result?: any;
  error?: string;
}

export interface GenerationProgress {
  offerId: string;
  progressPercent: number;
  message: string;
  status: 'running' | 'completed' | 'error';
}

export interface GenerationCompleted {
  offerId: string;
  downloadUrl: string;
  status: string;
}

@Injectable({ providedIn: 'root' })
export class SignalRService {
  private hubConnection?: signalR.HubConnection;
  private pipeline = inject(PipelineStateService);
  private keycloak = inject(Keycloak);

  readonly isConnected = signal(false);
  private connectionPromise: Promise<void> | null = null;
  private currentOfferId: string | null = null;

  constructor() {
    effect(() => {
      if (!this.pipeline.isFlowOpen() && this.isConnected()) {
        this.leaveCurrentGroup();
        this.disconnect();
      }
    });
  }

  async connect(): Promise<void> {
    if (this.hubConnection?.state === signalR.HubConnectionState.Connected) return;
    if (this.connectionPromise) return this.connectionPromise;

    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(`${environment.apiBaseUrl.replace('/api', '')}/hubs/pipeline`, {
        withCredentials: true,
        accessTokenFactory: () => this.keycloak.token ?? ''
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .build();

    this.hubConnection.onreconnecting(() => this.isConnected.set(false));
    this.hubConnection.onreconnected(() => this.isConnected.set(true));
    this.hubConnection.onclose(() => {
      this.isConnected.set(false);
      this.connectionPromise = null;
    });

    this.hubConnection.on('PipelineProgress', (data: PipelineProgress) => {
      this.handleProgress(data);
    });

    this.hubConnection.on('PipelineCompleted', (data: PipelineCompleted) => {
      this.handleCompleted(data);
    });

    this.hubConnection.on('PipelineError', (data: PipelineCompleted) => {
      this.handleError(data);
    });

    this.hubConnection.on('GenerationProgress', (data: GenerationProgress) => {
      this.handleGenerationProgress(data);
    });

    this.hubConnection.on('GenerationCompleted', (data: GenerationCompleted) => {
      this.handleGenerationCompleted(data);
    });

    this.hubConnection.on('GenerationError', (data: any) => {
      this.pipeline.generationProgress.set(null);
      this.pipeline.setLoading(false);
      this.pipeline.pipelineError.set(data.error ?? 'Erreur génération PDF');
    });

    this.connectionPromise = this.hubConnection.start()
      .then(() => this.isConnected.set(true))
      .catch(err => {
        console.error('SignalR connection failed:', err);
        this.connectionPromise = null;
      });

    return this.connectionPromise;
  }

  disconnect(): void {
    if (this.hubConnection) {
      this.hubConnection.stop();
      this.isConnected.set(false);
      this.connectionPromise = null;
    }
  }

  async joinOfferGroup(offerId: string): Promise<void> {
    this.currentOfferId = offerId;
    await this.ensureConnected();
    if (this.hubConnection?.state === signalR.HubConnectionState.Connected) {
      try {
        await this.hubConnection.invoke('JoinOfferGroup', offerId);
      } catch (err) {
        console.error('Failed to join offer group:', err);
      }
    }
  }

  async leaveOfferGroup(offerId: string): Promise<void> {
    try {
      await this.hubConnection?.invoke('LeaveOfferGroup', offerId);
    } catch { }
    if (this.currentOfferId === offerId) {
      this.currentOfferId = null;
    }
  }

  async leaveCurrentGroup(): Promise<void> {
    if (this.currentOfferId) {
      await this.leaveOfferGroup(this.currentOfferId);
    }
  }

  private async ensureConnected(): Promise<void> {
    if (this.hubConnection?.state !== signalR.HubConnectionState.Connected) {
      await this.connect();
    }
  }

  private handleProgress(data: PipelineProgress): void {
    this.pipeline.setLoading(true, data.message ?? 'Pipeline en cours...');
    this.pipeline.currentAgentProgress.set({
      step: data.step,
      agentName: data.agentName ?? '',
      label: data.message ?? data.step,
      status: data.status === 'running' ? 'running' : 'done',
      progressPercent: data.progressPercent,
    });
  }

  private handleCompleted(data: PipelineCompleted): void {
    this.pipeline.setLoading(false);

    if (data.status === 'error' || data.error) {
      this.pipeline.pipelineError.set(data.error ?? 'Erreur inconnue');
      return;
    }

    if (data.result) {
      const r: any = data.result;
      console.log('[SignalR] PipelineCompleted result:', JSON.stringify(r, null, 2));
      this.pipeline.setResult({
        offerTitle: r.titre ?? r.offerTitle ?? '',
        companyName: r.entreprise ?? r.companyName ?? '',
        contractType: r.typeContrat ?? r.contractType ?? '',
        location: r.localisation ?? r.location ?? undefined,
        requiredSkills: r.competencesRequises ?? r.requiredSkills ?? [],
        preferredSkills: r.competencesSouhaitees ?? r.preferredSkills ?? [],
        experienceYears: r.anneesExperience ?? r.experienceYears ?? undefined,
        educationLevel: r.niveauEtudes ?? r.educationLevel ?? undefined,
        modeTravail: r.modeTravail ?? undefined,
        descriptionPoste: r.descriptionPoste ?? r.description ?? '',
        originalRawText: r.texteBrut ?? r.originalRawText ?? '',
        matchScore: r.scoreMatching ?? r.matchScore ?? 0,
        matchBreakdown: r.matchBreakdown ?? { skills: 0, experience: 0, location: 0 },
        keywordsPresent: r.keywordsPresents ?? r.keywordsPresent ?? [],
        keywordsMissing: r.keywordsManquants ?? r.keywordsMissing ?? [],
        matchingSkills: r.competencesMatching ?? r.matchingSkills ?? [],
        recommendations: r.recommandations ?? r.recommendations ?? [],
        companyCultureScore: r.companyCultureScore ?? 0,
        companySalaryMin: r.companySalaryMin ?? 0,
        companySalaryMax: r.companySalaryMax ?? 0,
        companySize: r.companySize ?? '',
        companyNews: (r.companyNews ?? []).map((n: any) => typeof n === 'string' ? { title: n, date: '' } : n),
        missingSkills: r.competencesManquantes ?? r.missingSkills ?? [],
        profileStrengths: r.recommandations ?? r.profileStrengths ?? [],
        skillGaps: (r.skillGaps ?? []).map((g: any) => ({
          skill: g.skillName || g.skill, priority: g.priority, weeks: g.estimatedWeeks || g.weeks
        })),
        cvPdfPath: r.cvPdfPath ?? '',
        atsScore: r.scoreAts ?? r.atsScore ?? 0,
        atsImprovements: r.atsImprovements ?? r.cv_improvements ?? [],
        profileData: r.profileData ?? r.profile_data ?? undefined,
        cvGeneratedContent: r.cvGeneratedContent ?? r.cv_data ?? r.cvData ?? r.cv_optimized_content ?? r.cvOptimizedContent ?? undefined,
        emailSubject: r.emailSubject ?? '',
        emailBody: r.emailBody ?? '',
        recruiterName: r.recruiterName ?? '',
        coverLetterContent: r.coverLetterContent ?? '',
      });

      this.pipeline.markStepDone(1);
      this.pipeline.currentAgentProgress.set({
        step: 'pipeline_completed',
        agentName: 'db_persist',
        label: 'Pipeline termine',
        status: 'done',
        progressPercent: 100,
      });

      const gapsCount = r.competencesManquantes?.length ?? r.missingSkills?.length ?? 0;
      if (gapsCount > 0) {
        this.pipeline.showSidebarBadge('skill-gap', `${gapsCount} gaps`, 'red');
      }
      this.pipeline.showSidebarBadge('company-intel', 'Prêt', 'green');
    }
  }

  private handleError(data: PipelineCompleted): void {
    this.pipeline.setLoading(false);
    this.pipeline.pipelineError.set(data.error ?? 'Pipeline indisponible');
  }

  private handleGenerationProgress(data: GenerationProgress): void {
    this.pipeline.generationProgress.set(data);
    this.pipeline.setLoading(true, data.message);
  }

  private handleGenerationCompleted(data: GenerationCompleted): void {
    this.pipeline.generationProgress.set(null);
    this.pipeline.cvDownloadUrl.set(data.downloadUrl);
    this.pipeline.setLoading(false);
    this.pipeline.showSidebarBadge('cv-builder', 'Prêt', 'green');
    this.pipeline.showSidebarBadge('email', 'Nouveau', 'green');
  }
}
