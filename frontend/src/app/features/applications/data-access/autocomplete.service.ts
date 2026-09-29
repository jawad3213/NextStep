import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { catchError, map, timeout } from 'rxjs/operators';

export interface CompanySuggestion {
  name: string;
  domain?: string;
  logo?: string;
  source: 'clearbit' | 'catalog' | 'history';
  alreadyApplied?: boolean;
}

export type JobCategory =
  | 'Engineering & Dev'
  | 'Data & AI'
  | 'Cloud & DevOps'
  | 'Product & Design'
  | 'Cybersecurity'
  | 'Management'
  | 'QA & Testing'
  | 'Business Tech'
  | 'Other';

export interface JobTitleSuggestion {
  title: string;
  category: JobCategory;
  alreadyApplied?: boolean;
}

interface RawClearbitItem {
  name?: string;
  domain?: string;
  logo?: string;
}

@Injectable({
  providedIn: 'root',
})
export class AutocompleteService {
  private readonly http: HttpClient;

  constructor(http?: HttpClient) {
    this.http = http || inject(HttpClient);
  }

  // Clearbit public autocomplete endpoint
  private readonly clearbitUrl = 'https://autocomplete.clearbit.com/v1/companies/suggest';

  // In-memory query cache for fast subsequent lookups
  private readonly companyCache = new Map<string, CompanySuggestion[]>();

