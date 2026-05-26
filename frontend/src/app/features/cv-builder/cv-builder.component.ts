import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { ProfileService } from '../profile/profile.service';
import { firstValueFrom } from 'rxjs';


interface CvTemplate {
  id: string;
  slug: string;
  name: string;
  description: string;
  thumbnailUrl: string;
  industries: string[];
  experienceLevels: string[];
  style: string;
  layoutFlags: string[];
  backgroundColor: string;
  tags: string[];
}

interface CvHistoryItem {
  id: string;
  title: string;
  templateSlug: string;
  templateName: string;
  fileUrl: string;
  fileSizeBytes: number;
  createdAt: string;
  updatedAt: string;
}

interface CvData {
  candidate: { name: string; email: string; phone: string; location: string; linkedIn: string; gitHub: string; portfolio: string };
  summary: string;
  experience: { role: string; company: string; start: string; end: string; bullets: string[] }[];
  education: { degree: string; institution: string; year: string }[];
  skills: { name: string; level: number; isMatched: boolean }[];
  projects: { title: string; description: string; bullets: string[] }[];
  certifications: string[];
  languages: string[];
  activities: { title: string; role: string; description: string }[];
  themeColor: string;
  fontFamily: string;
  atsScore: number;
  matchingScore: number;
  atsCoveragePct: number;
}

