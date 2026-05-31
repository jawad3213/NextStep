import { Component, OnInit, signal, computed, ViewChild, ElementRef, AfterViewChecked } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ArenaService } from '../../services/arena.service';
import { ArenaConfig, SalaryResult, ChatMessage } from '../../models/arena.models';

@Component({
  selector: 'app-salary-coach',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './salary-coach.component.html',
  styleUrls: ['./salary-coach.component.scss'],
})
export class SalaryCoachComponent implements OnInit, AfterViewChecked {
  @ViewChild('chatScroll') chatScroll!: ElementRef;

  config!: ArenaConfig;

  salary = signal<SalaryResult | null>(null);
  loading = signal(true);
  activeStep = signal(0);

  phase = signal<'market' | 'coach'>('market');
  messages = signal<ChatMessage[]>([]);
  userInput = '';
  isTyping = signal(false);
  threadId = crypto.randomUUID();

  scenarioIdx = signal(0);
  scenarioAnswer = '';
  showFeedback = signal(false);
  scenarioFeedback = signal('');
  scenarioLoading = signal(false);

  private needsScroll = false;

  // ── Bilingual Data ──────────────────────────────────────────

  private readonly CONTENT = {
    fr: {
      qrLabels: ["Anchor basé marché", "Négocier avantages", "Concurrence", "Gérer le silence"],
      qrTexts: [
        "Sur la base du marché, je vise {{target}} {{currency}}. Ma stack le justifie.",
        "Je comprends. Peut-on explorer le télétravail ou un bonus ?",
        "J'ai une autre offre similaire. Pouvez-vous vous aligner ?",
        "..."
      ],
      scenarios: [
        {
          q: 'Le recruteur demande : "Quelles sont vos prétentions salariales ?"',
          hint: 'Ancrez haut avec une fourchette justifiée.',
          phrase: '"Sur la base du marché, je vise 26 000 MAD. C’est cohérent avec Glassdoor."',
          why: 'Ancrer haut vous laisse de la marge pour descendre.'
        },
        {
          q: "L'offre est 10% sous votre cible. Que dites-vous ?",
          hint: 'Contre-offrez avec des données concrètes.',
          phrase: '"Ma maîtrise de la stack technique justifie un salaire supérieur."',
          why: 'Relier vos compétences aux besoins justifie le prix.'
        },
        {
          q: '"Notre budget est plafonné." Comment réagissez-vous ?',
          hint: 'Négociez le package (télétravail, bonus).',
          phrase: '"Peut-on explorer le télétravail ou une révision à 6 mois ?"',
          why: 'Les avantages non-monétaires ont une vraie valeur.'
        }
      ],
      techs: [
        { name: "Anchor Effect", desc: "Nommez votre chiffre en premier pour ancrer la négo." },
        { name: "BATNA", desc: "Ayez toujours une alternative pour avoir du pouvoir." },
        { name: "Silence", desc: "Taisez-vous après votre demande pour mettre la pression." }
      ]
    },
    en: {
      qrLabels: ["Market Anchor", "Negotiate Benefits", "Competition", "Handle Silence"],
      qrTexts: [
        "Based on the market, I'm aiming for {{target}} {{currency}}.",
        "I understand. Can we explore remote work or a bonus?",
        "I have another similar offer. Can you match it?",
        "..."
      ],
      scenarios: [
        {
          q: 'The recruiter asks: "What are your salary expectations?"',
          hint: 'Anchor high with a justified range.',
          phrase: '"Based on the market, I\'m aiming for 26,000 MAD."',
          why: 'Anchoring high gives you room to negotiate down.'
        },
        {
          q: "The offer is 10% below your target. What do you say?",
          hint: 'Counter-offer with concrete data.',
          phrase: '"My proficiency in the tech stack justifies a higher salary."',
          why: 'Linking skills to needs justifies the price.'
        },
        {
          q: '"Our budget is capped." How do you react?',
          hint: 'Negotiate the package (remote, bonus).',
          phrase: '"Can we explore remote work or a 6-month review?"',
          why: 'Non-monetary benefits have real value.'
        }
      ],
      techs: [
        { name: "Anchor Effect", desc: "Name your figure first to anchor the negotiation." },
        { name: "BATNA", desc: "Always have an alternative to gain power." },
        { name: "Silence", desc: "Keep quiet after your request to apply pressure." }
      ]
    }
  };

  techList() {
    const lang = this.config?.language === 'fr' ? 'fr' : 'en';
    const icons = [
      '<svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"></circle><line x1="22" y1="12" x2="18" y2="12"></line><line x1="6" y1="12" x2="2" y2="12"></line><line x1="12" y1="6" x2="12" y2="2"></line><line x1="12" y1="22" x2="12" y2="18"></line></svg>',
      '<svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="2" y="7" width="20" height="14" rx="2" ry="2"></rect><path d="M16 21V5a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v16"></path></svg>',
      '<svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="1" y1="1" x2="23" y2="23"></line><path d="M9 9v3a3 3 0 0 0 5.12 2.12M15 9.34V4a3 3 0 0 0-5.94-.6"></path><path d="M17 16.95A7 7 0 0 1 5 12v-2m14 0v2a7 7 0 0 1-.11 1.23"></path><line x1="12" y1="19" x2="12" y2="23"></line><line x1="8" y1="23" x2="16" y2="23"></line></svg>'
    ];
    return this.CONTENT[lang].techs.map((t, i) => ({ ...t, icon: icons[i] }));
  }