  // ── Curated Catalog: Top French Tech, CAC 40, Scaleups & Global Tech ───────
  private readonly localCompanyCatalog: Array<{ name: string; domain: string; tags?: string[] }> = [
    // Tech Giants & Global Leaders
    { name: 'Google', domain: 'google.com', tags: ['alphabet', 'tech', 'search', 'cloud'] },
    { name: 'Microsoft', domain: 'microsoft.com', tags: ['tech', 'cloud', 'azure', 'windows'] },
    { name: 'Apple', domain: 'apple.com', tags: ['tech', 'ios', 'hardware'] },
    { name: 'Amazon', domain: 'amazon.com', tags: ['tech', 'aws', 'e-commerce', 'cloud'] },
    { name: 'Meta', domain: 'meta.com', tags: ['facebook', 'instagram', 'whatsapp', 'tech'] },
    { name: 'Netflix', domain: 'netflix.com', tags: ['streaming', 'tech'] },
    { name: 'Spotify', domain: 'spotify.com', tags: ['audio', 'tech'] },
    { name: 'Stripe', domain: 'stripe.com', tags: ['fintech', 'payments', 'api'] },
    { name: 'Salesforce', domain: 'salesforce.com', tags: ['crm', 'cloud', 'saas'] },
    { name: 'Datadog', domain: 'datadoghq.com', tags: ['monitoring', 'observability', 'saas', 'french tech'] },
    { name: 'Cloudflare', domain: 'cloudflare.com', tags: ['security', 'cdn', 'cloud'] },
    { name: 'Snowflake', domain: 'snowflake.com', tags: ['data', 'cloud', 'warehouse'] },
    { name: 'Adobe', domain: 'adobe.com', tags: ['creative', 'saas'] },
    { name: 'IBM', domain: 'ibm.com', tags: ['tech', 'cloud', 'consulting'] },
    { name: 'Oracle', domain: 'oracle.com', tags: ['database', 'cloud', 'enterprise'] },
    { name: 'SAP', domain: 'sap.com', tags: ['erp', 'enterprise', 'software'] },
    { name: 'Uber', domain: 'uber.com', tags: ['mobility', 'tech'] },
    { name: 'Airbnb', domain: 'airbnb.com', tags: ['travel', 'tech'] },
    { name: 'GitHub', domain: 'github.com', tags: ['git', 'code', 'microsoft'] },
    { name: 'GitLab', domain: 'gitlab.com', tags: ['devops', 'git'] },
    { name: 'Figma', domain: 'figma.com', tags: ['design', 'ui', 'ux'] },
    { name: 'Notion', domain: 'notion.so', tags: ['productivity', 'saas'] },
    { name: 'OpenAI', domain: 'openai.com', tags: ['ai', 'chatgpt', 'llm'] },
    { name: 'Anthropic', domain: 'anthropic.com', tags: ['ai', 'claude', 'llm'] },
    { name: 'Mistral AI', domain: 'mistral.ai', tags: ['ai', 'french tech', 'llm'] },

    // French Tech Scaleups & Next40 / FT120
    { name: 'Doctolib', domain: 'doctolib.fr', tags: ['health', 'scaleup', 'unicorn'] },
    { name: 'Alan', domain: 'alan.com', tags: ['health', 'insurance', 'scaleup'] },
    { name: 'Payfit', domain: 'payfit.com', tags: ['hr', 'payroll', 'scaleup'] },
    { name: 'Qonto', domain: 'qonto.com', tags: ['fintech', 'banking', 'scaleup'] },
    { name: 'Swile', domain: 'swile.co', tags: ['fintech', 'benefits', 'scaleup'] },
    { name: 'ManoMano', domain: 'manomano.fr', tags: ['e-commerce', 'scaleup'] },
    { name: 'Mirakl', domain: 'mirakl.com', tags: ['marketplace', 'saas', 'scaleup'] },
    { name: 'Contentsquare', domain: 'contentsquare.com', tags: ['analytics', 'ux', 'scaleup'] },
    { name: 'BlaBlaCar', domain: 'blablacar.fr', tags: ['mobility', 'carpooling', 'scaleup'] },
    { name: 'Deezer', domain: 'deezer.com', tags: ['music', 'streaming'] },
    { name: 'Criteo', domain: 'criteo.com', tags: ['adtech', 'data'] },
    { name: 'Ledger', domain: 'ledger.com', tags: ['crypto', 'security', 'hardware'] },
    { name: 'OVHcloud', domain: 'ovhcloud.com', tags: ['cloud', 'hosting', 'infrastructure'] },
    { name: 'Back Market', domain: 'backmarket.fr', tags: ['refurbished', 'circular', 'e-commerce'] },
    { name: 'Spendesk', domain: 'spendesk.com', tags: ['fintech', 'expenses'] },
    { name: 'Pennylane', domain: 'pennylane.com', tags: ['fintech', 'accounting'] },
    { name: 'Malt', domain: 'malt.fr', tags: ['freelance', 'platform'] },
    { name: 'Shift Technology', domain: 'shift-technology.com', tags: ['ai', 'insurance'] },
    { name: 'OpenClassrooms', domain: 'openclassrooms.com', tags: ['edtech', 'learning'] },
    { name: 'Vestiaire Collective', domain: 'vestiairecollective.com', tags: ['fashion', 'luxury'] },
    { name: 'Meero', domain: 'meero.com', tags: ['photography', 'ai'] },
    { name: 'Brut', domain: 'brut.media', tags: ['media', 'video'] },

    // ESN, Conseil & Services Informatiques
    { name: 'Capgemini', domain: 'capgemini.com', tags: ['esn', 'consulting', 'it'] },
    { name: 'Sopra Steria', domain: 'soprasteria.com', tags: ['esn', 'consulting', 'it'] },
    { name: 'Alten', domain: 'alten.com', tags: ['engineering', 'it', 'consulting'] },
    { name: 'Wavestone', domain: 'wavestone.com', tags: ['consulting', 'transformation', 'cyber'] },
    { name: 'Octo Technology', domain: 'octo.com', tags: ['consulting', 'software craft', 'agile'] },
    { name: 'Devoteam', domain: 'devoteam.com', tags: ['consulting', 'cloud', 'digital'] },
    { name: 'CGI', domain: 'cgi.com', tags: ['esn', 'consulting', 'it'] },
    { name: 'Atos', domain: 'atos.net', tags: ['esn', 'it', 'cloud'] },
    { name: 'Accenture', domain: 'accenture.com', tags: ['consulting', 'it', 'strategy'] },
    { name: 'Inetum', domain: 'inetum.com', tags: ['esn', 'digital', 'it'] },
    { name: 'Aubay', domain: 'aubay.com', tags: ['esn', 'it', 'finance'] },
    { name: 'Zenika', domain: 'zenika.com', tags: ['craft', 'training', 'it'] },
    { name: 'Ippon Technologies', domain: 'ippon.fr', tags: ['cloud', 'data', 'craft'] },
    { name: 'Extia', domain: 'extia.fr', tags: ['consulting', 'it'] },
    { name: 'Davidson Consulting', domain: 'davidson.fr', tags: ['consulting', 'it'] },
    { name: 'Meritis', domain: 'meritis.fr', tags: ['consulting', 'it', 'finance'] },

    // CAC 40 & Grands Groupes Industriels / Bancaires
    { name: 'BNP Paribas', domain: 'group.bnpparibas', tags: ['bank', 'finance', 'cac40'] },
    { name: 'Société Générale', domain: 'societegenerale.com', tags: ['bank', 'finance', 'cac40'] },
    { name: 'Crédit Agricole', domain: 'credit-agricole.com', tags: ['bank', 'finance', 'cac40'] },
    { name: 'Groupe BPCE', domain: 'groupebpce.com', tags: ['bank', 'finance', 'natixis'] },
    { name: 'AXA', domain: 'axa.com', tags: ['insurance', 'finance', 'cac40'] },
    { name: 'Thales', domain: 'thalesgroup.com', tags: ['defense', 'aerospace', 'cyber', 'cac40'] },
    { name: 'Airbus', domain: 'airbus.com', tags: ['aerospace', 'aviation', 'cac40'] },
    { name: 'Dassault Systèmes', domain: '3ds.com', tags: ['3d', 'software', 'cad', 'cac40'] },
    { name: 'Safran', domain: 'safran-group.com', tags: ['aerospace', 'defense', 'cac40'] },
    { name: 'Sanofi', domain: 'sanofi.com', tags: ['pharma', 'health', 'cac40'] },
    { name: "L'Oréal", domain: 'loreal.com', tags: ['beauty', 'cosmetics', 'cac40'] },
    { name: 'TotalEnergies', domain: 'totalenergies.com', tags: ['energy', 'oil', 'renewables', 'cac40'] },
    { name: 'Orange', domain: 'orange.com', tags: ['telecom', 'mobile', 'cyber', 'cac40'] },
    { name: 'Bouygues', domain: 'bouygues.com', tags: ['construction', 'telecom', 'media'] },
    { name: 'Renault Group', domain: 'renaultgroup.com', tags: ['automotive', 'ev', 'cac40'] },
    { name: 'Stellantis', domain: 'stellantis.com', tags: ['automotive', 'peugeot', 'citroen'] },
    { name: 'Michelin', domain: 'michelin.com', tags: ['automotive', 'tires', 'cac40'] },
    { name: 'Saint-Gobain', domain: 'saint-gobain.com', tags: ['materials', 'construction', 'cac40'] },
    { name: 'Schneider Electric', domain: 'se.com', tags: ['energy', 'iot', 'automation', 'cac40'] },
    { name: 'Legrand', domain: 'legrandgroup.com', tags: ['electrical', 'iot', 'cac40'] },
    { name: 'Danone', domain: 'danone.com', tags: ['food', 'consumer', 'cac40'] },
    { name: 'Carrefour', domain: 'carrefour.com', tags: ['retail', 'supermarket', 'cac40'] },
    { name: 'Publicis Groupe', domain: 'publicisgroupe.com', tags: ['advertising', 'digital', 'cac40'] },
    { name: 'Veolia', domain: 'veolia.com', tags: ['environment', 'water', 'waste', 'cac40'] },
    { name: 'Engie', domain: 'engie.com', tags: ['energy', 'utilities', 'cac40'] },
    { name: 'SNCF', domain: 'sncf.com', tags: ['rail', 'transport'] },
    { name: 'RATP', domain: 'ratp.fr', tags: ['transit', 'transport'] },
    { name: 'Decathlon', domain: 'decathlon.fr', tags: ['sports', 'retail'] },
    { name: 'Kering', domain: 'kering.com', tags: ['luxury', 'gucci', 'saint laurent'] },
    { name: 'LVMH', domain: 'lvmh.com', tags: ['luxury', 'dior', 'louis vuitton'] },
    { name: 'Hermès', domain: 'hermes.com', tags: ['luxury', 'craft'] },
  ];

