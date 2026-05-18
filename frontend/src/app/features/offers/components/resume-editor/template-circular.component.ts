import { Component, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ResumeData } from './resume.model';

@Component({
  selector: 'app-template-circular',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div
      class="w-full h-full flex print:flex-row"
      [style.--primary-color]="data().primaryColor"
      [ngClass]="fontSizeClasses"
    >
      <!-- Sidebar -->
      <aside
        class="w-1/3 p-6 text-white flex flex-col"
        [style.backgroundColor]="data().primaryColor"
      >
        <!-- Photo -->
        @if (data().personalDetails.photoUrl) {
          <div class="flex justify-center mb-4">
            <img
              [src]="data().personalDetails.photoUrl"
              alt="Photo"
              class="w-24 h-24 rounded-full object-cover border-4 border-white/30"
            />
          </div>
        }

        <!-- Name & Title -->
        <div class="text-center mb-6">
          @if (data().personalDetails.fullName) {
            <h1 class="text-xl font-bold uppercase tracking-wide">
              {{ data().personalDetails.fullName }}
            </h1>
          }
          @if (data().personalDetails.title) {
            <p class="text-sm mt-1 opacity-90">
              {{ data().personalDetails.title }}
            </p>
          }
        </div>

        <!-- Contact Info -->
        <div class="mb-6 space-y-2">
          <h3 class="text-xs font-bold uppercase tracking-wider mb-3 opacity-80">
            Contact
          </h3>
          @if (data().personalDetails.email) {
            <div class="flex items-center gap-2 text-sm">
              <svg class="w-4 h-4 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 8l7.89 5.26a2 2 0 002.22 0L21 8M5 19h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z"/>
              </svg>
              <span class="truncate">{{ data().personalDetails.email }}</span>
            </div>
          }
          @if (data().personalDetails.phone) {
            <div class="flex items-center gap-2 text-sm">
              <svg class="w-4 h-4 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 5a2 2 0 012-2h3.28a1 1 0 01.948.684l1.498 4.493a1 1 0 01-.502 1.21l-2.257 1.13a11.042 11.042 0 005.516 5.516l1.13-2.257a1 1 0 011.21-.502l4.493 1.498a1 1 0 01.684.949V19a2 2 0 01-2 2h-1C9.716 21 3 14.284 3 6V5z"/>
              </svg>
              <span>{{ data().personalDetails.phone }}</span>
            </div>
          }
          @if (data().personalDetails.location) {
            <div class="flex items-center gap-2 text-sm">
              <svg class="w-4 h-4 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z"/>
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 11a3 3 0 11-6 0 3 3 0 016 0z"/>
              </svg>
              <span>{{ data().personalDetails.location }}</span>
            </div>
          }
          @if (data().personalDetails.linkedIn) {
            <div class="flex items-center gap-2 text-sm">
              <svg class="w-4 h-4 flex-shrink-0" fill="currentColor" viewBox="0 0 24 24">
                <path d="M20.447 20.452h-3.554v-5.569c0-1.328-.027-3.037-1.852-3.037-1.853 0-2.136 1.445-2.136 2.939v5.667H9.351V9h3.414v1.561h.046c.477-.9 1.637-1.85 3.37-1.85 3.601 0 4.267 2.37 4.267 5.455v6.286zM5.337 7.433a2.062 2.062 0 01-2.063-2.065 2.064 2.064 0 112.063 2.065zm1.782 13.019H3.555V9h3.564v11.452zM22.225 0H1.771C.792 0 0 .774 0 1.729v20.542C0 23.227.792 24 1.771 24h20.451C23.2 24 24 23.227 24 22.271V1.729C24 .774 23.2 0 22.222 0h.003z"/>
              </svg>
              <span class="truncate">{{ data().personalDetails.linkedIn }}</span>
            </div>
          }
        </div>

        <!-- Skills -->
        @if (data().skills.length > 0) {
          <div class="mb-6">
            <h3 class="text-xs font-bold uppercase tracking-wider mb-3 opacity-80">
              Compétences
            </h3>
            <div class="space-y-2">
              @for (skill of data().skills; track skill.name) {
                <div>
                  <div class="flex justify-between text-xs mb-1">
                    <span>{{ skill.name }}</span>
                  </div>
                  <div class="h-1.5 bg-white/20 rounded-full overflow-hidden">
                    <div
                      class="h-full rounded-full bg-white"
                      [style.width.%]="(skill.level / 3) * 100"
                    ></div>
                  </div>
                </div>
              }
            </div>
          </div>
        }

        <!-- Languages -->
        @if (data().languages.length > 0) {
          <div class="mb-6">
            <h3 class="text-xs font-bold uppercase tracking-wider mb-3 opacity-80">
              Langues
            </h3>
            <div class="space-y-2">
              @for (lang of data().languages; track lang.name) {
                <div class="flex justify-between items-center text-sm">
                  <span>{{ lang.name }}</span>
                  <span class="text-xs opacity-70">{{ lang.proficiency }}</span>
                </div>
              }
            </div>
          </div>
        }

        <!-- Certifications -->
        @if (data().certifications.length > 0) {
          <div>
            <h3 class="text-xs font-bold uppercase tracking-wider mb-3 opacity-80">
              Certifications
            </h3>
            <ul class="space-y-2">
              @for (cert of data().certifications; track cert.name) {
                <li class="text-xs">
                  <span class="font-medium">{{ cert.name }}</span>
                  @if (cert.issuer) {
                    <span class="opacity-70"> - {{ cert.issuer }}</span>
                  }
                </li>
              }
            </ul>
          </div>
        }
      </aside>

      <!-- Main Content -->
      <main class="w-2/3 p-6 bg-white">
        <!-- Summary -->
        @if (data().summary) {
          <section class="mb-6">
            <h2
              class="text-sm font-bold uppercase tracking-wider mb-2 pb-1"
              [style.color]="data().primaryColor"
              [style.borderBottomColor]="data().primaryColor + '40'"
              style="border-bottom: 2px solid"
            >
              Profil Professionnel
            </h2>
            <p class="text-slate-600 leading-relaxed">
              {{ data().summary }}
            </p>
          </section>
        }

        <!-- Experience -->
        @if (data().experience.length > 0) {
          <section class="mb-6">
            <h2
              class="text-sm font-bold uppercase tracking-wider mb-3 pb-1"
              [style.color]="data().primaryColor"
              [style.borderBottomColor]="data().primaryColor + '40'"
              style="border-bottom: 2px solid"
            >
              Expérience Professionnelle
            </h2>
            <div class="space-y-4">
              @for (exp of data().experience; track exp.role + exp.company + exp.start; let i = $index) {
                <div class="relative pl-4" [style.borderLeft]="data().primaryColor + '30'" style="border-left: 3px solid">
                  <div class="flex justify-between items-start mb-1">
                    <div>
                      <h3 class="font-semibold text-slate-800">{{ exp.role }}</h3>
                      <p class="text-sm" [style.color]="data().primaryColor">{{ exp.company }}</p>
                    </div>
                    <span class="text-xs text-slate-400 whitespace-nowrap ml-2">
                      {{ formatDate(exp.start) }} - {{ formatDate(exp.end) }}
                    </span>
                  </div>
                  @if (exp.bullets.length > 0) {
                    <ul class="mt-2 space-y-1">
                      @for (bullet of exp.bullets; track bullet + $index) {
                        <li class="text-sm text-slate-600 flex items-start gap-2">
                          <span class="mt-1.5 w-1.5 h-1.5 rounded-full flex-shrink-0" [style.backgroundColor]="data().primaryColor"></span>
                          <span>{{ bullet }}</span>
                        </li>
                      }
                    </ul>
                  }
                </div>
              }
            </div>
          </section>
        }

        <!-- Education -->
        @if (data().education.length > 0) {
          <section class="mb-6">
            <h2
              class="text-sm font-bold uppercase tracking-wider mb-3 pb-1"
              [style.color]="data().primaryColor"
              [style.borderBottomColor]="data().primaryColor + '40'"
              style="border-bottom: 2px solid"
            >
              Formation
            </h2>
            <div class="space-y-3">
              @for (edu of data().education; track edu.degree + edu.institution + edu.year) {
                <div class="flex justify-between items-start">
                  <div>
                    <h3 class="font-semibold text-slate-800">{{ edu.degree }}</h3>
                    <p class="text-sm text-slate-600">{{ edu.institution }}</p>
                  </div>
                  <span class="text-xs text-slate-400 whitespace-nowrap ml-2">{{ edu.year }}</span>
                </div>
              }
            </div>
          </section>
        }

        <!-- Projects -->
        @if (data().projects.length > 0) {
          <section class="mb-6">
            <h2
              class="text-sm font-bold uppercase tracking-wider mb-3 pb-1"
              [style.color]="data().primaryColor"
              [style.borderBottomColor]="data().primaryColor + '40'"
              style="border-bottom: 2px solid"
            >
              Projets
            </h2>
            <div class="space-y-3">
              @for (project of data().projects; track project.title) {
                <div>
                  <h3 class="font-semibold text-slate-800">{{ project.title }}</h3>
                  @if (project.bullets.length > 0) {
                    <ul class="mt-1 space-y-1">
                      @for (bullet of project.bullets; track bullet) {
                        <li class="text-sm text-slate-600 flex items-start gap-2">
                          <span class="mt-1.5 w-1.5 h-1.5 rounded-full flex-shrink-0" [style.backgroundColor]="data().primaryColor"></span>
                          <span>{{ bullet }}</span>
                        </li>
                      }
                    </ul>
                  }
                </div>
              }
            </div>
          </section>
        }
      </main>
    </div>
  `,
  styles: [`
    :host {
      display: block;
      width: 100%;
      height: 100%;
    }
  `],
})
export class TemplateCircularComponent {
  data = input.required<ResumeData>();

  protected get fontSizeClasses(): string {
    const size = this.data().fontSize;
    switch (size) {
      case 'sm':
        return 'text-xs';
      case 'lg':
        return 'text-base';
      default:
        return 'text-sm';
    }
  }

  protected formatDate(date: string): string {
    if (!date || date.toLowerCase() === 'present') return 'Présent';
    const parts = date.split('-');
    if (parts.length === 2) {
      const months = ['Jan', 'Fév', 'Mar', 'Avr', 'Mai', 'Juin', 'Juil', 'Aoû', 'Sep', 'Oct', 'Nov', 'Déc'];
      return months[parseInt(parts[1]) - 1] + ' ' + parts[0];
    }
    return date;
  }
}
