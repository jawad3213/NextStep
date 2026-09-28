import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { catchError, map, tap } from 'rxjs/operators';
import { API_BASE_URL } from '@core/http/api-url';

export interface SnChatMessage {
  role: 'user' | 'assistant';
  content: string;
}

export interface SnCardDto {
  id?: string;
  id_offre?: string;
  entreprise: string;
  role: string;
  statut: string;
  channel: string;
  channel_url?: string;
  application_date?: string;
  has_response: boolean;
  response_status?: string;
  notes?: string;
  response_summary?: string;
  recommended_action?: string;
  follow_up_needed?: boolean;
}

export interface SnChatAgentResponse {
  markdown_text?: string;
  markdownText?: string;
  actions_performed?: string[];
  actionsPerformed?: string[];
  cards?: SnCardDto[];
  follow_up_suggestions?: string[];
  followUpSuggestions?: string[];
}

export interface SnStarterSuggestionItem {
  title: string;
  description: string;
  prompt: string;
  category: string;
  icon: string;
}

export interface SnUiMessage {
  id: string;
  sender: 'user' | 'sn';
  text: string;
  timestamp: Date;
  actions?: string[];
  cards?: SnCardDto[];
  suggestions?: string[];
  status?: 'sending' | 'delivered' | 'error';
}

@Injectable({
  providedIn: 'root'
})
export class SnCopilotService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = API_BASE_URL;

  // ── Reactive Drawer & Chat State ──────────────────────────────────────────
  readonly isOpen = signal<boolean>(false);
  readonly isExpanded = signal<boolean>(false);
  readonly loading = signal<boolean>(false);
  readonly messages = signal<SnUiMessage[]>([]);
  readonly starterSuggestions = signal<SnStarterSuggestionItem[]>([]);
  readonly activeFollowUps = signal<string[]>([
    'Affiche mes 10 dernières candidatures',
    'Quelles candidatures relancer en priorité ?',
    'Analyse mes taux de conversion et métriques'
  ]);

  constructor() {
    this.loadStarterSuggestions();
  }

  toggleDrawer(): void {
    this.isOpen.update((v) => !v);
  }

  openDrawer(initialPrompt?: string): void {
    this.isOpen.set(true);
    if (initialPrompt) {
      this.sendMessage(initialPrompt);
    }
  }

  closeDrawer(): void {
    this.isOpen.set(false);
  }

  toggleExpand(): void {
    this.isExpanded.update((v) => !v);
  }

  clearHistory(): void {
    this.messages.set([]);
    this.activeFollowUps.set([
      'Affiche mes 10 dernières candidatures',
      'Quelles candidatures relancer en priorité ?',
      'Analyse mes taux de conversion et métriques'
    ]);
  }

  loadStarterSuggestions(): void {
    this.http.get<SnStarterSuggestionItem[]>(`${this.baseUrl}/sn/starter-suggestions`)
      .pipe(
        catchError(() => of([
          {
            title: 'Mon Profil',
            description: 'Résumé complet de vos compétences, formations et expériences',
            prompt: 'Résume mon profil complet avec mes compétences et expériences',
            category: 'Profil',
            icon: 'user'
          },
          {
            title: 'CV Matching',
            description: 'Analyse la compatibilité de votre profil avec vos candidatures',
            prompt: 'Match mon profil avec mes candidatures actives',
            category: 'Intelligence',
            icon: 'target'
          },
          {
            title: 'Portfolio Review',
            description: 'Visualiser vos 10 dernières candidatures et statuts récents',
            prompt: 'Affiche mes 10 dernières candidatures avec leurs statuts et dates',
            category: 'Portfolio',
            icon: 'layers'
          },
          {
            title: 'Priorités de Relance',
            description: 'Identifier les candidatures stagnantes nécessitant un suivi',
            prompt: 'Quelles sont les candidatures sans réponse à relancer en priorité ?',
            category: 'Stratégie',
            icon: 'clock'
          },
          {
            title: 'Métriques & Vélocité',
            description: 'Synthèse exécutive du taux de réponse et conversion',
            prompt: 'Analyse mes taux de conversion et les statistiques de mon pipeline',
            category: 'Analytics',
            icon: 'bar-chart-2'
          },
          {
            title: 'Mutation Rapide',
            description: "Modifier un statut de candidature ou enregistrer un entretien",
            prompt: "Passe ma dernière candidature en statut 'Entretien'",
            category: 'Action',
            icon: 'check-circle'
          }
        ]))
      )
      .subscribe((suggestions) => {
        this.starterSuggestions.set(suggestions);
      });
  }

  sendMessage(prompt: string): void {
    const text = prompt.trim();
    if (!text || this.loading()) return;

    const userMessage: SnUiMessage = {
      id: `user-${Date.now()}`,
      sender: 'user',
      text,
      timestamp: new Date(),
      status: 'delivered'
    };

    // Append user message immediately
    this.messages.update((list) => [...list, userMessage]);
    this.loading.set(true);

    // Build history for context
    const historyPayload: SnChatMessage[] = this.messages()
      .slice(-6)
      .map((m) => ({
        role: m.sender === 'user' ? 'user' : 'assistant',
        content: m.text
      }));

    const requestPayload = {
      message: text,
      history: historyPayload
    };

    // Primary: .NET backend endpoint
    this.http.post<SnChatAgentResponse>(`${this.baseUrl}/sn/chat`, requestPayload)
      .pipe(
        // No direct call to the agents: they only accept requests from the backend.
        catchError((err) => {
          console.error('[SN Service] Backend request failed:', err);
          return of({
            markdownText: `Désolé, je rencontre une indisponibilité momentanée pour contacter le moteur d'intelligence artificielle. Veuillez vous assurer que le service backend est actif et réessayer dans un instant.`,
            actionsPerformed: ['⚠️ Connexion au service SN Copilot indisponible'],
            cards: [],
            followUpSuggestions: [
              'Affiche mes 10 dernières candidatures',
              'Quelles sont mes relances prioritaires ?'
            ]
          } as SnChatAgentResponse);
        })
      )
      .subscribe({
        next: (response) => {
          const markdown = response.markdownText || response.markdown_text || '';
          const actions = response.actionsPerformed || response.actions_performed || [];
          const cards = response.cards || [];
          const suggestions = response.followUpSuggestions || response.follow_up_suggestions || [];

          const snMessage: SnUiMessage = {
            id: `sn-${Date.now()}`,
            sender: 'sn',
            text: markdown,
            timestamp: new Date(),
            actions,
            cards,
            suggestions,
            status: 'delivered'
          };

          this.messages.update((list) => [...list, snMessage]);
          if (suggestions.length > 0) {
            this.activeFollowUps.set(suggestions);
          }
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
        }
      });
  }
}