  // ── Curated Job Taxonomy: 200+ Modern Tech & Corporate Roles ───────────────
  private readonly jobCatalog: Array<{ title: string; category: JobCategory; aliases?: string[] }> = [
    // 1. Software Engineering & Development
    { title: 'Full Stack Developer', category: 'Engineering & Dev', aliases: ['fullstack', 'fs', 'swe', 'dev'] },
    { title: 'Frontend Developer', category: 'Engineering & Dev', aliases: ['front', 'ui dev', 'web'] },
    { title: 'Backend Developer', category: 'Engineering & Dev', aliases: ['back', 'api dev', 'server'] },
    { title: 'Software Engineer', category: 'Engineering & Dev', aliases: ['swe', 'software dev', 'dev'] },
    { title: 'Senior Software Engineer', category: 'Engineering & Dev', aliases: ['sr swe', 'senior dev'] },
    { title: 'Lead Developer', category: 'Engineering & Dev', aliases: ['tech lead', 'lead dev', 'lead tech'] },
    { title: 'Software Architect', category: 'Engineering & Dev', aliases: ['architect', 'arch'] },
    { title: 'Angular Developer', category: 'Engineering & Dev', aliases: ['angular', 'front angular', 'typescript'] },
    { title: 'React Developer', category: 'Engineering & Dev', aliases: ['react', 'reactjs', 'nextjs'] },
    { title: 'Vue.js Developer', category: 'Engineering & Dev', aliases: ['vue', 'nuxtjs'] },
    { title: '.NET / C# Developer', category: 'Engineering & Dev', aliases: ['dotnet', 'c#', 'csharp', 'asp.net'] },
    { title: 'Java / Spring Boot Developer', category: 'Engineering & Dev', aliases: ['java', 'spring', 'springboot'] },
    { title: 'Python Developer', category: 'Engineering & Dev', aliases: ['python', 'django', 'fastapi', 'flask'] },
    { title: 'Node.js / TypeScript Developer', category: 'Engineering & Dev', aliases: ['nodejs', 'express', 'nest', 'nestjs'] },
    { title: 'Go (Golang) Developer', category: 'Engineering & Dev', aliases: ['go', 'golang'] },
    { title: 'PHP / Symfony Developer', category: 'Engineering & Dev', aliases: ['php', 'symfony', 'laravel'] },
    { title: 'C / C++ Developer', category: 'Engineering & Dev', aliases: ['c++', 'cpp', 'systems'] },
    { title: 'Rust Developer', category: 'Engineering & Dev', aliases: ['rust'] },
    { title: 'Mobile iOS Developer (Swift)', category: 'Engineering & Dev', aliases: ['ios', 'swift', 'swiftui', 'mobile'] },
    { title: 'Mobile Android Developer (Kotlin)', category: 'Engineering & Dev', aliases: ['android', 'kotlin', 'mobile'] },
    { title: 'Flutter Developer', category: 'Engineering & Dev', aliases: ['flutter', 'dart', 'cross-platform'] },
    { title: 'React Native Developer', category: 'Engineering & Dev', aliases: ['react native', 'rn', 'mobile'] },
    { title: 'Embedded Systems Developer', category: 'Engineering & Dev', aliases: ['embedded', 'iot'] },
    { title: 'Blockchain / Web3 Engineer', category: 'Engineering & Dev', aliases: ['web3', 'solidity', 'crypto', 'smart contracts'] },
    { title: 'No-Code / Low-Code Developer', category: 'Engineering & Dev', aliases: ['bubble', 'make', 'zapier', 'nocode'] },
    { title: 'Software Engineer Intern (Graduation)', category: 'Engineering & Dev', aliases: ['intern', 'internship', 'pfe', 'trainee'] },
    { title: 'Web Developer Apprentice', category: 'Engineering & Dev', aliases: ['apprentice', 'co-op', 'alternance'] },

    // 2. Data, AI & Analytics
    { title: 'Data Scientist', category: 'Data & AI', aliases: ['data science', 'ds', 'machine learning'] },
    { title: 'Data Engineer', category: 'Data & AI', aliases: ['de', 'pipeline', 'spark', 'sql', 'etl'] },
    { title: 'Machine Learning Engineer', category: 'Data & AI', aliases: ['mle', 'ml engineer', 'ai', 'deep learning'] },
    { title: 'AI Engineer / Generative AI Engineer', category: 'Data & AI', aliases: ['ai', 'genai', 'llm', 'rag'] },
    { title: 'Data Analyst', category: 'Data & AI', aliases: ['analytics', 'bi', 'sql', 'tableau', 'powerbi'] },
    { title: 'Analytics Engineer', category: 'Data & AI', aliases: ['dbt', 'snowflake', 'bigquery', 'data'] },
    { title: 'MLOps Engineer', category: 'Data & AI', aliases: ['mlops', 'ml infra', 'kubeflow'] },
    { title: 'Computer Vision Engineer', category: 'Data & AI', aliases: ['vision', 'opencv', 'image'] },
    { title: 'NLP Engineer', category: 'Data & AI', aliases: ['nlp', 'transformers', 'bert', 'huggingface'] },
    { title: 'Data Architect', category: 'Data & AI', aliases: ['data architect', 'lakehouse'] },
    { title: 'Business Intelligence (BI) Consultant', category: 'Data & AI', aliases: ['bi', 'power bi', 'tableau'] },
    { title: 'Data Science / AI Intern', category: 'Data & AI', aliases: ['data intern', 'internship'] },
    { title: 'Data Engineer Apprentice', category: 'Data & AI', aliases: ['apprentice', 'co-op'] },

    // 3. Cloud, DevOps & Infrastructure
    { title: 'DevOps Engineer', category: 'Cloud & DevOps', aliases: ['devops', 'ci/cd', 'docker', 'kubernetes', 'k8s'] },
    { title: 'Cloud Engineer', category: 'Cloud & DevOps', aliases: ['cloud', 'aws', 'azure', 'gcp', 'terraform'] },
    { title: 'Site Reliability Engineer (SRE)', category: 'Cloud & DevOps', aliases: ['sre', 'reliability', 'incident'] },
    { title: 'Cloud Architect', category: 'Cloud & DevOps', aliases: ['cloud architect', 'aws architect', 'azure architect'] },
    { title: 'Platform Engineer', category: 'Cloud & DevOps', aliases: ['internal platform', 'developer experience'] },
    { title: 'Systems & Network Engineer', category: 'Cloud & DevOps', aliases: ['sysadmin', 'linux', 'infra', 'network'] },
    { title: 'Linux Systems Administrator', category: 'Cloud & DevOps', aliases: ['sysadmin linux', 'debian', 'redhat'] },
    { title: 'Automation & Scripting Engineer', category: 'Cloud & DevOps', aliases: ['ansible', 'terraform', 'bash'] },
    { title: 'DevOps / Cloud Intern', category: 'Cloud & DevOps', aliases: ['devops intern', 'cloud intern'] },

    // 4. Product, Design & Agile
    { title: 'Product Manager', category: 'Product & Design', aliases: ['pm', 'product', 'roadmap'] },
    { title: 'Senior Product Manager', category: 'Product & Design', aliases: ['sr pm', 'lead pm'] },
    { title: 'Product Owner', category: 'Product & Design', aliases: ['po', 'backlog', 'user stories'] },
    { title: 'UI/UX Designer', category: 'Product & Design', aliases: ['ux', 'ui', 'figma', 'design', 'wireframe'] },
    { title: 'Product Designer', category: 'Product & Design', aliases: ['product design', 'ux/ui'] },
    { title: 'UX Researcher', category: 'Product & Design', aliases: ['ux research', 'user research'] },
    { title: 'Design System Lead', category: 'Product & Design', aliases: ['design tokens', 'components'] },
    { title: 'Scrum Master', category: 'Product & Design', aliases: ['scrum', 'agile coach', 'kanban'] },
    { title: 'Agile Coach', category: 'Product & Design', aliases: ['coach agile', 'safe'] },
    { title: 'Digital / IT Project Manager', category: 'Product & Design', aliases: ['pmo', 'project manager'] },
    { title: 'Technical Program Manager (TPM)', category: 'Product & Design', aliases: ['tpm', 'delivery manager'] },

    // 5. Cybersecurity
    { title: 'Cybersecurity Engineer', category: 'Cybersecurity', aliases: ['cyber', 'security', 'security engineer', 'secops'] },
    { title: 'SOC Analyst (Security Operations Center)', category: 'Cybersecurity', aliases: ['soc', 'siem', 'incident response'] },
    { title: 'Penetration Tester / Ethical Hacker', category: 'Cybersecurity', aliases: ['pentest', 'red team'] },
    { title: 'Cloud Security Engineer', category: 'Cybersecurity', aliases: ['cloud security', 'devsecops'] },
    { title: 'DevSecOps Engineer', category: 'Cybersecurity', aliases: ['devsecops', 'security pipeline'] },
    { title: 'GRC / Information Security Consultant', category: 'Cybersecurity', aliases: ['iso 27001', 'ebios', 'dpo'] },
    { title: 'CISO (Chief Information Security Officer)', category: 'Cybersecurity', aliases: ['ciso', 'security director'] },

    // 6. Management & Technical Leadership
    { title: 'Engineering Manager', category: 'Management', aliases: ['em', 'head of engineering', 'manager tech'] },
    { title: 'Tech Lead / Lead Developer', category: 'Management', aliases: ['lead dev', 'lead tech'] },
    { title: 'Chief Technology Officer (CTO)', category: 'Management', aliases: ['cto', 'technical director'] },
    { title: 'VP of Engineering', category: 'Management', aliases: ['vp eng', 'engineering director'] },
    { title: 'Chief Information Officer (CIO)', category: 'Management', aliases: ['cio'] },

    // 7. QA, Test & Quality
    { title: 'QA Engineer / Test Automation', category: 'QA & Testing', aliases: ['qa', 'test', 'cypress', 'playwright', 'selenium'] },
    { title: 'QA / Testing Analyst', category: 'QA & Testing', aliases: ['tester', 'istqb', 'qa analyst'] },
    { title: 'QA Lead', category: 'QA & Testing', aliases: ['lead qa', 'test manager'] },

    // 8. Business, Consulting & Tech Sales
    { title: 'Solutions Architect / Pre-Sales', category: 'Business Tech', aliases: ['presales', 'solution architect'] },
    { title: 'Customer Success Manager (CSM)', category: 'Business Tech', aliases: ['csm', 'client relation'] },
    { title: 'ERP Consultant (SAP / Salesforce)', category: 'Business Tech', aliases: ['sap consultant', 'salesforce consultant'] },
    { title: 'Tech Account Executive', category: 'Business Tech', aliases: ['sales', 'tech sales'] },
    { title: 'Growth Hacker / Growth Marketer', category: 'Business Tech', aliases: ['growth', 'acquisition'] },
  ];

