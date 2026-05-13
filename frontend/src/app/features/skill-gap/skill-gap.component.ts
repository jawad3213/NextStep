import { Component, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { ProfileService } from '../profile/profile.service';
import { firstValueFrom } from 'rxjs';

interface SkillGapResult {
  candidateName: string;
  jobTitle: string;
  relevanceScore: number;
  matchedSkills: { name: string; category: string }[];
  missingSkills: { name: string; category: string; priority: string }[];
  requiredCerts: string[];
  certMatch: boolean;
  experienceYears: number;
  requiredYears: number;
  experienceGapYears: number;
  flag: 'PERFECT' | 'MINOR' | 'CRITICAL';
  recommendations: { type: string; title: string; description: string; priority: string }[];
  revisionHints: string[];
}

@Component({
  selector: 'app-skill-gap',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="skillgap-shell">
      <header class="page-header">
        <div class="header-left">
          <h1 class="page-title">Skill Gap Analysis</h1>
          <p class="page-subtitle">Analysez les ecarts entre votre profil et les exigences du poste.</p>
        </div>
      </header>

      <div class="search-section">
        <div class="input-row">
          <div class="input-group">
            <label class="input-label">Description du poste</label>
            <textarea [(ngModel)]="offerText" class="input-textarea" placeholder="Collez la description de l'offre d'emploi ici..." rows="4"></textarea>
          </div>
          <button class="btn-analyze" (click)="analyzeGap()" [disabled]="!offerText() || loading()">
            @if (loading()) { <span class="spinner-sm"></span> Analyse... }
            @else { <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="11" cy="11" r="8"/><path d="m21 21-4.3-4.3"/></svg> Analyser }
          </button>
        </div>
      </div>

      @if (result(); as r) {
        <div class="results">
          <div class="score-header-card" [class.perfect]="r.flag === 'PERFECT'" [class.minor]="r.flag === 'MINOR'" [class.critical]="r.flag === 'CRITICAL'">
            <div class="score-circle">
              <svg viewBox="0 0 36 36">
                <path d="M18 2.0845 a 15.9155 15.9155 0 0 1 0 31.831 a 15.9155 15.9155 0 0 1 0 -31.831" fill="none" stroke="#E0E0E0" stroke-width="3"/>
                <path d="M18 2.0845 a 15.9155 15.9155 0 0 1 0 31.831 a 15.9155 15.9155 0 0 1 0 -31.831" fill="none" stroke="currentColor" stroke-width="3" [attr.stroke-dasharray]="r.relevanceScore + ', 100'" stroke-linecap="round"/>
              </svg>
              <span class="score-number">{{ r.relevanceScore }}%</span>
            </div>
            <div class="score-info">
              <h2>{{ r.flag === 'PERFECT' ? 'Excellent match !' : r.flag === 'MINOR' ? 'Quelques ecarts' : 'Ecart significatif' }}</h2>
              <p>Score de pertinence pour le poste de <strong>{{ r.jobTitle }}</strong></p>
              <div class="exp-badge">Experience: {{ r.experienceYears }} ans / {{ r.requiredYears }} ans requis</div>
            </div>
          </div>

          <div class="grid-2col">
            <div class="card match-card">
              <h3><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"/><polyline points="22 4 12 14.01 9 11.01"/></svg> Competences correspondantes ({{ r.matchedSkills.length }})</h3>
              @if (r.matchedSkills.length > 0) {
                <div class="skills-list">
                  @for (s of r.matchedSkills; track s.name) {
                    <div class="skill-chip match">
                      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="20 6 9 17 4 12"/></svg>
                      {{ s.name }}
                    </div>
                  }
                </div>
              } @else { <p class="empty-text">Aucune competence correspondante identifiee</p> }
            </div>

            <div class="card gap-card">
              <h3><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><line x1="15" y1="9" x2="9" y2="15"/><line x1="9" y1="9" x2="15" y2="15"/></svg> Competences manquantes ({{ r.missingSkills.length }})</h3>
              @if (r.missingSkills.length > 0) {
                <div class="skills-list">
                  @for (s of r.missingSkills; track s.name) {
                    <div class="skill-chip gap" [class.high-priority]="s.priority === 'high'">
                      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>
                      {{ s.name }}
                      @if (s.priority === 'high') { <span class="priority-badge">Prioritaire</span> }
                    </div>
                  }
                </div>
              } @else { <p class="empty-text">Toutes les competences sont couvertes !</p> }
            </div>
          </div>

          @if (r.recommendations.length > 0) {
            <div class="card">
              <h3><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 20h9"/><path d="M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4L16.5 3.5z"/></svg> Recommandations</h3>
              <div class="rec-list">
                @for (rec of r.recommendations; track rec.title) {
                  <div class="rec-item" [class.high]="rec.priority === 'high'" [class.medium]="rec.priority === 'medium'">
                    <div class="rec-icon">
                      @if (rec.type === 'COURSE') { <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20"/><path d="M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z"/></svg> }
                      @else if (rec.type === 'CERTIFICATION') { <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"/><polyline points="22 4 12 14.01 9 11.01"/></svg> }
                      @else { <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M10 20l4-16"/></svg> }
                    </div>
                    <div class="rec-content">
                      <strong>{{ rec.title }}</strong>
                      <p>{{ rec.description }}</p>
                    </div>
                    <span class="rec-priority" [class.high]="rec.priority === 'high'" [class.medium]="rec.priority === 'medium'">{{ rec.priority }}</span>
                  </div>
                }
              </div>
            </div>
          }

          @if (r.revisionHints.length > 0) {
            <div class="card hints">
              <h3><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><path d="M9.09 9a3 3 0 0 1 5.83 1c0 2-3 3-3 3"/><line x1="12" y1="17" x2="12.01" y2="17"/></svg> Conseils de revision</h3>
              <ul>@for (h of r.revisionHints; track h) { <li>{{ h }}</li> }</ul>
            </div>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    :host { display: flex; flex-direction: column; flex: 1; height: 100%; min-height: 0; overflow-y: auto; }
    .skillgap-shell { display: flex; flex-direction: column; padding: 24px 40px; gap: 20px; }
    .page-header { display: flex; justify-content: space-between; align-items: flex-start; }
    .header-left { display: flex; flex-direction: column; gap: 4px; }
    .page-title { font-size: 24px; font-weight: 700; color: #212121; margin: 0; font-family: 'Lato', sans-serif; }
    .page-subtitle { font-size: 14px; color: #616161; margin: 0; }

    .search-section { background: white; border: 1px solid #E0E0E0; border-radius: 12px; padding: 20px; }
    .input-row { display: flex; gap: 12px; align-items: flex-start; }
    .input-group { flex: 1; display: flex; flex-direction: column; gap: 4px; }
    .input-label { font-size: 12px; font-weight: 700; color: #616161; }
    .input-textarea { padding: 12px; border: 1px solid #E0E0E0; border-radius: 4px; background: #F5F5F5; font-size: 13px; resize: vertical; &:focus { outline: none; border-color: #1A91F0; background: white; box-shadow: 0 0 0 3px rgba(26,145,240,0.15); } }

    .btn-analyze { display: flex; align-items: center; gap: 8px; padding: 10px 24px; margin-top: 20px; background: #0C1986; color: white; border: none; border-radius: 8px; font-size: 14px; font-weight: 600; cursor: pointer; white-space: nowrap; svg { width: 18px; height: 18px; } &:hover { background: #091361; } &:disabled { background: #BDBDBD; cursor: not-allowed; } }
    .spinner-sm { width: 16px; height: 16px; border: 2px solid rgba(255,255,255,0.3); border-top-color: white; border-radius: 50%; animation: spin 0.8s linear infinite; }
    @keyframes spin { 100% { transform: rotate(360deg); } }

    .results { display: flex; flex-direction: column; gap: 16px; }
    .score-header-card { display: flex; align-items: center; gap: 24px; background: white; border: 1px solid #E0E0E0; border-radius: 16px; padding: 24px; &.perfect { border-left: 4px solid #34A853; } &.minor { border-left: 4px solid #F59B00; } &.critical { border-left: 4px solid #D93025; } }
    .score-circle { position: relative; width: 80px; height: 80px; flex-shrink: 0; svg { width: 80px; height: 80px; } .score-number { position: absolute; top: 50%; left: 50%; transform: translate(-50%, -50%); font-size: 18px; font-weight: 800; color: #212121; } }
    .score-info h2 { margin: 0 0 4px; font-size: 18px; font-weight: 700; color: #212121; }
    .score-info p { margin: 0 0 8px; font-size: 13px; color: #616161; }
    .exp-badge { display: inline-block; padding: 4px 12px; background: #F1F5F9; border-radius: 100px; font-size: 12px; font-weight: 600; color: #475569; }

    .grid-2col { display: grid; grid-template-columns: 1fr 1fr; gap: 16px; }
    .card { background: white; border: 1px solid #E0E0E0; border-radius: 12px; padding: 20px; h3 { margin: 0 0 12px; font-size: 15px; font-weight: 700; color: #212121; display: flex; align-items: center; gap: 8px; svg { width: 18px; height: 18px; stroke: #0C1986; } } }

    .skills-list { display: flex; flex-wrap: wrap; gap: 8px; }
    .skill-chip { display: flex; align-items: center; gap: 6px; padding: 6px 12px; border-radius: 100px; font-size: 12px; font-weight: 600; svg { width: 14px; height: 14px; } &.match { background: #E6F4EA; color: #34A853; } &.gap { background: #FCE8E6; color: #D93025; } &.high-priority { background: #FEF2F2; border: 1px solid #D93025; } }
    .priority-badge { font-size: 9px; padding: 2px 4px; background: #D93025; color: white; border-radius: 4px; }

    .empty-text { color: #9E9E9E; font-size: 13px; margin: 0; }

    .rec-list { display: flex; flex-direction: column; gap: 8px; }
    .rec-item { display: flex; gap: 12px; padding: 12px; background: #FAFAFA; border-radius: 8px; border-left: 3px solid #E0E0E0; &.high { border-left-color: #D93025; } &.medium { border-left-color: #F59B00; } }
    .rec-icon { width: 32px; height: 32px; background: #F1F5F9; border-radius: 6px; display: flex; align-items: center; justify-content: center; color: #0C1986; flex-shrink: 0; svg { width: 16px; height: 16px; } }
    .rec-content { flex: 1; strong { display: block; font-size: 13px; color: #212121; margin-bottom: 2px; } p { margin: 0; font-size: 12px; color: #616161; line-height: 1.4; } }
    .rec-priority { padding: 2px 8px; border-radius: 100px; font-size: 10px; font-weight: 700; text-transform: capitalize; &.high { background: #FCE8E6; color: #D93025; } &.medium { background: #FFF8E6; color: #F59B00; } }

    .card.hints ul { margin: 0; padding-left: 20px; li { font-size: 13px; color: #424242; margin-bottom: 6px; line-height: 1.5; } }
  `]
})
export class SkillGapComponent {
  private http = inject(HttpClient);
  private profileService = inject(ProfileService);
  private baseUrl = environment.apiBaseUrl;

  offerText = signal('');
  loading = signal(false);
  result = signal<SkillGapResult | null>(null);

  async analyzeGap() {
    if (!this.offerText()) return;
    this.loading.set(true);
    const profile = this.profileService.profile();
    try {
      const res = await firstValueFrom(this.http.post<SkillGapResult>(`${this.baseUrl.replace('/api', '')}/offer/match`, {
        offer_text: this.offerText(),
        profile: {
          skills: profile.skills.map(s => ({ name: s.name, category: s.category || 'Technical' })),
          experience_years: profile.experience.length,
          certifications: profile.certifications.map(c => c.name)
        }
      }));
      this.result.set(res);
    } catch {
      this.result.set(mockGap);
    } finally {
      this.loading.set(false);
    }
  }
}

const mockGap: SkillGapResult = {
  candidateName: 'Jean Dupont',
  jobTitle: 'Full Stack Developer',
  relevanceScore: 72,
  matchedSkills: [
    { name: 'Angular', category: 'Frontend' },
    { name: 'React', category: 'Frontend' },
    { name: 'TypeScript', category: 'Language' },
    { name: 'PostgreSQL', category: 'Database' },
    { name: 'Node.js', category: 'Backend' },
  ],
  missingSkills: [
    { name: 'Docker', category: 'DevOps', priority: 'high' },
    { name: 'Kubernetes', category: 'DevOps', priority: 'high' },
    { name: 'AWS', category: 'Cloud', priority: 'medium' },
    { name: 'GraphQL', category: 'API', priority: 'low' },
  ],
  requiredCerts: ['AWS Certified Developer'],
  certMatch: false,
  experienceYears: 2,
  requiredYears: 3,
  experienceGapYears: 1,
  flag: 'MINOR',
  recommendations: [
    { type: 'COURSE', title: 'Docker pour les developpeurs', description: 'Maitrisez les conteneurs Docker et l\'orchestration de base', priority: 'high' },
    { type: 'COURSE', title: 'Introduction a Kubernetes', description: 'Comprendre les concepts de base de K8s pour le deploiement', priority: 'high' },
    { type: 'CERTIFICATION', title: 'AWS Cloud Practitioner', description: 'Certification fondamentale pour demarrer sur AWS', priority: 'medium' },
    { type: 'PROJECT', title: 'Projet full-stack avec conteneurisation', description: 'Ajoutez un projet Docker a votre portfolio', priority: 'medium' },
  ],
  revisionHints: [
    'Ajoutez Docker Compose a vos projets existants pour demontrer votre connaissance des conteneurs',
    'Suivez le "Docker Mastery" course sur Udemy (environ 2 semaines)',
    'Creer un petit projet deploye avec Docker et ajoutez-le a votre section projets',
  ],
};
