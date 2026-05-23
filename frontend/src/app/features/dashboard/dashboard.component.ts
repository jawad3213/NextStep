import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';

interface Application {
  company: string;
  logo: string;
  poste: string;
  date: string;
  status: string;
  statusColor: 'success' | 'warning' | 'primary' | 'slate';
}

interface Activity {
  icon: string;
  iconBg: string;
  iconColor: string;
  text: string;
  time: string;
}

interface Interview {
  dayLabel: string;
  date: string;
  title: string;
  time: string;
  location: string;
  isToday: boolean;
}

interface MissingSection {
  icon: string;
  label: string;
  description: string;
  route: string;
  color: 'brand' | 'amber' | 'purple' | 'green';
}

interface OfferOverview {
  company: string;
  poste: string;
  matchScore: number;
  atsScore: number;
  date: string;
  skills: string[];
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent {
  readonly refreshing = signal(false);

  readonly userName = 'Yassine';
  readonly profileCompletion = 75;

  readonly missingSections: MissingSection[] = [
    { icon: 'work_history', label: 'Expériences', description: 'Ajoutez votre parcours professionnel', route: '/profile', color: 'brand' },
    { icon: 'school', label: 'Formations', description: 'Complétez votre niveau d\'études', route: '/profile', color: 'amber' },
    { icon: 'psychology', label: 'Compétences', description: 'Listez vos savoir-faire techniques', route: '/profile', color: 'purple' },
    { icon: 'verified', label: 'Certifications', description: 'Ajoutez vos certifications', route: '/profile', color: 'green' },
  ];

  readonly totalSent = 12;
  readonly totalPending = 5;
  readonly totalAccepted = 2;
  readonly totalCvs = 8;
  readonly successRate = 68;
  readonly successTrend = '+5%';
  readonly avgTime = '18 j.';

  readonly totalApplications = this.totalSent + this.totalPending + this.totalAccepted;
  readonly sentPct = Math.round((this.totalSent / this.totalApplications) * 100);
  readonly pendingPct = Math.round((this.totalPending / this.totalApplications) * 100);
  readonly acceptedPct = Math.round((this.totalAccepted / this.totalApplications) * 100);

  readonly sentOffset = 251.2 * (1 - this.sentPct / 100);
  readonly pendingOffset = 251.2 * (1 - this.pendingPct / 100);
  readonly acceptedOffset = 251.2 * (1 - this.acceptedPct / 100);

  readonly sentAngle = 0;
  readonly pendingAngle = (this.sentPct / 100) * 360;
  readonly acceptedAngle = ((this.sentPct + this.pendingPct) / 100) * 360;

  readonly recentApplications: Application[] = [
    { company: 'Google', logo: 'business', poste: 'Senior Product Designer', date: '17 Mars 2026', status: 'Entretien', statusColor: 'success' },
    { company: 'Stripe', logo: 'payments', poste: 'UX Lead', date: '15 Mars 2026', status: 'En attente', statusColor: 'warning' },
    { company: 'Meta', logo: 'tactic', poste: 'Product Manager', date: '12 Mars 2026', status: 'Envoyé', statusColor: 'primary' },
    { company: 'Airbnb', logo: 'home', poste: 'Senior Front-end', date: '10 Mars 2026', status: 'Archivé', statusColor: 'slate' },
  ];

  readonly activities: Activity[] = [
    { icon: 'description', iconBg: 'bg-brand-50', iconColor: 'text-brand-500', text: 'CV "Product Designer v4" mis à jour', time: 'Il y a 2 heures' },
    { icon: 'check_circle', iconBg: 'bg-green-50', iconColor: 'text-green-600', text: 'Candidature acceptée chez Google Cloud', time: 'Hier, 14:30' },
    { icon: 'auto_awesome', iconBg: 'bg-purple-50', iconColor: 'text-purple-600', text: 'Nouvelle offre analysée — Senior Front-end', time: 'Hier, 09:15' },
  ];

  readonly interviews: Interview[] = [
    { dayLabel: 'Mar', date: '17', title: 'Entretien Technique — Google', time: '14:00', location: 'Meet', isToday: true },
    { dayLabel: 'Mer', date: '18', title: 'Culture Fit — Stripe', time: '10:30', location: 'Zoom', isToday: false },
  ];

  readonly applications: Application[] = [
    { company: 'Google', logo: '', poste: 'Senior Product Designer', date: '', status: 'Entretien', statusColor: 'success' },
    { company: 'Stripe', logo: '', poste: 'UX Lead', date: '', status: 'En attente', statusColor: 'warning' },
    { company: 'Meta', logo: '', poste: 'Product Manager', date: '', status: 'Envoyé', statusColor: 'primary' },
  ];

  readonly responsesPct = 67;
  readonly responsesCount = 8;
  readonly noResponseCount = 4;

  readonly offers: OfferOverview[] = [
    { company: 'Google', poste: 'Senior Product Designer', matchScore: 82, atsScore: 71, date: '17 Mars', skills: ['Figma', 'Design Systems', 'User Research'] },
    { company: 'Stripe', poste: 'UX Lead', matchScore: 65, atsScore: 58, date: '15 Mars', skills: ['UX Strategy', 'Prototyping', 'Design Ops'] },
    { company: 'Meta', poste: 'Product Manager', matchScore: 45, atsScore: 52, date: '12 Mars', skills: ['Agile', 'Roadmapping', 'Analytics'] },
  ];

  get statusClassMap(): Record<string, string> {
    return {
      success: 'bg-green-50 text-green-700',
      warning: 'bg-amber-50 text-amber-700',
      primary: 'bg-brand-50 text-brand-600',
      slate: 'bg-slate-100 text-slate-500',
    };
  }

  get missingColorMap(): Record<string, string> {
    return {
      brand: 'border-brand-200 bg-brand-50 hover:bg-brand-100 text-brand-700',
      amber: 'border-amber-200 bg-amber-50 hover:bg-amber-100 text-amber-700',
      purple: 'border-purple-200 bg-purple-50 hover:bg-purple-100 text-purple-700',
      green: 'border-green-200 bg-green-50 hover:bg-green-100 text-green-700',
    };
  }

  get missingIconColorMap(): Record<string, string> {
    return {
      brand: 'text-brand-500',
      amber: 'text-amber-500',
      purple: 'text-purple-500',
      green: 'text-green-600',
    };
  }

  getScoreColor(score: number): string {
    if (score >= 70) return 'text-green-600';
    if (score >= 50) return 'text-amber-500';
    return 'text-red-500';
  }

  getScoreBg(score: number): string {
    if (score >= 70) return 'bg-green-50';
    if (score >= 50) return 'bg-amber-50';
    return 'bg-red-50';
  }

  refresh(): void {
    this.refreshing.set(true);
    setTimeout(() => this.refreshing.set(false), 1200);
  }
}
