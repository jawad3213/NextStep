import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../../environments/environment';
import { ArenaConfig, QuestionItem, ChatMessage, FeedbackResult, SalaryResult, SessionSummary } from '../models/arena.models';

const mapConfig = (c: ArenaConfig) => ({
  domain: c.domain,
  level: c.level,
  durationMinutes: c.duration_minutes,
  language: c.language,
  focusAreas: c.focus_areas,
});

@Injectable({ providedIn: 'root' })
export class ArenaService {
  private readonly api = `${environment.apiBaseUrl}/arena`;

  constructor(private http: HttpClient) { }

  getQuestions(config: ArenaConfig): Observable<{ status: string; total: number; session_id: string; questions: QuestionItem[] }> {
    return this.http.post<any>(`${this.api}/questions`, { arenaConfig: mapConfig(config) })
      .pipe(map(r => ({
        status: 'success',
        total: r.questions?.length || 0,
        session_id: r.sessionId || r.session_id,
        questions: (r.questions || []).map((q: any) => ({
          id: q.id || Math.random().toString(36).substr(2, 9),
          question: q.texteQuestion || q.question,
          type: (q.typeQuestion || q.type || 'behavioral').toLowerCase(),
          source: (q.source || 'generated').toLowerCase(),
          company_specific: q.companySpecific || q.company_specific || false,
          tip: q.conseilReponse || q.tip || ''
        }))
      })));
  }

  freeChat(input: string, threadId: string, history: ChatMessage[]): Observable<{ status: string; thread_id: string; response: string }> {
    return this.http.post<any>(`${this.api}/chat`, {
      userInput: input,
      threadId: threadId,
      history: history.map(m => ({ role: m.role, content: m.content })),
    }).pipe(map(r => ({
      status: 'success',
      thread_id: r.threadId || r.thread_id || threadId,
      response: r.message || r.response || ''
    })));
  }

  startSession(config: ArenaConfig, sessionId?: string): Observable<{ status: string; session_id: string; opening_message: string }> {
    return this.http.post<any>(`${this.api}/session/start`, {
      sessionId: sessionId,
      arenaConfig: mapConfig(config),
      language: config.language,
      durationMinutes: config.duration_minutes,
    }).pipe(map(r => ({
      status: 'success',
      session_id: r.sessionId || r.session_id,
      opening_message: r.openingMessage || r.opening_message
    })));
  }

  sendMessage(sessionId: string, userInput: string, history: ChatMessage[], config: ArenaConfig): Observable<{ status: string; session_id: string; ai_response: string }> {
    return this.http.post<any>(`${this.api}/session/message`, {
      sessionId: sessionId,
      userInput: userInput,
      history: history.map(m => ({ role: m.role, content: m.content })),
      arenaConfig: mapConfig(config),
    }).pipe(map(r => ({
      status: 'success',
      session_id: r.sessionId || r.session_id,
      ai_response: r.aiResponse || r.message || r.ai_response || ''
    })));
  }

  endSession(sessionId: string, history: ChatMessage[], config: ArenaConfig): Observable<{ status: string; session_id: string; score: number; feedback: FeedbackResult }> {
    return this.http.post<any>(`${this.api}/session/end`, {
      sessionId: sessionId,
      history: history.map(m => ({ role: m.role, content: m.content })),
      arenaConfig: mapConfig(config),
    }).pipe(map(r => {
      const f = r.feedback || r;
      const score = r.score !== undefined ? r.score : (f.globalScore !== undefined ? f.globalScore : (f.global_score || 0));
      return {
        status: 'success',
        session_id: r.sessionId || r.session_id,
        score: score,
        feedback: {
          global_score: score,
          dimensions: f.dimensions || [],
          question_evaluations: f.questionEvaluations || f.question_evaluations || [],
          strengths: f.strengths || [],
          improvements: f.improvements || [],
          best_answer: f.bestAnswer || f.best_answer || '',
          worst_answer: f.worstAnswer || f.worst_answer || '',
          coaching_tips: f.coachingTips || f.coaching_tips || []
        }
      };
    }));
  }

  getSalary(config: ArenaConfig): Observable<SalaryResult & { status: string }> {
    return this.http.post<any>(`${this.api}/salary`, { arenaConfig: mapConfig(config) })
      .pipe(map(r => ({
        status: 'success',
        range_min: r.rangeMin || r.salaryMin || r.range_min || 0,
        range_max: r.rangeMax || r.salaryMax || r.range_max || 0,
        currency: r.currency || 'MAD',
        your_target: r.yourTarget || r.your_target || 0,
        confidence_level: r.confidenceLevel || r.confidence_level || 'medium',
        market_sources: r.marketSources || r.market_sources || [],
        negotiation_script: (r.negotiationScript || r.negotiation_script || []).map((s: any) => ({
          step: s.step,
          action: s.action,
          phrase: s.phrase,
          why: s.why
        }))
      })));
  }

  salaryCoach(
    userInput: string,
    threadId: string,
    salaryCtx: { rangeMin: number; rangeMax: number; currency: string; yourTarget: number },
    history: { role: string; content: string }[]
  ): Observable<{ status: string; response: string }> {
    return this.http.post<any>(`${this.api}/salary-coach`, {
      userInput: userInput,
      threadId: threadId,
      salaryContext: salaryCtx,
      history,
    });
  }

  getHistory(): Observable<SessionSummary[]> {
    // Note: The backend expects userId in query or gets it from token
    return this.http.get<SessionSummary[]>(`${this.api}/sessions`);
  }

  getSessionDetails(sessionId: string): Observable<FeedbackResult> {
    return this.http.get<FeedbackResult>(`${this.api}/sessions/${sessionId}/details`);
  }
}