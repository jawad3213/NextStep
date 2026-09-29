import { Injectable, signal, computed } from '@angular/core';

export type AppLang = 'en' | 'fr';

const LS_KEY = 'ns_settings_language';

export const TRANSLATIONS: Record<AppLang, Record<string, string>> = {
  fr: {
    // Sidebar group titles
    'nav.core': 'Vue d\'ensemble',
    'nav.discovery': 'Découverte & Analyse',
    'nav.tracking': 'Candidatures & Suivi',
    'nav.preparation': 'Préparation IA & Docs',
    'nav.system': 'Système',

    // Sidebar items
    'nav.dashboard': 'Tableau de bord',
    'nav.profile': 'Mon Profil',
    'nav.jobScraper': 'Recherche d\'offres',
    'nav.jobPipeline': 'Analyse d\'offres',
    'nav.companyAnalyzer': 'Analyse entreprise',
    'nav.applicationTracking': 'Suivi candidatures',
    'nav.emailTracking': 'Suivi emails',
    'nav.aiChatbot': 'AI Chatbot',
    'nav.cvHistory': 'Historique CV',
    'nav.settings': 'Paramètres',
    'nav.signOut': 'Se déconnecter',

    // Settings page
    'settings.title': 'Paramètres',
    'settings.subtitle': 'Gérez votre compte, vos connexions et vos préférences.',
    'settings.account': 'Compte',
    'settings.personalInfo': 'Informations personnelles',
    'settings.personalInfoDesc': 'Nom, email, téléphone et photo de profil',
    'settings.edit': 'Modifier',
    'settings.exportProfile': 'Export du profil (JSON)',
    'settings.exportProfileDesc': 'Téléchargez vos données de profil au format JSON',
    'settings.export': 'Exporter',
    'settings.notificationsEmail': 'Notifications & Email',
    'settings.gmailConnection': 'Connexion Gmail',
    'settings.gmailConnectedAs': 'Connecté en tant que :',
    'settings.gmailConnectDesc': 'Connectez votre boîte Gmail pour la détection automatique des réponses',
    'settings.connected': 'Connecté',
    'settings.connect': 'Connecter',
    'settings.reconnect': 'Reconnecter',
    'settings.gmailReconnectDesc': 'Gmail doit être reconnecté pour envoyer des emails et détecter les réponses',
    'settings.manage': 'Gérer',
    'settings.emailNotifs': 'Notifications email',
    'settings.emailNotifsDesc': 'Recevoir des alertes de relance et nouvelles offres',
    'settings.interviewReminders': 'Rappels entretiens',
    'settings.interviewRemindersDesc': 'Notifications 24h avant un entretien planifié',
    'settings.linkedin': 'LinkedIn',
    'settings.linkedinUrl': 'URL du profil LinkedIn',
    'settings.linkedinUrlDesc': 'Ajoutez votre profil pour la recherche d\'offres personnalisée',
    'settings.save': 'Enregistrer',
    'settings.preferences': 'Préférences',
    'settings.language': 'Langue de l\'interface',
    'settings.languageDesc': 'Choisissez la langue d\'affichage de l\'application',
    'settings.timezone': 'Fuseau horaire',
    'settings.dangerZone': 'Zone de danger',
    'settings.clearData': 'Supprimer toutes les données',
    'settings.clearDataDesc': 'Cette action effacera votre profil, vos offres et vos CV de manière irréversible',
    'settings.clear': 'Effacer',

    // Dashboard
    'dashboard.greeting': 'Bonjour',
    'dashboard.loading': 'Chargement de votre tableau de bord…',
    'dashboard.analyzeOffer': 'Analyser une offre',
    'dashboard.retry': 'Réessayer',
    'dashboard.generateCv': 'Générer un CV',
    'dashboard.searchOffers': 'Chercher des offres',
    'dashboard.writeEmail': 'Rédiger un email',
    'dashboard.analyzeCompany': 'Analyser une entreprise',
  },

  en: {
    // Sidebar group titles
    'nav.core': 'Overview',
    'nav.discovery': 'Job Discovery & Analysis',
    'nav.tracking': 'Application & Tracking',
    'nav.preparation': 'AI Preparation & Docs',
    'nav.system': 'System',

    // Sidebar items
    'nav.dashboard': 'Dashboard',
    'nav.profile': 'Profile',
    'nav.jobScraper': 'Job Scraper',
    'nav.jobPipeline': 'Job Analyzer Pipeline',
    'nav.companyAnalyzer': 'Company Analyzer',
    'nav.applicationTracking': 'Application Tracking',
    'nav.emailTracking': 'Email Tracking',
    'nav.aiChatbot': 'AI Chatbot',
    'nav.cvHistory': 'CV History',
    'nav.settings': 'Settings',
    'nav.signOut': 'Sign out',

    // Settings page
    'settings.title': 'Settings',
    'settings.subtitle': 'Manage your account, connections and preferences.',
    'settings.account': 'Account',
    'settings.personalInfo': 'Personal information',
    'settings.personalInfoDesc': 'Name, email, phone and profile picture',
    'settings.edit': 'Edit',
    'settings.exportProfile': 'Profile export (JSON)',
    'settings.exportProfileDesc': 'Download your complete profile data as JSON',
    'settings.export': 'Export',
    'settings.notificationsEmail': 'Notifications & Email',
    'settings.gmailConnection': 'Gmail connection',
    'settings.gmailConnectedAs': 'Connected as:',
    'settings.gmailConnectDesc': 'Connect your Gmail inbox for automatic reply detection',
    'settings.connected': 'Connected',
    'settings.connect': 'Connect',
    'settings.reconnect': 'Reconnect',
    'settings.gmailReconnectDesc': 'Gmail needs to be reconnected to send emails and detect replies',
    'settings.manage': 'Manage',
    'settings.emailNotifs': 'Email notifications',
    'settings.emailNotifsDesc': 'Receive alerts for follow-ups and new offers',
    'settings.interviewReminders': 'Interview reminders',
    'settings.interviewRemindersDesc': 'Notifications 24h before a scheduled interview',
    'settings.linkedin': 'LinkedIn',
    'settings.linkedinUrl': 'LinkedIn profile URL',
    'settings.linkedinUrlDesc': 'Add your profile for personalized job search',
    'settings.save': 'Save',
    'settings.preferences': 'Preferences',
    'settings.language': 'Interface language',
    'settings.languageDesc': 'Choose the display language of the application',
    'settings.timezone': 'Time zone',
    'settings.dangerZone': 'Danger zone',
    'settings.clearData': 'Delete all data',
    'settings.clearDataDesc': 'This will permanently erase your profile, offers and CVs',
    'settings.clear': 'Clear',

    // Dashboard
    'dashboard.greeting': 'Hello',
    'dashboard.loading': 'Loading your dashboard…',
    'dashboard.analyzeOffer': 'Analyze an offer',
    'dashboard.retry': 'Retry',
    'dashboard.generateCv': 'Generate a CV',
    'dashboard.searchOffers': 'Search jobs',
    'dashboard.writeEmail': 'Write an email',
    'dashboard.analyzeCompany': 'Analyze a company',
  }
};

@Injectable({ providedIn: 'root' })
export class LanguageService {
  readonly lang = signal<AppLang>(this.getInitialLang());

  readonly dict = computed(() => TRANSLATIONS[this.lang()]);

  /** Translate a key. Falls back to the key itself if not found. */
  t(key: string): string {
    return this.dict()[key] ?? key;
  }

  setLang(lang: AppLang): void {
    this.lang.set(lang);
    localStorage.setItem(LS_KEY, lang);
    // Update the html[lang] attribute for accessibility & SEO
    document.documentElement.lang = lang;
  }

  private getInitialLang(): AppLang {
    const saved = localStorage.getItem(LS_KEY);
    if (saved === 'en' || saved === 'fr') return saved;
    // Default to English
    return 'en';
  }
}
