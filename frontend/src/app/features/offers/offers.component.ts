import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';

export interface OfferCard {
  offerId: string;
  jobTitle: string;
  company: string;
  location: string;
  contractType: string;
  matchingScore: number;
  yearsExperience: number;
  requiredSkills: string[];
  optionalSkills: string[];
  dateAnalysed: string;
  knownQuestions: string[];
  glassdoorRating: number;
  salaryRange: string;
}

// ─── STATIC SEED DATA ────────────────────────────────────────────────────────
// Mirrors the exact seed we inserted in seed_offer_mode.sql
// When the backend /api/arena/my-offers is ready, replace this with a real call.
const SEED_OFFERS: OfferCard[] = [
  {
    offerId:          'b1e1e1e1-e1e1-4e1e-b1e1-e1e1e1e1e1e1',
    jobTitle:         'Senior Full-Stack Developer',
    company:          'Google',
    location:         'Mountain View, CA (Remote)',
    contractType:     'Full-time / CDI',
    matchingScore:    88,
    yearsExperience:  5,
    requiredSkills:   ['React', 'Node.js', 'PostgreSQL', 'TypeScript', 'JavaScript'],
    optionalSkills:   ['Docker', 'Kubernetes', 'Google Cloud Platform'],
    dateAnalysed:     new Date().toISOString(),
    knownQuestions: [
      'Explain the difference between a process and a thread.',
      'How would you design a URL shortener like bit.ly?',
      'What happens when you type a URL into your browser?',
    ],
    glassdoorRating:  4.5,
    salaryRange:      '$120,000 – $185,000 / yr',
  }
];