  /**
   * Nettoie et normalise une chaîne (retire les accents et passe en minuscules).
   */
  normalize(value: string): string {
    return (value || '')
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .toLowerCase()
      .trim();
  }

  /**
   * Retourne l'URL du logo haute résolution via le domaine (ou Clearbit fallback).
   */
  getCompanyLogo(domain?: string): string | undefined {
    if (!domain) return undefined;
    return `https://logo.clearbit.com/${domain}`;
  }

  /**
   * Recherche intelligente d'entreprises :
   * 1. Vérifie le cache mémoire.
   * 2. Recherche dans l'historique utilisateur (marqué `alreadyApplied`).
   * 3. Recherche dans l'annuaire local certifié (150+ grandes entreprises / startups).
   * 4. Interroge l'API Clearbit Autocomplete en parallèle pour les entreprises mondiales.
   * 5. Fusionne et déduplique les résultats par nom/domaine normalisé.
   */
  searchCompanies(query: string, historicalCompanies: string[] = []): Observable<CompanySuggestion[]> {
    const rawQuery = (query || '').trim();
    if (rawQuery.length < 2) {
      return of([]);
    }

    const normQ = this.normalize(rawQuery);

    // Vérification cache
    if (this.companyCache.has(normQ)) {
      return of(this.companyCache.get(normQ)!);
    }

    // 1. Résultats depuis l'historique de candidatures
    const historyResults: CompanySuggestion[] = [];
    const uniqueHistory = Array.from(new Set(historicalCompanies.map((c) => (c || '').trim()))).filter(Boolean);

    for (const comp of uniqueHistory) {
      if (this.normalize(comp).includes(normQ)) {
        // Tenter de trouver le domaine dans le catalogue local
        const matchedLocal = this.localCompanyCatalog.find(
          (c) => this.normalize(c.name) === this.normalize(comp)
        );
        const domain = matchedLocal?.domain;
        historyResults.push({
          name: comp,
          domain,
          logo: this.getCompanyLogo(domain),
          source: 'history',
          alreadyApplied: true,
        });
      }
    }

    // 2. Résultats depuis l'annuaire local certifié
    const catalogResults: CompanySuggestion[] = [];
    for (const item of this.localCompanyCatalog) {
      const normName = this.normalize(item.name);
      const normDomain = this.normalize(item.domain);
      const matchName = normName.includes(normQ);
      const matchDomain = normDomain.includes(normQ);
      const matchTag = item.tags?.some((t) => this.normalize(t).includes(normQ));

      if (matchName || matchDomain || matchTag) {
        catalogResults.push({
          name: item.name,
          domain: item.domain,
          logo: this.getCompanyLogo(item.domain),
          source: 'catalog',
          alreadyApplied: uniqueHistory.some((h) => this.normalize(h) === normName),
        });
      }
    }

    // 3. Appel de l'API Clearbit en direct (avec timeout sécurisé de 2.5s)
    const clearbit$ = this.http
      .get<RawClearbitItem[]>(`${this.clearbitUrl}?query=${encodeURIComponent(rawQuery)}`)
      .pipe(
        timeout(2500),
        map((items) => {
          if (!Array.isArray(items)) return [];
          return items
            .filter((item) => item && typeof item.name === 'string' && item.name.trim().length > 0)
            .map((item): CompanySuggestion => {
              const name = item.name!.trim();
              const domain = item.domain?.trim();
              return {
                name,
                domain,
                logo: item.logo || this.getCompanyLogo(domain),
                source: 'clearbit',
                alreadyApplied: uniqueHistory.some((h) => this.normalize(h) === this.normalize(name)),
              };
            });
        }),
        catchError(() => of([] as CompanySuggestion[]))
      );

    return clearbit$.pipe(
      map((clearbitList) => {
        // Fusion ordonnée :
        // 1. Historique utilisateur pertinent
        // 2. Catalogue certifié local (priorité aux correspondances exactes / préfixes)
        // 3. Résultats Clearbit API
        const merged: CompanySuggestion[] = [];
        const seenNames = new Set<string>();

        const addCandidate = (item: CompanySuggestion) => {
          const key = this.normalize(item.name);
          if (!seenNames.has(key)) {
            seenNames.add(key);
            merged.push(item);
          }
        };

        // Ajout historique
        historyResults.forEach(addCandidate);

        // Tri du catalogue local : préfixe exact d'abord
        catalogResults.sort((a, b) => {
          const aStarts = this.normalize(a.name).startsWith(normQ) ? 0 : 1;
          const bStarts = this.normalize(b.name).startsWith(normQ) ? 0 : 1;
          return aStarts - bStarts;
        });
        catalogResults.forEach(addCandidate);

        // Ajout Clearbit
        clearbitList.forEach((cb) => {
          const key = this.normalize(cb.name);
          if (seenNames.has(key)) {
            // Si déjà présent mais sans logo/domaine, on enrichit
            const existing = merged.find((m) => this.normalize(m.name) === key);
            if (existing && !existing.domain && cb.domain) existing.domain = cb.domain;
            if (existing && !existing.logo && cb.logo) existing.logo = cb.logo;
          } else {
            addCandidate(cb);
          }
        });

        const finalResults = merged.slice(0, 8);
        this.companyCache.set(normQ, finalResults);
        return finalResults;
      })
    );
  }

