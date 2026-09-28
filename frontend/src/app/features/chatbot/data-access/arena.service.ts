import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../../environments/environment';
import { ArenaConfig, QuestionItem, ChatMessage, FeedbackResult, SalaryResult, SessionSummary, SessionDetail, UserOfferSummary } from '../models/arena.models';

const mapConfig = (c: ArenaConfig) => ({
  domain: c.domain,
  level: c.level,
  durationMinutes: c.duration_minutes,
  language: c.language,
  focusAreas: c.focus_areas,
  offerId: c.offer_id
});

@Injectable({ providedIn: 'root' })
export class ArenaService {
  private readonly api = `${environment.apiBaseUrl}/arena`;

  constructor(private http: HttpClient) { }

  getQuestions(config: ArenaConfig): Observable<{ status: string; total: number; session_id: string; questions: QuestionItem[] }> {
    const body: any = { arenaConfig: mapConfig(config) };
    if (config.offer_id) body['offerId'] = config.offer_id;
    return this.http.post<any>(`${this.api}/questions`, body)
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

  startSession(config: ArenaConfig, sessionId?: string, questions?: QuestionItem[]): Observable<{ status: string; session_id: string; opening_message: string }> {
    const body: any = {
      sessionId: sessionId,
      arenaConfig: mapConfig(config),
      questions: questions || []
    };
    if (config.offer_id) body['offerId'] = config.offer_id;

    return this.http.post<any>(`${this.api}/session/start`, body).pipe(map(r => ({
      status: 'success',
      session_id: r.sessionId || r.session_id,
      opening_message: r.openingMessage || r.opening_message
    })));
  }

  sendMessage(sessionId: string, userInput: string, history: ChatMessage[], config: ArenaConfig): Observable<{ status: string; session_id: string; ai_response: string }> {
    const body: any = {
      sessionId: sessionId,
      userInput: userInput,
      history: history.map(m => ({ role: m.role, content: m.content })),
      arenaConfig: mapConfig(config),
    };
    if (config.offer_id) body['offerId'] = config.offer_id;

    return this.http.post<any>(`${this.api}/session/message`, body).pipe(map(r => ({
      status: 'success',
      session_id: r.sessionId || r.session_id,
      ai_response: r.aiResponse || r.message || r.ai_response || ''
    })));
  }

  endSession(sessionId: string, history: ChatMessage[], config: ArenaConfig): Observable<{ status: string; session_id: string; score: number; feedback: FeedbackResult }> {
    const body: any = {
      sessionId: sessionId,
      history: history.map(m => ({ role: m.role, content: m.content })),
      arenaConfig: mapConfig(config),
    };
    if (config.offer_id) body['offerId'] = config.offer_id;

    return this.http.post<any>(`${this.api}/session/end`, body).pipe(map(r => {
      const f = r.feedback || r;
      const score = r.score !== undefined ? r.score : (f.globalScore !== undefined ? f.globalScore : (f.global_score || 0));
      return {
        status: 'success',
        session_id: r.sessionId || r.session_id,
        score: score,
        feedback: {
          globalScore: score,
          dimensions: f.dimensions || [],
          questionEvaluations: (f.questionEvaluations || f.question_evaluations || []).map((qe: any) => ({
            question: qe.question,
            userAnswer: qe.userAnswer || qe.user_answer,
            score: qe.score,
            correction: qe.correction
          })),
          strengths: f.strengths || [],
          improvements: f.improvements || [],
          bestAnswer: f.bestAnswer || f.best_answer || '',
          worstAnswer: f.worstAnswer || f.worst_answer || '',
          coachingTips: f.coachingTips || f.coaching_tips || []
        }
      };
    }));
  }

  getSalary(config: ArenaConfig): Observable<SalaryResult & { status: string }> {
    const body: any = { arenaConfig: mapConfig(config) };
    if (config.offer_id) body['offerId'] = config.offer_id;

    return this.http.post<any>(`${this.api}/salary`, body)
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
    history: { role: string; content: string }[],
    config: ArenaConfig
  ): Observable<{ status: string; response: string }> {
    return this.http.post<any>(`${this.api}/salary-coach`, {
      userInput: userInput,
      threadId: threadId,
      salaryContext: salaryCtx,
      history,
      offerId: config?.offer_id || null,
      arenaConfig: config ? mapConfig(config) : null,
      mode: config?.offer_id ? 'offer' : 'arena'
    });
  }

  // Liste légère → cards
  getSessions(userId: string): Observable<SessionSummary[]> {
    return this.http.get<any[]>(`${this.api}/sessions`, {
      params: { userId }
    }).pipe(map(list => (list || []).map(s => ({
      sessionId: s.sessionId || s.session_id || s.idSession,
      mode: s.mode,
      status: s.status,
      language: s.language,
      durationMinutes: s.durationMinutes || s.duration_minutes,
      domain: s.domain,
      level: s.level,
      scoreEntretien: s.scoreEntretien !== undefined ? s.scoreEntretien : s.score_entretien,
      dateSession: s.dateSession || s.date_session,
      completedAt: s.completedAt || s.completed_at,
      jobTitle: s.jobTitle || s.job_title,
      company: s.company
    }))));
  }

  // Détail complet → modal
  getSessionDetail(sessionId: string): Observable<SessionDetail> {
    return this.http.get<any>(`${this.api}/sessions/${sessionId}`)
      .pipe(map(s => ({
        sessionId: s.sessionId || s.session_id,
        mode: s.mode,
        status: s.status,
        language: s.language,
        durationMinutes: s.durationMinutes || s.duration_minutes,
        domain: s.domain,
        level: s.level,
        globalScore: s.globalScore !== undefined ? s.globalScore : (s.global_score || s.scoreEntretien || s.score_entretien || 0),
        dateSession: s.dateSession || s.date_session,
        dimensions: s.dimensions || [],
        strengths: s.strengths || [],
        improvements: s.improvements || [],
        coachingTips: s.coachingTips || s.coaching_tips || [],
        questionEvaluations: (s.questionEvaluations || s.question_evaluations || []).map((qe: any) => ({
          question: qe.question,
          userAnswer: qe.userAnswer || qe.user_answer,
          score: qe.score,
          correction: qe.correction
        })),
        bestAnswer: s.bestAnswer || s.best_answer,
        worstAnswer: s.worstAnswer || s.worst_answer,
        jobTitle: s.jobTitle || s.job_title,
        company: s.company
      })));
  }

  // Suppression
  deleteSession(sessionId: string): Observable<void> {
    return this.http.post<void>(`${this.api}/sessions/${sessionId}/delete`, {});
  }

  // Offers page — fetch analyzed offers for the authenticated user
  getMyOffers(): Observable<UserOfferSummary[]> {
    return this.http.get<any[]>(`${this.api}/my-offers`).pipe(
      map(list => (list || []).map(o => ({
        offerId:         o.offerId,
        jobTitle:        o.jobTitle,
        company:         o.company,
        location:        o.location,
        contractType:    o.contractType,
        matchingScore:   o.matchingScore,
        yearsExperience: o.yearsExperience,
        requiredSkills:  o.requiredSkills || [],
        dateAnalysed:    o.dateAnalysed,
      })))
    );
  }
}