  getScenario(i: number) {
    const lang = this.config?.language === 'fr' ? 'fr' : 'en';
    return this.CONTENT[lang].scenarios[i] || this.CONTENT[lang].scenarios[0];
  }

  get qrLabels() {
    const lang = this.config?.language === 'fr' ? 'fr' : 'en';
    return this.CONTENT[lang].qrLabels;
  }

  get scenarios() {
    const lang = this.config?.language === 'fr' ? 'fr' : 'en';
    return this.CONTENT[lang].scenarios;
  }

  currentScenario = computed(() => this.scenarios[this.scenarioIdx()] || this.scenarios[0]);

  getQR(i: number): string {
    const lang = this.config?.language === 'fr' ? 'fr' : 'en';
    const s = this.salary();
    return this.CONTENT[lang].qrTexts[i]
      .replace('{{target}}', s ? String(s.your_target) : '24 000')
      .replace('{{currency}}', s ? s.currency : 'MAD');
  }

  constructor(private svc: ArenaService, private router: Router) { }

  ngOnInit() {
    const state = history.state as { arenaConfig?: ArenaConfig };
    if (!state?.arenaConfig) { this.router.navigate(['/chatbot/arena']); return; }
    this.config = state.arenaConfig;
    this.loading.set(false);
    this.svc.getSalary(this.config).subscribe({
      next: r => { this.salary.set(r); },
      error: () => { },
    });
  }

  ngAfterViewChecked() {
    if (this.needsScroll) {
      try { const el = this.chatScroll?.nativeElement; if (el) el.scrollTop = el.scrollHeight; } catch { }
      this.needsScroll = false;
    }
  }

  get median(): number {
    const s = this.salary();
    return s ? Math.round((s.range_min + s.range_max) / 2) : 0;
  }

  get isInternship(): boolean {
    const jobTitleLower = (this.config?.job_title || '').toLowerCase();
    const domainLower = (this.config?.domain || '').toLowerCase();
    return jobTitleLower.includes('stage') ||
      jobTitleLower.includes('pfe') ||
      jobTitleLower.includes('pfa') ||
      jobTitleLower.includes('intern') ||
      jobTitleLower.includes('stagiaire') ||
      domainLower.includes('stage') ||
      domainLower.includes('pfe') ||
      domainLower.includes('pfa') ||
      domainLower.includes('intern') ||
      domainLower.includes('stagiaire');
  }

  thumbPct(): number {
    const s = this.salary();
    if (!s) return 52;
    const min = s.range_min * 0.6;
    const max = s.range_max * 1.35;
    return Math.min(95, Math.max(5, ((s.your_target - min) / (max - min)) * 100));
  }

  setStep(i: number) { this.activeStep.set(i); }

  startCoach() {
    this.phase.set('coach');
    const salVal = this.salary();
    const isInternship = this.isInternship;
    const lang = this.config?.language === 'fr' ? 'fr' : 'en';
    const sc = this.CONTENT[lang].scenarios[0];

    // Determine the range text
    let rangeText = '';
    const hasRange = salVal && (salVal.range_min > 0 || salVal.range_max > 0);
    if (hasRange) {
      rangeText = `${salVal!.range_min.toLocaleString()} – ${salVal!.range_max.toLocaleString()} ${salVal!.currency}`;
    }

    let msg = '';
    if (isInternship) {
      if (lang === 'fr') {
        msg = `Bonjour ! 🎓 Je suis votre Coach de Négociation IA.\n\n`;
        if (hasRange) {
          msg += `Le salaire professionnel typique pour ce métier est estimé à **${rangeText}**. `;
        }
        msg += `Néanmoins, comme il s'agit d'une opportunité de stage, votre objectif principal aujourd'hui n'est pas de négocier ce salaire immédiatement. Vous êtes là avant tout pour apprendre, acquérir de l'expérience pratique et prouver votre valeur afin de négocier stratégiquement une embauche future (Return Offer en CDI/CDD) à la fin de votre stage.\n\nMise en situation : Le recruteur vous demande :\n"${sc.q}"\n\nQue répondez-vous pour exprimer votre motivation d'apprentissage tout en gardant une vision d'embauche future ?`;
      } else {
        msg = `Hello! 🎓 I am your AI Negotiation Coach.\n\n`;
        if (hasRange) {
          msg += `The typical professional salary for this role is estimated at **${rangeText}**. `;
        }
        msg += `However, since this is an internship opportunity, our primary focus today is not immediate salary negotiation. You are here to learn, gain real-world experience, and demonstrate your value in order to strategically secure a full-time return offer (CDI/CDD) at the end of your internship.\n\nSimulation: The recruiter asks:\n"${sc.q}"\n\nWhat do you say to show your drive to learn while positioning yourself for a future full-time role?`;
      }
    } else {
      // Job offer or arena
      if (lang === 'fr') {
        msg = `Bonjour ! 💼 Je suis votre Coach de Négociation IA.\n\n`;
        if (hasRange) {
          msg += `Votre objectif aujourd'hui est d'analyser et de cibler avec précision votre fourchette idéale de **${rangeText}** pour maximiser votre package global.\n\n`;
        } else {
          msg += `Nous n'avons pas trouvé de fourchette de salaire exacte pour ce poste ou sur Internet. Cependant, nous allons travailler ensemble sur vos techniques de négociation pour viser une rémunération compétitive sur le marché.\n\n`;
        }
        msg += `Mise en situation : Le recruteur vous pose la question cruciale :\n"${sc.q}"\n\nComment allez-vous argumenter pour ancrer les discussions au plus haut ?`;
      } else {
        msg = `Hello! 💼 I am your AI Negotiation Coach.\n\n`;
        if (hasRange) {
          msg += `Our objective today is to target the ideal range of **${rangeText}** to maximize your global compensation package.\n\n`;
        } else {
          msg += `We could not find an exact salary range for this position on the internet or in our database. However, we will work together on your negotiation strategies to target a competitive market rate.\n\n`;
        }
        msg += `Simulation: The recruiter asks the crucial question:\n"${sc.q}"\n\nHow will you frame your response to anchor the discussion at the highest level?`;
      }
    }

    this.messages.set([{
      role: 'ai',
      content: msg,
      timestamp: new Date(),
    }]);
  }

