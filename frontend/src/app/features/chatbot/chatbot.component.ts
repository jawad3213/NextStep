import { Component, inject, signal, computed, ElementRef, ViewChild, AfterViewChecked } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { firstValueFrom } from 'rxjs';

interface ChatMessage {
  role: 'user' | 'assistant' | 'system';
  content: string;
  timestamp: Date;
}

interface Question {
  id: string;
  question: string;
  type: string;
  source: string;
  companySpecific: boolean;
  tip: string;
}

interface InterviewSession {
  sessionId: string;
  openingMessage: string;
}

interface InterviewFeedback {
  globalScore: number;
  dimensions: { name: string; score: number; comment: string }[];
  strengths: string[];
  improvements: string[];
  bestAnswer: string;
  worstAnswer: string;
  coachingTips: string[];
}

@Component({
  selector: 'app-chatbot',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="chat-shell">
      <header class="page-header">
        <div class="header-left">
          <h1 class="page-title">AI Interview Coach</h1>
          <p class="page-subtitle">Preparation aux entretiens, questions personnalisees et feedback STAR.</p>
        </div>
      </header>

      <div class="tabs-bar">
        <button class="tab" [class.active]="mode() === 'questions'" (click)="setMode('questions')">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><path d="M9.09 9a3 3 0 0 1 5.83 1c0 2-3 3-3 3"/><line x1="12" y1="17" x2="12.01" y2="17"/></svg>
          Questions
        </button>
        <button class="tab" [class.active]="mode() === 'interview'" (click)="setMode('interview')">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>
          Mock Interview
        </button>
        <button class="tab" [class.active]="mode() === 'chat'" (click)="setMode('chat')">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/></svg>
          Chat libre
        </button>
        <button class="tab" [class.active]="mode() === 'salary'" (click)="setMode('salary')">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="12" y1="1" x2="12" y2="23"/><path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"/></svg>
          Salaire
        </button>
      </div>

      <!-- QUESTIONS MODE -->
      @if (mode() === 'questions') {
        <div class="questions-panel">
          <div class="q-controls">
            <div class="q-select">
              <label>Domaine</label>
              <select [(ngModel)]="qDomain" class="form-select">
                <option value="general">General</option>
                <option value="technical">Technique</option>
                <option value="behavioral">Comportemental</option>
                <option value="motivation">Motivation</option>
              </select>
            </div>
            <div class="q-select">
              <label>Niveau</label>
              <select [(ngModel)]="qLevel" class="form-select">
                <option value="junior">Junior</option>
                <option value="mid">Intermediaire</option>
                <option value="senior">Senior</option>
              </select>
            </div>
            <button class="btn-generate" (click)="generateQuestions()" [disabled]="loadingQuestions()">
              @if (loadingQuestions()) { <span class="spinner-sm"></span> Generation... }
              @else { Generer les questions }
            </button>
          </div>

          @if (questions().length > 0) {
            <div class="questions-list">
              @for (q of questions(); track q.id) {
                <div class="q-card" (click)="q.expanded = !q.expanded">
                  <div class="q-header">
                    <div class="q-type-badge" [class]="q.type">{{ q.type }}</div>
                    <p>{{ q.question }}</p>
                  </div>
                  @if (q.expanded) {
                    <div class="q-details">
                      <p class="q-tip"><strong>Conseil:</strong> {{ q.tip }}</p>
                      @if (q.companySpecific) { <p class="q-company">Question specifique a l'entreprise</p> }
                    </div>
                  }
                </div>
              }
            </div>
          }
        </div>
      }

      <!-- INTERVIEW MODE -->
      @if (mode() === 'interview') {
        <div class="interview-panel">
          @if (!interviewSession()) {
            <div class="start-card">
              <h3>Demarrer un entretien simule</h3>
              <p>L'IA jouera le role du recruteur et vous posera des questions adaptees a votre profil et au poste.</p>
              <div class="q-select">
                <label>Domaine</label>
                <select [(ngModel)]="qDomain" class="form-select"><option value="general">General</option><option value="technical">Technique</option><option value="behavioral">Comportemental</option></select>
              </div>
              <button class="btn-generate" (click)="startInterview()" [disabled]="interviewLoading()">
                @if (interviewLoading()) { <span class="spinner-sm"></span> Preparation... }
                @else { Commencer l'entretien }
              </button>
            </div>
          } @else {
            <div class="chat-area" #chatArea>
              @for (msg of chatMessages(); track msg.timestamp) {
                <div class="msg" [class.user]="msg.role === 'user'" [class.assistant]="msg.role === 'assistant'">
                  <div class="msg-content">{{ msg.content }}</div>
                  <span class="msg-time">{{ msg.timestamp | date:'HH:mm' }}</span>
                </div>
              }
              @if (aiTyping()) {
                <div class="msg assistant typing">
                  <div class="typing-dots"><span></span><span></span><span></span></div>
                </div>
              }
            </div>
            <div class="chat-input-bar">
              <input type="text" [(ngModel)]="userInput" class="chat-input" placeholder="Votre reponse..." (keyup.enter)="sendMessage()" />
              <button class="btn-send" (click)="sendMessage()" [disabled]="!userInput() || aiTyping()">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="22" y1="2" x2="11" y2="13"/><polygon points="22 2 15 22 11 13 2 9 22 2"/></svg>
              </button>
              <button class="btn-end" (click)="endInterview()">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><line x1="15" y1="9" x2="9" y2="15"/><line x1="9" y1="9" x2="15" y2="15"/></svg>
              </button>
            </div>
          }

          @if (interviewFeedback(); as fb) {
            <div class="feedback-card">
              <div class="fb-header">
                <h3>Feedback</h3>
                <div class="fb-score" [class.high]="fb.globalScore >= 7" [class.mid]="fb.globalScore >= 4" [class.low]="fb.globalScore < 4">
                  {{ fb.globalScore }}/10
                </div>
              </div>
              <div class="fb-dimensions">
                @for (d of fb.dimensions; track d.name) {
                  <div class="fb-dim">
                    <div class="dim-header"><span>{{ d.name }}</span><span>{{ d.score }}/10</span></div>
                    <div class="dim-bar"><div class="dim-fill" [style.width.%]="d.score * 10" [class.high]="d.score >= 7" [class.mid]="d.score >= 4" [class.low]="d.score < 4"></div></div>
                    <p>{{ d.comment }}</p>
                  </div>
                }
              </div>
              <div class="fb-grid">
                @if (fb.strengths.length > 0) {
                  <div class="fb-section strengths"><h4>Points forts</h4><ul>@for (s of fb.strengths; track s) { <li>{{ s }}</li> }</ul></div>
                }
                @if (fb.improvements.length > 0) {
                  <div class="fb-section improvements"><h4>A ameliorer</h4><ul>@for (i of fb.improvements; track i) { <li>{{ i }}</li> }</ul></div>
                }
              </div>
            </div>
          }
        </div>
      }

      <!-- FREE CHAT MODE -->
      @if (mode() === 'chat') {
        <div class="chat-panel">
          <div class="chat-area" #chatArea>
            @for (msg of chatMessages(); track msg.timestamp) {
              <div class="msg" [class.user]="msg.role === 'user'" [class.assistant]="msg.role === 'assistant'">
                <div class="msg-content">{{ msg.content }}</div>
                <span class="msg-time">{{ msg.timestamp | date:'HH:mm' }}</span>
              </div>
            }
            @if (aiTyping()) {
              <div class="msg assistant typing"><div class="typing-dots"><span></span><span></span><span></span></div></div>
            }
          </div>
          <div class="chat-input-bar">
            <input type="text" [(ngModel)]="userInput" class="chat-input" placeholder="Posez votre question sur le recrutement, les entretiens..." (keyup.enter)="freeChat()" />
            <button class="btn-send" (click)="freeChat()" [disabled]="!userInput() || aiTyping()">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="22" y1="2" x2="11" y2="13"/><polygon points="22 2 15 22 11 13 2 9 22 2"/></svg>
            </button>
          </div>
        </div>
      }

      <!-- SALARY MODE -->
      @if (mode() === 'salary') {
        <div class="salary-panel">
          <div class="q-controls">
            <div class="q-select">
              <label>Intitule du poste</label>
              <input type="text" [(ngModel)]="salaryJob" class="form-input" placeholder="Developpeur Full Stack" />
            </div>
            <div class="q-select">
              <label>Localisation</label>
              <input type="text" [(ngModel)]="salaryLocation" class="form-input" placeholder="Casablanca" />
            </div>
            <button class="btn-generate" (click)="getSalary()" [disabled]="salaryLoading()">
              @if (salaryLoading()) { <span class="spinner-sm"></span> Analyse... }
              @else { Estimer le salaire }
            </button>
          </div>

          @if (salaryResult(); as sr) {
            <div class="salary-result">
              <div class="salary-range-card">
                <h3>{{ sr.jobTitle }} a {{ sr.location }}</h3>
                <div class="range-visual">
                  <div class="range-track">
                    <div class="range-fill" [style.left.%]="((sr.minSalary - sr.minSalary) / (sr.maxSalary - sr.minSalary || 1)) * 0" [style.width.%]="100"></div>
                  </div>
                  <div class="range-labels">
                    <span>{{ sr.minSalary }} {{ sr.currency }}</span>
                    <span class="target">Votre cible: {{ sr.yourTarget }} {{ sr.currency }}</span>
                    <span>{{ sr.maxSalary }} {{ sr.currency }}</span>
                  </div>
                </div>
                <div class="confidence">Confiance: {{ sr.confidenceLevel }}</div>
              </div>

              @if (sr.negotiationScript.length > 0) {
                <div class="card">
                  <h3>Script de negociation</h3>
                  @for (step of sr.negotiationScript; track step.step) {
                    <div class="negotiation-step">
                      <div class="step-num">{{ step.step }}</div>
                      <div class="step-content"><strong>{{ step.action }}</strong><p>{{ step.phrase }}</p><span class="step-why">{{ step.why }}</span></div>
                    </div>
                  }
                </div>
              }
            </div>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    :host { display: flex; flex-direction: column; flex: 1; height: 100%; min-height: 0; overflow: hidden; }
    .chat-shell { display: flex; flex-direction: column; height: 100%; padding: 24px 40px; gap: 16px; overflow: hidden; }
    .page-header { display: flex; justify-content: space-between; align-items: flex-start; flex-shrink: 0; }
    .header-left { display: flex; flex-direction: column; gap: 4px; }
    .page-title { font-size: 24px; font-weight: 700; color: #212121; margin: 0; font-family: 'Lato', sans-serif; }
    .page-subtitle { font-size: 14px; color: #616161; margin: 0; }

    .tabs-bar { display: flex; gap: 4px; background: #F5F5F5; border-radius: 8px; padding: 4px; width: fit-content; flex-shrink: 0; }
    .tab { display: flex; align-items: center; gap: 6px; padding: 8px 16px; border: none; background: transparent; border-radius: 6px; font-size: 13px; font-weight: 600; color: #616161; cursor: pointer; transition: all 0.2s; svg { width: 16px; height: 16px; } &.active { background: white; color: #0C1986; box-shadow: 0 1px 3px rgba(0,0,0,0.1); } }

    .questions-panel, .chat-panel, .interview-panel, .salary-panel { flex: 1; display: flex; flex-direction: column; gap: 16px; min-height: 0; }
    .q-controls { display: flex; gap: 12px; align-items: flex-end; padding: 16px; background: white; border: 1px solid #E0E0E0; border-radius: 12px; }
    .q-select { display: flex; flex-direction: column; gap: 4px; flex: 1; label { font-size: 12px; font-weight: 700; color: #616161; } }
    .form-select, .form-input { height: 40px; padding: 0 12px; border: 1px solid #E0E0E0; border-radius: 4px; background: #F5F5F5; font-size: 13px; &:focus { outline: none; border-color: #1A91F0; background: white; } }

    .btn-generate, .btn-start { display: flex; align-items: center; gap: 8px; padding: 10px 20px; background: #0C1986; color: white; border: none; border-radius: 8px; font-size: 13px; font-weight: 600; cursor: pointer; white-space: nowrap; &:hover { background: #091361; } &:disabled { background: #BDBDBD; } }
    .spinner-sm { width: 16px; height: 16px; border: 2px solid rgba(255,255,255,0.3); border-top-color: white; border-radius: 50%; animation: spin 0.8s linear infinite; display: inline-block; }
    @keyframes spin { 100% { transform: rotate(360deg); } }

    .questions-list { display: flex; flex-direction: column; gap: 8px; overflow-y: auto; }
    .q-card { background: white; border: 1px solid #E0E0E0; border-radius: 10px; padding: 16px; cursor: pointer; transition: all 0.2s; &:hover { border-color: #1A91F0; } }
    .q-header { display: flex; gap: 12px; align-items: flex-start; }
    .q-type-badge { padding: 2px 8px; border-radius: 4px; font-size: 10px; font-weight: 700; text-transform: uppercase; flex-shrink: 0; &.technical { background: #E8F4FD; color: #1A91F0; } &.behavioral { background: #FFF8E6; color: #F59B00; } &.general { background: #F1F5F9; color: #475569; } &.motivation { background: #E6F4EA; color: #34A853; } }
    .q-header p { margin: 0; font-size: 13px; color: #212121; line-height: 1.5; }
    .q-details { margin-top: 12px; padding: 12px; background: #FAFAFA; border-radius: 8px; }
    .q-tip { margin: 0; font-size: 12px; color: #616161; line-height: 1.5; }
    .q-company { margin: 8px 0 0; font-size: 11px; color: #1A91F0; font-weight: 600; }

    .start-card { background: white; border: 1px solid #E0E0E0; border-radius: 12px; padding: 32px; text-align: center; display: flex; flex-direction: column; align-items: center; gap: 16px; h3 { margin: 0; font-size: 18px; color: #212121; } p { margin: 0; font-size: 14px; color: #616161; max-width: 500px; } .q-select { width: 300px; } }

    .chat-area { flex: 1; display: flex; flex-direction: column; gap: 12px; padding: 16px; background: #FAFAFA; border: 1px solid #E0E0E0; border-radius: 12px; overflow-y: auto; }
    .msg { display: flex; flex-direction: column; max-width: 80%; &.user { align-self: flex-end; .msg-content { background: #0C1986; color: white; border-radius: 16px 16px 4px 16px; } } &.assistant { align-self: flex-start; .msg-content { background: white; border: 1px solid #E0E0E0; border-radius: 16px 16px 16px 4px; } } }
    .msg-content { padding: 12px 16px; font-size: 13px; line-height: 1.5; }
    .msg-time { font-size: 10px; color: #9E9E9E; margin-top: 4px; padding: 0 4px; }
    .typing-dots { display: flex; gap: 4px; padding: 12px 16px; span { width: 8px; height: 8px; background: #94A3B8; border-radius: 50%; animation: bounce 1.4s infinite; &:nth-child(2) { animation-delay: 0.2s; } &:nth-child(3) { animation-delay: 0.4s; } } }
    @keyframes bounce { 0%, 80%, 100% { transform: scale(0); } 40% { transform: scale(1); } }

    .chat-input-bar { display: flex; gap: 8px; padding: 12px; background: white; border: 1px solid #E0E0E0; border-radius: 12px; }
    .chat-input { flex: 1; height: 44px; padding: 0 16px; border: 1px solid #E0E0E0; border-radius: 8px; background: #F5F5F5; font-size: 14px; &:focus { outline: none; border-color: #1A91F0; background: white; } }
    .btn-send, .btn-end { width: 44px; height: 44px; border: none; border-radius: 8px; display: flex; align-items: center; justify-content: center; cursor: pointer; svg { width: 18px; height: 18px; } }
    .btn-send { background: #0C1986; color: white; &:disabled { background: #BDBDBD; } }
    .btn-end { background: #FCE8E6; color: #D93025; &:hover { background: #F5C6C6; } }

    .feedback-card { background: white; border: 1px solid #E0E0E0; border-radius: 12px; padding: 20px; }
    .fb-header { display: flex; justify-content: space-between; align-items: center; h3 { margin: 0; font-size: 16px; } }
    .fb-score { padding: 8px 16px; border-radius: 100px; font-size: 20px; font-weight: 800; &.high { background: #E6F4EA; color: #34A853; } &.mid { background: #FFF8E6; color: #F59B00; } &.low { background: #FCE8E6; color: #D93025; } }
    .fb-dimensions { margin: 16px 0; display: flex; flex-direction: column; gap: 12px; }
    .fb-dim { .dim-header { display: flex; justify-content: space-between; font-size: 13px; font-weight: 600; margin-bottom: 4px; } .dim-bar { height: 6px; background: #E0E0E0; border-radius: 100px; overflow: hidden; .dim-fill { height: 100%; border-radius: 100px; &.high { background: #34A853; } &.mid { background: #F59B00; } &.low { background: #D93025; } } } p { font-size: 12px; color: #616161; margin: 4px 0 0; } }
    .fb-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 16px; }
    .fb-section { h4 { margin: 0 0 8px; font-size: 13px; } ul { margin: 0; padding-left: 16px; li { font-size: 12px; margin-bottom: 4px; } } &.strengths ul li { color: #34A853; } &.improvements ul li { color: #D93025; } }

    .salary-result { display: flex; flex-direction: column; gap: 16px; }
    .salary-range-card { background: white; border: 1px solid #E0E0E0; border-radius: 12px; padding: 24px; h3 { margin: 0 0 20px; font-size: 16px; } }
    .range-visual { margin-bottom: 12px; }
    .range-track { height: 8px; background: #E0E0E0; border-radius: 100px; position: relative; }
    .range-fill { position: absolute; top: 0; height: 100%; background: linear-gradient(90deg, #1A91F0, #0C1986); border-radius: 100px; }
    .range-labels { display: flex; justify-content: space-between; margin-top: 8px; font-size: 13px; font-weight: 600; color: #616161; .target { color: #0C1986; font-weight: 800; } }
    .confidence { font-size: 12px; color: #616161; }

    .card { background: white; border: 1px solid #E0E0E0; border-radius: 12px; padding: 20px; h3 { margin: 0 0 16px; font-size: 15px; } }
    .negotiation-step { display: flex; gap: 12px; margin-bottom: 12px; }
    .step-num { width: 28px; height: 28px; background: #0C1986; color: white; border-radius: 50%; display: flex; align-items: center; justify-content: center; font-size: 13px; font-weight: 700; flex-shrink: 0; }
    .step-content { flex: 1; strong { display: block; font-size: 13px; color: #212121; } p { font-size: 12px; color: #424242; line-height: 1.5; margin: 4px 0; } .step-why { font-size: 11px; color: #1A91F0; } }
  `]
})
export class ChatbotComponent {
  private http = inject(HttpClient);
  private baseUrl = environment.apiBaseUrl;

  mode = signal<'questions' | 'interview' | 'chat' | 'salary'>('questions');
  userInput = signal('');
  chatMessages = signal<ChatMessage[]>([
    { role: 'assistant', content: 'Bonjour ! Je suis votre coach IA pour la preparation aux entretiens. Posez-moi toutes vos questions !', timestamp: new Date() }
  ]);
  questions = signal<(Question & { expanded?: boolean })[]>([]);
  interviewSession = signal<InterviewSession | null>(null);
  interviewFeedback = signal<InterviewFeedback | null>(null);
  aiTyping = signal(false);
  loadingQuestions = signal(false);
  interviewLoading = signal(false);
  salaryLoading = signal(false);

  qDomain = signal('general');
  qLevel = signal('junior');
  salaryJob = signal('Developpeur Full Stack');
  salaryLocation = signal('Casablanca');
  salaryResult = signal<any>(null);

  setMode(m: 'questions' | 'interview' | 'chat' | 'salary') {
    this.mode.set(m);
    if (m === 'chat' && this.chatMessages().length === 0) {
      this.chatMessages.set([{ role: 'assistant', content: 'Bonjour ! Je suis votre coach IA pour la preparation aux entretiens. Posez-moi toutes vos questions !', timestamp: new Date() }]);
    }
  }

  async generateQuestions() {
    this.loadingQuestions.set(true);
    try {
      const res = await firstValueFrom(this.http.post<{ questions: Question[] }>(`${this.baseUrl.replace('/api', '')}/api/chatbot/questions`, {
        mode: this.qDomain(),
        arena_config: { level: this.qLevel() }
      }));
      this.questions.set(res.questions.map(q => ({ ...q, expanded: false })));
    } catch {
      this.questions.set(mockQuestions.map(q => ({ ...q, expanded: false })));
    } finally {
      this.loadingQuestions.set(false);
    }
  }

  async startInterview() {
    this.interviewLoading.set(true);
    try {
      const res = await firstValueFrom(this.http.post<InterviewSession>(`${this.baseUrl.replace('/api', '')}/api/chatbot/interview/start`, {
        mode: this.qDomain(),
        arena_config: { level: this.qLevel() }
      }));
      this.interviewSession.set(res);
      this.chatMessages.set([{ role: 'assistant', content: res.openingMessage, timestamp: new Date() }]);
    } catch {
      this.interviewSession.set({ sessionId: crypto.randomUUID(), openingMessage: 'Bonjour ! Je suis le recruteur. Pouvez-vous vous presenter et me parler de votre parcours ?' });
      this.chatMessages.set([{ role: 'assistant', content: 'Bonjour ! Je suis le recruteur. Pouvez-vous vous presenter et me parler de votre parcours ?', timestamp: new Date() }]);
    } finally {
      this.interviewLoading.set(false);
    }
  }

  async sendMessage() {
    const input = this.userInput();
    if (!input || this.aiTyping()) return;
    this.chatMessages.update(m => [...m, { role: 'user', content: input, timestamp: new Date() }]);
    this.userInput.set('');
    this.aiTyping.set(true);

    try {
      const res = await firstValueFrom(this.http.post<{ response: string }>(`${this.baseUrl.replace('/api', '')}/api/chatbot/interview/message`, {
        session_id: this.interviewSession()?.sessionId,
        user_input: input,
        mode: this.qDomain()
      }));
      this.chatMessages.update(m => [...m, { role: 'assistant', content: res.response, timestamp: new Date() }]);
    } catch {
      setTimeout(() => {
        this.chatMessages.update(m => [...m, { role: 'assistant', content: 'Merci pour votre reponse ! Passons a la question suivante : Parlez-moi d\'un projet dont vous etes particulierement fier.', timestamp: new Date() }]);
      }, 1000);
    } finally {
      this.aiTyping.set(false);
    }
  }

  async endInterview() {
    this.aiTyping.set(true);
    try {
      const res = await firstValueFrom(this.http.post<{ feedback: InterviewFeedback }>(`${this.baseUrl.replace('/api', '')}/api/chatbot/interview/end`, {
        session_id: this.interviewSession()?.sessionId
      }));
      this.interviewFeedback.set(res.feedback);
    } catch {
      this.interviewFeedback.set(mockFeedback);
    } finally {
      this.aiTyping.set(false);
    }
  }

  async freeChat() {
    const input = this.userInput();
    if (!input || this.aiTyping()) return;
    this.chatMessages.update(m => [...m, { role: 'user', content: input, timestamp: new Date() }]);
    this.userInput.set('');
    this.aiTyping.set(true);

    try {
      const res = await firstValueFrom(this.http.post<{ response: string }>(`${this.baseUrl.replace('/api', '')}/api/chatbot/free-chat`, {
        user_input: input,
        thread_id: crypto.randomUUID()
      }));
      this.chatMessages.update(m => [...m, { role: 'assistant', content: res.response, timestamp: new Date() }]);
    } catch {
      setTimeout(() => {
        this.chatMessages.update(m => [...m, { role: 'assistant', content: 'C\'est une excellente question ! Pour reussir un entretien technique, je vous recommande de :\n\n1. Maitriser les fondamentaux du langage\n2. Preparer des exemples concrets de projets\n3. Pratiquer les algorithmes classiques\n4. Preparer des questions pertinentes pour le recruteur\n\nSouhaitez-vous que je detaille un de ces points ?', timestamp: new Date() }]);
      }, 1200);
    } finally {
      this.aiTyping.set(false);
    }
  }

  async getSalary() {
    this.salaryLoading.set(true);
    try {
      const res = await firstValueFrom(this.http.post<any>(`${this.baseUrl.replace('/api', '')}/api/chatbot/salary`, {
        mode: 'market',
        arena_config: { job_title: this.salaryJob(), location: this.salaryLocation() }
      }));
      this.salaryResult.set(res);
    } catch {
      this.salaryResult.set(mockSalary);
    } finally {
      this.salaryLoading.set(false);
    }
  }
}

const mockQuestions: Question[] = [
  { id: '1', question: 'Parlez-moi de vous et de votre parcours.', type: 'general', source: 'common', companySpecific: false, tip: 'Structurez votre reponse en 3 parties : present, passé, futur. 2 minutes max.' },
  { id: '2', question: 'Quelle est votre plus grande force professionnelle ?', type: 'behavioral', source: 'common', companySpecific: false, tip: 'Choisissez une force pertinente pour le poste et illustrez-la avec un exemple concret.' },
  { id: '3', question: 'Decrivez une situation difficile que vous avez resolue en equipe.', type: 'behavioral', source: 'common', companySpecific: false, tip: 'Utilisez la methode STAR : Situation, Tache, Action, Resultat.' },
  { id: '4', question: 'Quels sont vos objectifs de carriere a 5 ans ?', type: 'motivation', source: 'common', companySpecific: false, tip: 'Montrez votre ambition tout en restant realiste et aligne avec le poste.' },
  { id: '5', question: 'Expliquez la difference entre let et var en JavaScript.', type: 'technical', source: 'common', companySpecific: false, tip: 'Mentionnez le scope de bloc, le hoisting et la temporal dead zone.' },
];

const mockFeedback: InterviewFeedback = {
  globalScore: 7,
  dimensions: [
    { name: 'Clarte', score: 8, comment: 'Reponses bien structures et faciles a suivre.' },
    { name: 'Pertinence', score: 7, comment: 'Bon usage de la methode STAR, mais quelques details superflus.' },
    { name: 'Confiance', score: 6, comment: 'Bon contact mais quelques hesitations en debut de reponse.' },
    { name: 'Technique', score: 7, comment: 'Bonnes connaissances fondamentales, a approfondir sur les sujets avances.' },
  ],
  strengths: ['Bonne capacite de synthese', 'Exemples concrets bien choisis', 'Attitude positive et professionnelle'],
  improvements: ['Reduire les hesitations en debut de reponse', 'Approfondir les reponses techniques', 'Preparer des questions plus pertinentes pour le recruteur'],
  bestAnswer: 'Situation difficile resolue en equipe - bien structure avec la methode STAR',
  worstAnswer: 'Question sur les objectifs de carriere - un peu vague',
  coachingTips: ['Preparez 3 histoires STAR', 'Pratiquez vos reponses a voix haute', 'Recherchez l\'entreprise en amont'],
};

const mockSalary = {
  jobTitle: 'Developpeur Full Stack',
  location: 'Casablanca',
  minSalary: 12000,
  maxSalary: 22000,
  avgSalary: 16000,
  currency: 'MAD',
  yourTarget: 18000,
  confidenceLevel: 'Eleve',
  marketSources: ['Glassdoor', 'LinkedIn Salary', 'ReKrute'],
  negotiationScript: [
    { step: 1, action: 'Recherche preliminaire', phrase: 'D\'apres mes recherches, le salaire median pour ce poste a Casablanca est de 16 000 MAD.', why: 'Montre que vous etes informe et professionnel.' },
    { step: 2, action: 'Ancrage positif', phrase: 'Je suis tres enthousiaste a propos de cette opportunite et je pense que mes competences en Angular et Node.js apporteront une valeur immediate.', why: 'Etablit une base positive avant la negociation.' },
    { step: 3, action: 'Proposition', phrase: 'Compte tenu de mon experience et des standards du marche, je vise une remuneration autour de 18 000 MAD.', why: 'Proposition concrete et justifiee.' },
  ],
};
