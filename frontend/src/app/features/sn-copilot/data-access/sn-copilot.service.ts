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
    'Show my 10 most recent applications',
    'Which applications should I follow up on first?',
    'Analyze my conversion rates and metrics'
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
      'Show my 10 most recent applications',
      'Which applications should I follow up on first?',
      'Analyze my conversion rates and metrics'
    ]);
  }

  loadStarterSuggestions(): void {
    this.http.get<SnStarterSuggestionItem[]>(`${this.baseUrl}/sn/starter-suggestions`)
      .pipe(
        catchError(() => of([
          {
            title: 'My Profile',
            description: 'Full summary of your skills, education, and experience',
            prompt: 'Summarize my complete profile including skills and experience',
            category: 'Profile',
            icon: 'user'
          },
          {
            title: 'CV Matching',
            description: 'Evaluate compatibility between your profile and job openings',
            prompt: 'Match my profile against active job applications',
            category: 'Intelligence',
            icon: 'target'
          },
          {
            title: 'Portfolio Review',
            description: 'View your 10 most recent applications and latest statuses',
            prompt: 'Show my 10 most recent applications with status and dates',
            category: 'Portfolio',
            icon: 'layers'
          },
          {
            title: 'Follow-up Priorities',
            description: 'Identify stagnant applications that require outreach',
            prompt: 'Which unanswered applications should I follow up on first?',
            category: 'Strategy',
            icon: 'clock'
          },
          {
            title: 'Metrics & Velocity',
            description: 'Executive summary of response and conversion rates',
            prompt: 'Analyze my conversion rates and pipeline statistics',
            category: 'Analytics',
            icon: 'bar-chart-2'
          },
          {
            title: 'Quick Status Update',
            description: "Update an application status or record an interview",
            prompt: "Move my latest application to 'Interview' status",
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
            markdownText: `I am currently experiencing a temporary issue connecting to the AI engine. Please ensure the backend services are running and try again in a moment.`,
            actionsPerformed: ['⚠️ SN Copilot connection unavailable'],
            cards: [],
            followUpSuggestions: [
              'Show my 10 most recent applications',
              'Which applications should I follow up on first?'
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