@Component({
  selector: 'app-offers',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="offers-shell">

      <!-- ── PAGE HEADER ── -->
      <header class="offers-header">
        <div class="header-left">
          <h1 class="page-title">Job Offers</h1>
          <p class="page-sub">AI-analyzed offers tailored to your profile — ready for interview prep.</p>
        </div>
        <span class="count-badge">{{ offers().length }} offer{{ offers().length !== 1 ? 's' : '' }}</span>
      </header>

      <!-- ── OFFER CARDS ── -->
      <div class="offers-list" *ngIf="offers().length > 0">
        <article class="offer-card" *ngFor="let offer of offers(); let i = index"
                 [style.animation-delay]="i * 90 + 'ms'">

          <!-- ── TOP ROW ── -->
          <div class="card-top">
            <div class="avatar">{{ initials(offer.company) }}</div>

            <div class="card-info">
              <h2 class="job-title">{{ offer.jobTitle }}</h2>
              <p class="company">{{ offer.company }}</p>
              <div class="meta-row">
                <span class="meta-chip" *ngIf="offer.location">
                  <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z"/><circle cx="12" cy="10" r="3"/></svg>
                  {{ offer.location }}
                </span>
                <span class="meta-chip" *ngIf="offer.contractType">
                  <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><rect x="2" y="7" width="20" height="14" rx="2"/><path d="M16 7V5a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v2"/></svg>
                  {{ offer.contractType }}
                </span>
                <span class="meta-chip" *ngIf="offer.yearsExperience">
                  <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>
                  {{ offer.yearsExperience }}+ yrs exp.
                </span>
              </div>
            </div>

            <!-- Match circle -->
            <div class="match-ring"
                 [class.match-hi]="offer.matchingScore >= 75"
                 [class.match-md]="offer.matchingScore >= 50 && offer.matchingScore < 75"
                 [class.match-lo]="offer.matchingScore < 50">
              <span class="ring-pct">{{ offer.matchingScore }}%</span>
              <span class="ring-lbl">Match</span>
            </div>
          </div>

          <!-- ── STATS ROW ── -->
          <div class="stats-row">
            <div class="stat-box">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2"/></svg>
              <div><span class="stat-val">{{ offer.glassdoorRating }}/5</span><span class="stat-key">Glassdoor</span></div>
            </div>
            <div class="stat-box">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="12" y1="1" x2="12" y2="23"/><path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"/></svg>
              <div><span class="stat-val">{{ offer.salaryRange }}</span><span class="stat-key">Salary est.</span></div>
            </div>
            <div class="stat-box">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/></svg>
              <div><span class="stat-val">{{ offer.knownQuestions.length }} known</span><span class="stat-key">Interview Q's</span></div>
            </div>
          </div>

          <!-- ── SKILLS ── -->
          <div class="skills-section">
            <p class="skills-label">Required Skills</p>
            <div class="skills-row">
              <span class="skill required" *ngFor="let s of offer.requiredSkills">{{ s }}</span>
            </div>
            <div class="skills-row" style="margin-top:8px" *ngIf="offer.optionalSkills.length">
              <span class="skill optional" *ngFor="let s of offer.optionalSkills">{{ s }}</span>
            </div>
          </div>

          <!-- ── KNOWN QUESTIONS PREVIEW ── -->
          <div class="known-q-section" *ngIf="offer.knownQuestions.length">
            <p class="skills-label">Real Interview Questions Spotted</p>
            <ul class="q-list">
              <li *ngFor="let q of offer.knownQuestions">{{ q }}</li>
            </ul>
          </div>

          <!-- ── FOOTER ── -->
          <div class="card-footer">
            <span class="analysed-date">
              Analyzed {{ formatDate(offer.dateAnalysed) }}
            </span>
            <button class="btn-prepare"
                    [id]="'btn-prepare-' + i"
                    (click)="prepareInterview(offer)">
              <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><polygon points="5 3 19 12 5 21 5 3"/></svg>
              Prepare for Interview
            </button>
          </div>

        </article>
      </div>

      <!-- ── EMPTY ── -->
      <div class="offers-empty" *ngIf="offers().length === 0">
        <div class="empty-icon">
          <svg width="40" height="40" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/></svg>
        </div>
        <h3>No analyzed offers yet</h3>
        <p>Submit a job offer to get AI-powered analysis and interview prep.</p>
      </div>

    </div>
  `,
  styles: [`
    :host {
      --blue:    #1A91F0;
      --blue-l:  #60B4F8;
      --gold:    #D97706;
      --green:   #16A34A;
      --red:     #DC2626;
      --surface: #FFFFFF;
      --bg:      #F8FAFF;
      --border:  #E2E8F0;
      --text:    #0F172A;
      --text-2:  #475569;
      --text-3:  #94A3B8;
      display: block;
    }

    /* ── Shell ── */
    .offers-shell {
      max-width: 840px;
      margin: 0 auto;
      padding: 40px 24px 80px;
      font-family: 'Lato', sans-serif;
    }

    /* ── Header ── */
    .offers-header {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: 16px;
      margin-bottom: 36px;
    }
    .page-title {
      font-size: 30px;
      font-weight: 800;
      margin: 0 0 6px;
      background: linear-gradient(120deg, var(--blue), var(--blue-l) 55%, var(--gold));
      -webkit-background-clip: text;
      -webkit-text-fill-color: transparent;
    }
    .page-sub { font-size: 14px; color: var(--text-3); margin: 0; }
    .count-badge {
      flex-shrink: 0;
      background: var(--bg);
      border: 1px solid var(--border);
      border-radius: 20px;
      padding: 6px 16px;
      font-size: 13px;
      font-weight: 700;
      color: var(--text-2);
    }

    /* ── List ── */
    .offers-list { display: flex; flex-direction: column; gap: 24px; }

    /* ── Card ── */
    .offer-card {
      background: var(--surface);
      border: 1.5px solid var(--border);
      border-radius: 20px;
      padding: 28px 28px 24px;
      box-shadow: 0 4px 20px rgba(15,23,42,0.05);
      animation: fadeUp 0.4s ease both;
      transition: transform .22s ease, box-shadow .22s ease, border-color .22s ease;
    }
    .offer-card:hover {
      transform: translateY(-3px);
      box-shadow: 0 14px 40px rgba(26,145,240,0.12);
      border-color: var(--blue-l);
    }
    @keyframes fadeUp {
      from { opacity:0; transform:translateY(14px); }
      to   { opacity:1; transform:translateY(0); }
    }

    /* ── Top row ── */
    .card-top {
      display: flex;
      align-items: flex-start;
      gap: 16px;
      margin-bottom: 20px;
    }
    .avatar {
      width: 50px; height: 50px;
      border-radius: 14px;
      background: linear-gradient(135deg, var(--blue), var(--blue-l));
      color: #fff;
      font-size: 17px;
      font-weight: 800;
      display: flex; align-items: center; justify-content: center;
      flex-shrink: 0;
      letter-spacing: -0.5px;
    }
    .card-info { flex: 1; min-width: 0; }
    .job-title {
      font-size: 18px; font-weight: 800; color: var(--text);
      margin: 0 0 3px;
      white-space: nowrap; overflow: hidden; text-overflow: ellipsis;
    }
    .company { font-size: 14px; font-weight: 600; color: var(--text-2); margin: 0 0 10px; }
    .meta-row { display: flex; flex-wrap: wrap; gap: 6px; }
    .meta-chip {
      display: inline-flex; align-items: center; gap: 5px;
      background: var(--bg); border: 1px solid var(--border);
      border-radius: 20px; padding: 3px 10px;
      font-size: 11.5px; font-weight: 600; color: var(--text-2);
      svg { color: var(--blue); flex-shrink: 0; }
    }

    /* ── Match ring ── */
    .match-ring {
      width: 62px; height: 62px;
      border-radius: 50%;
      border: 3.5px solid;
      display: flex; flex-direction: column;
      align-items: center; justify-content: center;
      flex-shrink: 0;
    }
    .match-hi { border-color: var(--green); background: #F0FDF4; }
    .match-md { border-color: var(--gold);  background: #FFFBEB; }
    .match-lo { border-color: var(--red);   background: #FEF2F2; }
    .ring-pct {
      font-size: 15px; font-weight: 800; line-height: 1;
    }
    .match-hi .ring-pct { color: var(--green); }
    .match-md .ring-pct { color: var(--gold); }
    .match-lo .ring-pct { color: var(--red); }
    .ring-lbl { font-size: 9px; font-weight: 700; text-transform: uppercase; letter-spacing: .07em; color: var(--text-3); margin-top: 2px; }

    /* ── Stats row ── */
    .stats-row {
      display: flex;
      gap: 12px;
      margin-bottom: 20px;
    }
    .stat-box {
      flex: 1;
      background: var(--bg);
      border: 1px solid var(--border);
      border-radius: 12px;
      padding: 10px 14px;
      display: flex;
      align-items: center;
      gap: 10px;
      svg { color: var(--blue); flex-shrink: 0; }
    }
    .stat-box div { display: flex; flex-direction: column; min-width: 0; }
    .stat-val { font-size: 13px; font-weight: 800; color: var(--text); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .stat-key { font-size: 10px; font-weight: 600; color: var(--text-3); text-transform: uppercase; letter-spacing: .06em; }

    /* ── Skills ── */
    .skills-section { margin-bottom: 20px; }
    .skills-label { font-size: 11.5px; font-weight: 700; color: var(--text-3); text-transform: uppercase; letter-spacing: .07em; margin: 0 0 8px; }
    .skills-row { display: flex; flex-wrap: wrap; gap: 7px; }
    .skill {
      border-radius: 8px; padding: 4px 10px;
      font-size: 12px; font-weight: 700;
    }
    .skill.required  { background: #EFF6FF; color: #1D4ED8; border: 1px solid #BFDBFE; }
    .skill.optional  { background: #F0FDF4; color: #15803D; border: 1px solid #BBF7D0; }

    /* ── Known Q ── */
    .known-q-section { margin-bottom: 20px; }
    .q-list {
      list-style: none; margin: 0; padding: 0;
      display: flex; flex-direction: column; gap: 8px;
    }
    .q-list li {
      font-size: 13px; color: var(--text-2);
      padding-left: 18px; position: relative; line-height: 1.5;
    }
    .q-list li::before {
      content: '→';
      position: absolute; left: 0;
      color: var(--blue); font-weight: 800;
    }

    /* ── Footer ── */
    .card-footer {
      display: flex;
      align-items: center;
      justify-content: space-between;
      border-top: 1px solid var(--border);
      padding-top: 18px;
    }
    .analysed-date { font-size: 12px; color: var(--text-3); font-weight: 500; }

    /* ── Prepare button ── */
    .btn-prepare {
      display: inline-flex;
      align-items: center;
      gap: 9px;
      background: var(--blue);
      color: #fff;
      border: none;
      border-radius: 12px;
      padding: 12px 22px;
      font-size: 14px;
      font-weight: 700;
      font-family: 'Lato', sans-serif;
      cursor: pointer;
      letter-spacing: 0.01em;
      transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1);
    }
    .btn-prepare:hover {
      background: #0D7DD4;
      transform: translateY(-2px);
      box-shadow: 0 8px 28px rgba(26, 145, 240, 0.35);
    }
    .btn-prepare:active { transform: translateY(0); }

    /* ── Empty ── */
    .offers-empty {
      text-align: center;
      padding: 80px 32px;
      background: var(--bg);
      border: 1.5px dashed var(--border);
      border-radius: 20px;
    }
    .empty-icon {
      width: 72px; height: 72px;
      background: linear-gradient(135deg, #EFF6FF, #DBEAFE);
      border-radius: 50%;
      display: flex; align-items: center; justify-content: center;
      margin: 0 auto 18px;
      svg { color: var(--blue); }
    }
    .offers-empty h3 { font-size: 18px; font-weight: 800; color: var(--text); margin: 0 0 8px; }
    .offers-empty p  { font-size: 14px; color: var(--text-3); margin: 0; }
  `]
})
export class OffersComponent implements OnInit {
  offers = signal<OfferCard[]>([]);

  constructor(private router: Router) {}

  ngOnInit() {
    // Load the static seed data immediately — guaranteed to show the card
    this.offers.set(SEED_OFFERS);
  }

  prepareInterview(offer: OfferCard) {
    this.router.navigate(['/chatbot'], {
      state: {
        preselectedMode: 'offer',
        offerConfig: {
          offer_id:         offer.offerId,
          domain:           'software',
          level:            'senior',
          duration_minutes: 20,
          language:         'en',
          focus_areas:      offer.requiredSkills.slice(0, 3),
          job_title:        offer.jobTitle,
          company:          offer.company,
        }
      }
    });
  }

  initials(company: string): string {
    return company.split(' ').map(w => w[0] ?? '').join('').substring(0, 2).toUpperCase();
  }

  formatDate(dateStr: string): string {
    const d = new Date(dateStr);
    return isNaN(d.getTime())
      ? 'recently'
      : d.toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' });
  }
}