  send() {
    const v = this.userInput.trim();
    if (!v || this.isTyping()) return;
    this.messages.update(m => [...m, { role: 'user', content: v, timestamp: new Date() }]);
    this.userInput = '';
    this.isTyping.set(true);
    this.needsScroll = true;
    const s = this.salary();
    const ctx = s
      ? { rangeMin: s.range_min, rangeMax: s.range_max, currency: s.currency, yourTarget: s.your_target }
      : { rangeMin: 0, rangeMax: 0, currency: 'MAD', yourTarget: 0 };
    const langName = this.config?.language === 'fr' ? 'French' : 'English';
    const msgWithLang = `[MANDATORY: Respond in ${langName}] ${v}`;

    this.svc.salaryCoach(msgWithLang, this.threadId, ctx, this.messages().map(m => ({ role: m.role, content: m.content })), this.config!).subscribe({
      next: r => {
        this.messages.update(m => [...m, { role: 'ai', content: r.response, timestamp: new Date() }]);
        this.isTyping.set(false);
        this.needsScroll = true;
      },
      error: () => {
        const errorMsg = this.config?.language === 'fr'
          ? "Je suis momentanément indisponible. Consultez les scripts dans l'onglet Données marché."
          : "I am temporarily unavailable. Please check the scripts in the Market Data tab.";
        this.messages.update(m => [...m, { role: 'ai', content: errorMsg, timestamp: new Date() }]);
        this.isTyping.set(false);
      },
    });
  }

  onKey(e: KeyboardEvent) { if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); this.send(); } }
  quickSend(text: string) { this.userInput = text; this.send(); }

  submitScenario() {
    if (!this.scenarioAnswer.trim() || this.scenarioLoading()) return;
    this.scenarioLoading.set(true);
    const s = this.salary();
    const ctx = s
      ? { rangeMin: s.range_min, rangeMax: s.range_max, currency: s.currency, yourTarget: s.your_target }
      : { rangeMin: 0, rangeMax: 0, currency: 'MAD', yourTarget: 0 };
    const lang = this.config?.language === 'fr' ? 'fr' : 'en';
    const langName = lang === 'fr' ? 'French' : 'English';
    const prompt = `[MANDATORY: Respond in ${langName}] Scenario: "${this.currentScenario().q}" — My response: "${this.scenarioAnswer}" — Short feedback in 2-3 sentences.`;

    this.svc.salaryCoach(prompt, this.threadId + '-q', ctx, [], this.config!).subscribe({
      next: r => { this.scenarioFeedback.set(r.response); this.showFeedback.set(true); this.scenarioLoading.set(false); },
      error: () => {
        const fallback = lang === 'fr'
          ? 'Bonne tentative ! Pensez à toujours ancrer avec des données marché et à maintenir le silence.'
          : 'Good attempt! Always remember to anchor with market data and maintain silence.';
        this.scenarioFeedback.set(fallback);
        this.showFeedback.set(true);
        this.scenarioLoading.set(false);
      },
    });
  }

  nextScenario() {
    this.scenarioIdx.set((this.scenarioIdx() + 1) % this.scenarios.length);
    this.scenarioAnswer = '';
    this.showFeedback.set(false);
    this.scenarioFeedback.set('');
  }

  back() { this.router.navigate(['/chatbot/arena'], { state: { arenaConfig: this.config } }); }
  levelLabel() { return { junior: 'Junior', mid: 'Mid-level', senior: 'Senior' }[this.config?.level] ?? ''; }
}