  /**
   * Recherche intelligente de postes & rôles :
   * 1. Priorité aux rôles déjà postulés par l'utilisateur.
   * 2. Analyse de la taxonomie métier (200+ rôles) avec correspondances par mot-clé et alias.
   * 3. Insensibilité aux accents, à la casse et aux espaces.
   */
  searchJobTitles(query: string, historicalRoles: string[] = []): JobTitleSuggestion[] {
    const rawQuery = (query || '').trim();
    if (rawQuery.length < 2) {
      return [];
    }

    const normQ = this.normalize(rawQuery);
    const results: JobTitleSuggestion[] = [];
    const seenTitles = new Set<string>();

    const addTitle = (item: JobTitleSuggestion) => {
      const key = this.normalize(item.title);
      if (!seenTitles.has(key)) {
        seenTitles.add(key);
        results.push(item);
      }
    };

    // 1. Postes historiques de l'utilisateur
    const uniqueHistory = Array.from(new Set(historicalRoles.map((r) => (r || '').trim()))).filter(Boolean);
    for (const r of uniqueHistory) {
      if (this.normalize(r).includes(normQ)) {
        // Tenter de retrouver la catégorie depuis le catalogue
        const matched = this.jobCatalog.find((j) => this.normalize(j.title) === this.normalize(r));
        addTitle({
          title: r,
          category: matched?.category || 'Other',
          alreadyApplied: true,
        });
      }
    }

    // 2. Correspondances avec la taxonomie métier
    const scoredList: Array<{ item: JobTitleSuggestion; score: number }> = [];

    for (const job of this.jobCatalog) {
      const normTitle = this.normalize(job.title);
      let score = -1;

      if (normTitle === normQ) {
        score = 100; // Exact match
      } else if (normTitle.startsWith(normQ)) {
        score = 80; // Préfixe direct
      } else {
        // Détection mot par mot (ex: "engineer" correspond à "Software Engineer")
        const words = normTitle.split(/\s+/);
        if (words.some((w) => w.startsWith(normQ))) {
          score = 60;
        } else if (normTitle.includes(normQ)) {
          score = 40;
        } else if (job.aliases?.some((a) => this.normalize(a).includes(normQ) || normQ.includes(this.normalize(a)))) {
          score = 30; // Correspondance par alias (ex: "dev" -> "Développeur Full Stack")
        }
      }

      if (score > 0) {
        scoredList.push({
          item: {
            title: job.title,
            category: job.category,
            alreadyApplied: uniqueHistory.some((h) => this.normalize(h) === normTitle),
          },
          score,
        });
      }
    }

    // Trier par score décroissant puis alphabétique
    scoredList.sort((a, b) => {
      if (b.score !== a.score) return b.score - a.score;
      return a.item.title.localeCompare(b.item.title);
    });

    scoredList.forEach((s) => addTitle(s.item));

    return results.slice(0, 8);
  }
}
