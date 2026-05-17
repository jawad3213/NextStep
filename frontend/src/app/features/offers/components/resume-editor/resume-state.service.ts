import { Injectable, signal, computed } from '@angular/core';
import { ResumeData, INITIAL_RESUME_DATA } from './resume.model';

@Injectable({
  providedIn: 'root',
})
export class ResumeStateService {
  private readonly STORAGE_KEY = 'nextstep_resume_state';

  private readonly savedState = this.loadFromStorage();

  resumeData = signal<ResumeData>(this.savedState ?? INITIAL_RESUME_DATA);

  selectedTemplate = computed(() => this.resumeData().selectedTemplate);

  primaryColor = computed(() => this.resumeData().primaryColor);

  fontSize = computed(() => this.resumeData().fontSize);

  hasData = computed(() => {
    const data = this.resumeData();
    return !!(
      data.personalDetails.fullName ||
      data.summary ||
      data.experience.length > 0 ||
      data.education.length > 0 ||
      data.skills.length > 0
    );
  });

  completionPercentage = computed(() => {
    const data = this.resumeData();
    let filled = 0;
    let total = 8;

    if (data.personalDetails.fullName) filled++;
    if (data.personalDetails.email) filled++;
    if (data.personalDetails.phone) filled++;
    if (data.summary) filled++;
    if (data.experience.length > 0) filled++;
    if (data.education.length > 0) filled++;
    if (data.skills.length > 0) filled++;
    if (data.languages.length > 0) filled++;

    return Math.round((filled / total) * 100);
  });

  updateData(newData: Partial<ResumeData>): void {
    this.resumeData.update(current => ({
      ...current,
      ...newData,
    }));
    this.saveToStorage();
  }

  updatePersonalDetails(details: Partial<ResumeData['personalDetails']>): void {
    this.resumeData.update(current => ({
      ...current,
      personalDetails: {
        ...current.personalDetails,
        ...details,
      },
    }));
    this.saveToStorage();
  }

  setTemplate(templateId: ResumeData['selectedTemplate']): void {
    this.resumeData.update(current => ({
      ...current,
      selectedTemplate: templateId,
    }));
    this.saveToStorage();
  }

  setPrimaryColor(color: string): void {
    this.resumeData.update(current => ({
      ...current,
      primaryColor: color,
    }));
    this.saveToStorage();
  }

  setFontSize(size: ResumeData['fontSize']): void {
    this.resumeData.update(current => ({
      ...current,
      fontSize: size,
    }));
    this.saveToStorage();
  }

  resetState(): void {
    this.resumeData.set(INITIAL_RESUME_DATA);
    localStorage.removeItem(this.STORAGE_KEY);
  }

  private loadFromStorage(): ResumeData | null {
    try {
      const stored = localStorage.getItem(this.STORAGE_KEY);
      if (stored) {
        return JSON.parse(stored) as ResumeData;
      }
    } catch {
      console.warn('[ResumeStateService] Failed to load state from localStorage');
    }
    return null;
  }

  private saveToStorage(): void {
    try {
      localStorage.setItem(this.STORAGE_KEY, JSON.stringify(this.resumeData()));
    } catch {
      console.warn('[ResumeStateService] Failed to save state to localStorage');
    }
  }
}