@Component({
  selector: 'app-cv-builder',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="cv-shell">
      <header class="page-header">
        <div class="header-left">
          <h1 class="page-title">CV Builder</h1>
          <p class="page-subtitle">Selectionnez un template, prévisualisez votre CV optimise ATS et gérez vos versions.</p>
        </div>
        <div class="header-right">
          <button class="btn-refresh" (click)="refreshTemplates()" [class.spinning]="loadingTemplates()" title="Rafraichir">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="23 4 23 10 17 10"/><polyline points="1 20 1 14 7 14"/><path d="M3.51 9a9 9 0 0 1 14.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0 0 20.49 15"/></svg>
          </button>
        </div>
      </header>

      <!-- Tabs: Templates / Historique -->
      <div class="tab-bar">
        <button class="tab" [class.active]="activeTab() === 'templates'" (click)="activeTab.set('templates')">Templates</button>
        <button class="tab" [class.active]="activeTab() === 'history'" (click)="loadHistory(); activeTab.set('history')">Historique</button>
        <button class="tab" [class.active]="activeTab() === 'preview'" (click)="activeTab.set('preview')" [class.disabled]="!selectedTemplate()">Apercu</button>
      </div>

      <!-- TEMPLATES TAB -->
      @if (activeTab() === 'templates') {
        <div class="templates-section">
          <div class="template-filters">
            <div class="search-box">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="search-icon"><circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/></svg>
              <input type="text" placeholder="Rechercher un template..." [(ngModel)]="templateSearch" />
            </div>
            <div class="filter-select-wrapper">
              <select [(ngModel)]="styleFilter">
                <option value="">Tous les styles</option>
                <option value="Modern">Moderne</option>
                <option value="Classic">Classique</option>
                <option value="Executive">Executif</option>
                <option value="Pro">Professionnel</option>
                <option value="Elegant">Elegant</option>
              </select>
            </div>
          </div>

          @if (loadingTemplates()) {
            <div class="loading-grid">
              @for (_ of [1,2,3,4,5,6]; track _) {
                <div class="template-skeleton"></div>
              }
            </div>
          } @else {
            <div class="templates-grid">
              @for (tpl of filteredTemplates(); track tpl.id) {
                <div class="template-card" [class.selected]="selectedTemplate()?.id === tpl.id" (click)="selectTemplate(tpl)">
                  <div class="template-thumb" [style.background]="tpl.backgroundColor || '#F1F5F9'">
                    @if (isPngThumbnailSupported(tpl.thumbnailUrl)) {
                      <img [src]="thumbnailUrl(tpl.thumbnailUrl)" class="thumb-pdf" loading="lazy" alt="{{ tpl.name }} preview" />
                    } @else {
                      <div class="thumb-placeholder">
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/></svg>
                        <span>{{ tpl.name }}</span>
                      </div>
                    }
                    <div class="template-badges">
                      @for (tag of tpl.tags.slice(0, 2); track tag) {
                        <span class="tpl-tag">{{ tag }}</span>
                      }
                    </div>
                  </div>
                  <div class="template-info">
                    <h3>{{ tpl.name }}</h3>
                    <p>{{ tpl.description }}</p>
                    <div class="template-meta">
                      <span class="meta-style">{{ tpl.style }}</span>
                      @for (ind of tpl.industries.slice(0, 2); track ind) {
                        <span class="meta-ind">{{ ind }}</span>
                      }
                    </div>
                  </div>
                </div>
              }
            </div>
          }

          <div class="action-bar">
            <button class="btn-preview" [disabled]="!selectedTemplate()" (click)="generatePreview()">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/></svg>
              Apercu & Generer
            </button>
          </div>
        </div>
      }

      <!-- HISTORY TAB -->
      @if (activeTab() === 'history') {
        <div class="history-section">
          @if (loadingHistory()) {
            <div class="loading-center"><div class="spinner-lg"></div><p>Chargement de l'historique...</p></div>
          } @else if (historyItems().length === 0) {
            <div class="empty-state">
              <div class="empty-icon">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/></svg>
              </div>
              <h3>Aucun CV genere</h3>
              <p>Generez votre premier CV depuis l'onglet Templates.</p>
            </div>
          } @else {
            <div class="history-list">
              @for (item of historyItems(); track item.id) {
                <div class="history-card">
                  <div class="history-icon">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/></svg>
                  </div>
                  <div class="history-details">
                    <h4>{{ item.title || item.templateName }}</h4>
                    <p class="history-meta">
                      <span>Template: {{ item.templateName }}</span>
                      <span class="dot">•</span>
                      <span>{{ item.createdAt | date:'dd/MM/yyyy' }}</span>
                      <span class="dot">•</span>
                      <span>{{ (item.fileSizeBytes / 1024).toFixed(0) }} KB</span>
                    </p>
                  </div>
                  <div class="history-actions">
                    <button class="btn-icon" title="Telecharger" (click)="downloadCv(item.id)">
                      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/></svg>
                    </button>
                    <button class="btn-icon" title="Supprimer" (click)="deleteCv(item.id)">
                      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="3 6 5 6 21 6"/><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/></svg>
                    </button>
                  </div>
                </div>
              }
            </div>
          }
        </div>
      }

      <!-- PREVIEW TAB -->
      @if (activeTab() === 'preview' && previewData()) {
        <div class="preview-section">
          <div class="preview-header">
            <h2>Apercu du CV</h2>
            <div class="preview-scores">
              <div class="score-chip" [class.high]="previewData()!.atsScore >= 80" [class.mid]="previewData()!.atsScore >= 50 && previewData()!.atsScore < 80" [class.low]="previewData()!.atsScore < 50">
                ATS: {{ previewData()!.atsScore }}%
              </div>
              <div class="score-chip" [class.high]="previewData()!.matchingScore >= 80" [class.mid]="previewData()!.matchingScore >= 50 && previewData()!.matchingScore < 80" [class.low]="previewData()!.matchingScore < 50">
                Match: {{ previewData()!.matchingScore }}%
              </div>
            </div>
          </div>

          <div class="preview-content">
            <div class="cv-section">
              <h3>{{ previewData()!.candidate.name }}</h3>
              <p class="cv-contact">{{ previewData()!.candidate.email }} • {{ previewData()!.candidate.phone }} • {{ previewData()!.candidate.location }}</p>
              <p class="cv-summary">{{ previewData()!.summary }}</p>
            </div>

            @if (previewData()!.experience.length > 0) {
              <div class="cv-section">
                <h4>Experience</h4>
                @for (exp of previewData()!.experience; track exp.role + exp.company) {
                  <div class="cv-entry">
                    <div class="entry-header">
                      <strong>{{ exp.role }}</strong> &#64; {{ exp.company }}
                      <span class="entry-date">{{ exp.start }} - {{ exp.end || 'Present' }}</span>
                    </div>
                    <ul>
                      @for (b of exp.bullets; track b) { <li>{{ b }}</li> }
                    </ul>
                  </div>
                }
              </div>
            }

            @if (previewData()!.education.length > 0) {
              <div class="cv-section">
                <h4>Formation</h4>
                @for (edu of previewData()!.education; track edu.degree + edu.institution) {
                  <div class="cv-entry">
                    <div class="entry-header">
                      <strong>{{ edu.degree }}</strong> — {{ edu.institution }}
                      <span class="entry-date">{{ edu.year }}</span>
                    </div>
                  </div>
                }
              </div>
            }

            @if (previewData()!.projects.length > 0) {
              <div class="cv-section">
                <h4>Projets</h4>
                @for (prj of previewData()!.projects; track prj.title) {
                  <div class="cv-entry">
                    <div class="entry-header">
                      <strong>{{ prj.title }}</strong>
                    </div>
                    @if (prj.description) { <p class="cv-project-desc">{{ prj.description }}</p> }
                    @if (prj.bullets && prj.bullets.length > 0) {
                      <ul>
                        @for (b of prj.bullets; track b) { <li>{{ b }}</li> }
                      </ul>
                    }
                  </div>
                }
              </div>
            }

            @if (previewData()!.skills.length > 0) {
              <div class="cv-section">
                <h4>Competences</h4>
                <div class="skills-grid">
                  @for (sk of previewData()!.skills; track sk.name) {
                    <div class="skill-item" [class.matched]="sk.isMatched" [style.--skill-level]="sk.level / 5">
                      <span>{{ sk.name }}</span>
                      @if (sk.isMatched) { <span class="match-badge">ATS+</span> }
                    </div>
                  }
                </div>
              </div>
            }

            @if (previewData()!.certifications && previewData()!.certifications.length > 0) {
              <div class="cv-section">
                <h4>Certifications</h4>
                <ul class="cv-bullets-flat">
                  @for (cert of previewData()!.certifications; track cert) {
                    <li>{{ cert }}</li>
                  }
                </ul>
              </div>
            }

            @if (previewData()!.languages && previewData()!.languages.length > 0) {
              <div class="cv-section">
                <h4>Langues</h4>
                <div class="languages-grid">
                  @for (lang of previewData()!.languages; track lang) {
                    <span class="lang-chip">{{ lang }}</span>
                  }
                </div>
              </div>
            }

            @if (previewData()!.activities && previewData()!.activities.length > 0) {
              <div class="cv-section">
                <h4>Activites Extra-professionnelles</h4>
                @for (act of previewData()!.activities; track act.title) {
                  <div class="cv-entry">
                    <div class="entry-header">
                      <strong>{{ act.title }}</strong>
                      @if (act.role) { <span> — {{ act.role }}</span> }
                    </div>
                    @if (act.description) { <p class="cv-project-desc">{{ act.description }}</p> }
                  </div>
                }
              </div>
            }
          </div>

          <div class="preview-actions">
            <button class="btn-save" (click)="saveCv()" [disabled]="saving()">
              @if (saving()) {
                <span class="spinner-sm"></span> Sauvegarde...
              } @else {
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z"/><polyline points="17 21 17 13 7 13 7 21"/><polyline points="7 3 7 8 15 8"/></svg>
                Sauvegarder le CV
              }
            </button>
            <button class="btn-download" (click)="downloadLastCv()">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/></svg>
              Telecharger PDF
            </button>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    :host { display: flex; flex-direction: column; flex: 1; height: 100%; min-height: 0; overflow-y: auto; }
    .cv-shell { display: flex; flex-direction: column; height: 100%; padding: 24px 40px; gap: 20px; }
    .page-header { display: flex; justify-content: space-between; align-items: flex-start; }
    .header-left { display: flex; flex-direction: column; gap: 4px; }
    .page-title { font-size: 24px; font-weight: 700; color: #212121; margin: 0; font-family: 'Lato', sans-serif; }
    .page-subtitle { font-size: 14px; color: #616161; margin: 0; }
    .header-right { display: flex; gap: 8px; }
    .btn-refresh { width: 40px; height: 40px; border-radius: 4px; border: 1px solid #E0E0E0; background: #FAFAFA; display: flex; align-items: center; justify-content: center; color: #616161; cursor: pointer; transition: all 0.2s; svg { width: 18px; height: 18px; } &:hover { background: #F5F5F5; } &.spinning svg { animation: spin 1s linear infinite; } }
    @keyframes spin { 100% { transform: rotate(360deg); } }

    .tab-bar { display: flex; gap: 4px; background: #F5F5F5; border-radius: 8px; padding: 4px; width: fit-content; }
    .tab { padding: 8px 20px; border: none; background: transparent; border-radius: 6px; font-size: 13px; font-weight: 600; color: #616161; cursor: pointer; transition: all 0.2s; &.active { background: white; color: #0C1986; box-shadow: 0 1px 3px rgba(0,0,0,0.1); } &.disabled { opacity: 0.4; cursor: not-allowed; } }

    .templates-section { display: flex; flex-direction: column; gap: 16px; flex: 1; }
    .template-filters { display: flex; gap: 12px; }
    .search-box { flex: 1; position: relative; display: flex; align-items: center; }
    .search-icon { position: absolute; left: 12px; width: 16px; height: 16px; color: #9E9E9E; }
    .search-box input { width: 100%; height: 40px; padding: 0 12px 0 36px; border: 1px solid #E0E0E0; border-radius: 4px; background: #F5F5F5; font-size: 13px; &:focus { outline: none; border-color: #1A91F0; background: white; box-shadow: 0 0 0 3px rgba(26,145,240,0.15); } }
    .filter-select-wrapper select { height: 40px; padding: 0 32px 0 12px; border: 1px solid #E0E0E0; border-radius: 4px; background: #F5F5F5; font-size: 13px; appearance: none; cursor: pointer; min-width: 160px; &:focus { outline: none; border-color: #1A91F0; } }

    .templates-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(280px, 1fr)); gap: 16px; }
    .template-card { background: white; border: 1px solid #E0E0E0; border-radius: 12px; overflow: hidden; cursor: pointer; transition: all 0.2s; &:hover { border-color: #1A91F0; box-shadow: 0 4px 12px rgba(0,0,0,0.08); transform: translateY(-2px); } &.selected { border-color: #0C1986; box-shadow: 0 0 0 2px rgba(12,25,134,0.2); } }
    .template-thumb { height: 140px; display: flex; align-items: center; justify-content: center; position: relative; overflow: hidden; }
    .thumb-placeholder { display: flex; flex-direction: column; align-items: center; gap: 8px; color: #94A3B8; svg { width: 36px; height: 36px; } span { font-size: 12px; font-weight: 600; } }
    .thumb-pdf { width: 100%; height: 100%; border: none; pointer-events: none; }
    .template-badges { position: absolute; top: 8px; right: 8px; display: flex; gap: 4px; }
    .tpl-tag { padding: 2px 6px; background: rgba(0,0,0,0.4); color: white; border-radius: 4px; font-size: 10px; font-weight: 600; }
    .template-info { padding: 16px; h3 { margin: 0 0 4px; font-size: 15px; font-weight: 600; color: #212121; } p { margin: 0 0 8px; font-size: 12px; color: #616161; line-height: 1.4; } }
    .template-meta { display: flex; gap: 6px; flex-wrap: wrap; .meta-style, .meta-ind { padding: 2px 6px; background: #F1F5F9; border-radius: 4px; font-size: 10px; font-weight: 600; color: #475569; } }

    .loading-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(280px, 1fr)); gap: 16px; }
    .template-skeleton { height: 280px; background: #F1F5F9; border-radius: 12px; animation: pulse 1.5s infinite; }
    @keyframes pulse { 0%, 100% { opacity: 1; } 50% { opacity: 0.5; } }

    .action-bar { display: flex; justify-content: center; padding: 16px 0; }
    .btn-preview { display: flex; align-items: center; gap: 8px; padding: 12px 32px; background: #0C1986; color: white; border: none; border-radius: 8px; font-size: 14px; font-weight: 600; cursor: pointer; transition: all 0.2s; svg { width: 18px; height: 18px; } &:hover { background: #091361; box-shadow: 0 4px 12px rgba(12,25,134,0.3); } &:disabled { background: #BDBDBD; cursor: not-allowed; } }

    .history-section { flex: 1; }
    .history-list { display: flex; flex-direction: column; gap: 8px; }
    .history-card { display: flex; align-items: center; gap: 16px; background: white; border: 1px solid #E0E0E0; border-radius: 10px; padding: 16px; transition: all 0.2s; &:hover { border-color: #1A91F0; } }
    .history-icon { width: 40px; height: 40px; background: #F1F5F9; border-radius: 8px; display: flex; align-items: center; justify-content: center; color: #0C1986; svg { width: 20px; height: 20px; } }
    .history-details { flex: 1; h4 { margin: 0 0 4px; font-size: 14px; font-weight: 600; color: #212121; } .history-meta { margin: 0; font-size: 12px; color: #616161; display: flex; gap: 6px; align-items: center; .dot { opacity: 0.4; } } }
    .history-actions { display: flex; gap: 4px; }
    .btn-icon { width: 32px; height: 32px; border: none; background: transparent; border-radius: 6px; display: flex; align-items: center; justify-content: center; color: #616161; cursor: pointer; svg { width: 16px; height: 16px; } &:hover { background: #F5F5F5; color: #EF4444; } }

    .preview-section { flex: 1; display: flex; flex-direction: column; gap: 16px; }
    .preview-header { display: flex; justify-content: space-between; align-items: center; h2 { margin: 0; font-size: 18px; font-weight: 600; color: #212121; } }
    .preview-scores { display: flex; gap: 8px; }
    .score-chip { padding: 4px 12px; border-radius: 100px; font-size: 12px; font-weight: 700; &.high { background: #E6F4EA; color: #34A853; } &.mid { background: #FFF8E6; color: #F59B00; } &.low { background: #FCE8E6; color: #D93025; } }
    .preview-content { flex: 1; background: white; border: 1px solid #E0E0E0; border-radius: 12px; padding: 32px; overflow-y: auto; }
    .cv-section { margin-bottom: 24px; h3 { margin: 0 0 8px; font-size: 22px; font-weight: 700; color: #0C1986; } h4 { margin: 0 0 12px; font-size: 14px; font-weight: 700; color: #212121; text-transform: uppercase; letter-spacing: 0.05em; border-bottom: 2px solid #0C1986; padding-bottom: 4px; } }
    .cv-contact { font-size: 13px; color: #616161; margin: 0 0 12px; }
    .cv-summary { font-size: 13px; color: #424242; line-height: 1.6; margin: 0; }
    .cv-entry { margin-bottom: 16px; .entry-header { font-size: 13px; color: #212121; margin-bottom: 4px; .entry-date { float: right; color: #616161; font-weight: 400; } } ul { margin: 4px 0 0; padding-left: 20px; li { font-size: 12px; color: #424242; line-height: 1.5; } } }
    .cv-project-desc { font-size: 12px; color: #616161; margin: 4px 0 2px; font-style: italic; }
    .cv-bullets-flat { margin: 4px 0 0; padding-left: 20px; li { font-size: 12px; color: #424242; margin-bottom: 4px; } }
    .languages-grid { display: flex; flex-wrap: wrap; gap: 8px; }
    .lang-chip { padding: 4px 10px; background: #F5F7FA; border: 1px solid #E2E8F0; border-radius: 6px; font-size: 12px; color: #334155; font-weight: 500; }
    .skills-grid { display: flex; flex-wrap: wrap; gap: 8px; }
    .skill-item { padding: 4px 10px; background: #F1F5F9; border-radius: 4px; font-size: 12px; color: #424242; display: flex; align-items: center; gap: 6px; &.matched { background: #E6F4EA; border: 1px solid #34A853; } .match-badge { font-size: 9px; font-weight: 700; color: #34A853; } }

    .preview-actions { display: flex; gap: 12px; justify-content: center; }
    .btn-save, .btn-download { display: flex; align-items: center; gap: 8px; padding: 10px 24px; border: none; border-radius: 8px; font-size: 14px; font-weight: 600; cursor: pointer; transition: all 0.2s; svg { width: 18px; height: 18px; } }
    .btn-save { background: #0C1986; color: white; &:hover { background: #091361; } &:disabled { background: #BDBDBD; cursor: not-allowed; } }
    .btn-download { background: #1A91F0; color: white; &:hover { background: #0F6EBB; } }

    .loading-center { display: flex; flex-direction: column; align-items: center; justify-content: center; padding: 60px; gap: 12px; color: #616161; }
    .spinner-lg { width: 32px; height: 32px; border: 3px solid #E0E0E0; border-top-color: #0C1986; border-radius: 50%; animation: spin 0.8s linear infinite; }
    .spinner-sm { width: 16px; height: 16px; border: 2px solid rgba(255,255,255,0.3); border-top-color: white; border-radius: 50%; animation: spin 0.8s linear infinite; display: inline-block; }

    .empty-state { display: flex; flex-direction: column; align-items: center; justify-content: center; padding: 60px; text-align: center; .empty-icon { width: 64px; height: 64px; background: #F1F5F9; border-radius: 50%; display: flex; align-items: center; justify-content: center; color: #94A3B8; margin-bottom: 16px; svg { width: 32px; height: 32px; } } h3 { margin: 0 0 8px; font-size: 18px; color: #212121; } p { margin: 0; font-size: 14px; color: #616161; } }
  `]
})
export class CvBuilderComponent implements OnInit {
  private http = inject(HttpClient);
  private profileService = inject(ProfileService);
  private baseUrl = environment.apiBaseUrl;

  activeTab = signal<'templates' | 'history' | 'preview'>('templates');
  templates = signal<CvTemplate[]>([]);
  historyItems = signal<CvHistoryItem[]>([]);
  selectedTemplate = signal<CvTemplate | null>(null);
  previewData = signal<CvData | null>(null);
  loadingTemplates = signal(false);
  loadingHistory = signal(false);
  saving = signal(false);
  templateSearch = signal('');
  styleFilter = signal('');

  filteredTemplates = computed(() => {
    const search = this.templateSearch().toLowerCase();
    const style = this.styleFilter();
    return this.templates().filter(t => {
      const matchSearch = !search || t.name.toLowerCase().includes(search) || t.description.toLowerCase().includes(search) || t.tags.some(tg => tg.toLowerCase().includes(search));
      const matchStyle = !style || t.style === style;
      return matchSearch && matchStyle;
    });
  });

  ngOnInit() {
    this.refreshTemplates();
  }

  async refreshTemplates() {
    this.loadingTemplates.set(true);
    try {
      const data = await firstValueFrom(this.http.get<CvTemplate[]>(`${this.baseUrl}/cv/templates`));
      this.templates.set(data);
    } catch {
      this.templates.set(mockTemplates);
    } finally {
      this.loadingTemplates.set(false);
    }
  }

  thumbnailUrl(path: string): string {
    const normalizedPath = path.replace(/\/thumbnail\.pdf(\?.*)?$/i, '/thumbnail');
    if (normalizedPath.startsWith('http')) return normalizedPath;
    const origin = new URL(this.baseUrl).origin;
    return `${origin}${normalizedPath}`;
  }

  isPngThumbnailSupported(path: string): boolean {
    const normalizedPath = path.replace(/\/thumbnail\.pdf(\?.*)?$/i, '/thumbnail').toLowerCase();
    return normalizedPath.includes('/api/cv/templates/modern/thumbnail')
      || normalizedPath.includes('/api/cv/templates/latex/thumbnail');
  }

  selectTemplate(tpl: CvTemplate) {
    this.selectedTemplate.set(tpl);
  }

  async generatePreview() {
    const tpl = this.selectedTemplate();
    if (!tpl) return;
    this.loadingTemplates.set(true);
    try {
      const profile = this.profileService.profile();
      const payload = {
        templateSlug: tpl.slug,
        profileData: {
          fullName: `${profile.personal.firstName} ${profile.personal.lastName}`,
          email: profile.personal.email,
          phone: profile.personal.phone,
          location: profile.personal.city,
          linkedIn: profile.personal.linkedinUrl,
          gitHub: profile.personal.githubUrl,
          summary: profile.resume,
          skills: profile.skills.map(s => ({ name: s.name, level: 3, category: s.category })),
          experience: profile.experience.map(e => ({
            role: e.title, company: e.company, startDate: e.startDate, endDate: e.endDate || '', description: e.description
          })),
          education: profile.education.map(e => ({
            degree: e.degree, institution: e.institution, year: e.endYear || e.startYear
          })),
          projects: profile.projets.map(p => ({
            title: p.title, description: p.description, technologies: p.stack
          })),
          certifications: profile.certifications.map(c => ({
            name: c.name, issuer: c.issuer, date: c.date
          }))
        }
      };
      const result = await firstValueFrom(this.http.post<any>(`${this.baseUrl}/cv/preview?template=${tpl.slug}`, payload));
      this.previewData.set(result.data || result);
      this.activeTab.set('preview');
    } catch {
      this.previewData.set(mockPreviewData);
      this.activeTab.set('preview');
    } finally {
      this.loadingTemplates.set(false);
    }
  }

  async saveCv() {
    const tpl = this.selectedTemplate();
    if (!tpl || !this.previewData()) return;
    this.saving.set(true);
    try {
      await firstValueFrom(this.http.post(`${this.baseUrl}/cv/save`, {
        templateSlug: tpl.slug,
        title: `CV - ${tpl.name} - ${new Date().toLocaleDateString()}`,
        data: this.previewData()
      }));
    } catch (e) {
      console.error('Save error:', e);
    } finally {
      this.saving.set(false);
    }
  }

  async loadHistory() {
    this.loadingHistory.set(true);
    try {
      const data = await firstValueFrom(this.http.get<CvHistoryItem[]>(`${this.baseUrl}/cv/history`));
      this.historyItems.set(data);
    } catch {
      this.historyItems.set([]);
    } finally {
      this.loadingHistory.set(false);
    }
  }

  async downloadCv(id: string) {
    try {
      const blob = await firstValueFrom(this.http.get(`${this.baseUrl}/cv/${id}/download-file`, { responseType: 'blob' }));
      const url = window.URL.createObjectURL(blob);
      window.open(url, '_blank');
    } catch (e) {
      console.error('Download error:', e);
    }
  }

  async deleteCv(id: string) {
    try {
      await firstValueFrom(this.http.delete(`${this.baseUrl}/cv/${id}`));
      this.historyItems.update(items => items.filter(i => i.id !== id));
    } catch (e) {
      console.error('Delete error:', e);
    }
  }

  downloadLastCv() {
    const data = this.previewData();
    if (data) {
      const pdfUrl = this.historyItems()[0]?.fileUrl;
      if (pdfUrl) window.open(pdfUrl, '_blank');
    }
  }
}

const mockTemplates: CvTemplate[] = [
  { id: '1', slug: 'latex', name: 'LaTeX Tech', description: 'Design épuré et ultra-structuré, optimisé pour les systèmes ATS.', thumbnailUrl: '/api/cv/templates/latex/thumbnail', industries: ['IT', 'Tech', 'Engineering'], experienceLevels: ['Mid', 'Senior'], style: 'Traditional', layoutFlags: ['One Column', 'Without Photo'], backgroundColor: '#FFFFFF', tags: ['ATS', 'Classic'] },
  { id: '2', slug: 'modern', name: 'Modern', description: 'Format tech stylé avec séparations de colonnes nettes.', thumbnailUrl: '/api/cv/templates/modern/thumbnail', industries: ['IT', 'Tech'], experienceLevels: ['Mid', 'Senior'], style: 'Modern', layoutFlags: ['Two Column', 'With Photo'], backgroundColor: '#F5F5FF', tags: ['ATS', 'Minimal'] },
];

const mockPreviewData: CvData = {
  candidate: { name: 'Jean Dupont', email: 'jean.dupont@email.com', phone: '+212 6 00 00 00 00', location: 'Casablanca, Maroc', linkedIn: 'linkedin.com/in/jeandupont', gitHub: 'github.com/jeandupont', portfolio: '' },
  summary: 'Développeur full-stack passionné avec 3 ans d\'expérience dans la conception d\'applications web et mobiles. Expert en Angular, React, Node.js et architectures cloud.',
  experience: [
    { role: 'Développeur Full Stack', company: 'TechCorp', start: '2022-03', end: '', bullets: ['Développement d\'applications SaaS avec Angular et Node.js', 'Mise en place de pipelines CI/CD avec Docker et GitHub Actions', 'Optimisation des performances base de données PostgreSQL'] },
    { role: 'Stagiaire Développeur', company: 'StartupXYZ', start: '2021-06', end: '2021-09', bullets: ['Contribution au développement frontend avec React', 'Création d\'API REST avec Express.js', 'Tests unitaires avec Jest'] },
  ],
  education: [{ degree: 'Master en Informatique', institution: 'Université Hassan II', year: '2024' }],
  skills: [
    { name: 'Angular', level: 5, isMatched: true },
    { name: 'React', level: 4, isMatched: true },
    { name: 'Node.js', level: 4, isMatched: true },
    { name: 'PostgreSQL', level: 4, isMatched: true },
    { name: 'Docker', level: 3, isMatched: false },
    { name: 'TypeScript', level: 5, isMatched: true },
  ],
  projects: [{ title: 'E-commerce App', description: 'Plateforme e-commerce full-stack', bullets: ['Architecture microservices', 'Paiement Stripe intégré'] }],
  certifications: ['AWS Cloud Practitioner - Amazon'],
  languages: ['Français - Natif', 'Anglais - C1'],
  activities: [{ title: 'Club Robotique', role: 'Membre', description: 'Participation à des compétitions nationales' }],
  themeColor: '#0C1986',
  fontFamily: 'Lato',
  atsScore: 87,
  matchingScore: 82,
  atsCoveragePct: 78,
};
