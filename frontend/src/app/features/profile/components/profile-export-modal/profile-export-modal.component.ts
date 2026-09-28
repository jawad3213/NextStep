import { Component, computed, input, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { Profile } from '../../data-access/profile.models';

/**
 * "Export du profil" dialog: JSON payload (code view) or a readable summary (visual view),
 * with copy and download. Styles live in the profile page stylesheet (export-* classes).
 */
@Component({
  selector: 'app-profile-export-modal',
  standalone: true,
  imports: [CommonModule, MatIconModule],
  templateUrl: './profile-export-modal.component.html',
})
export class ProfileExportModalComponent {
  readonly profile = input.required<Profile>();
  readonly exportJsonString = input.required<string>();
  readonly exportFilename = input.required<string>();

  readonly closed = output<void>();
  readonly download = output<void>();

  readonly isExportCopied = signal(false);
  readonly exportViewMode = signal<'code' | 'visual'>('code');

  readonly exportStats = computed(() => {
    const p = this.profile();
    return {
      experiences: p.experience?.length || 0,
      education: p.education?.length || 0,
      skills: p.skills?.length || 0,
      languages: p.languages?.length || 0,
      projects: p.projets?.length || 0,
      certifications: p.certifications?.length || 0,
      hasResume: !!(p.resume && p.resume.length > 20),
      totalItems: (p.experience?.length || 0) + (p.education?.length || 0) + (p.skills?.length || 0) + (p.languages?.length || 0) + (p.projets?.length || 0) + (p.certifications?.length || 0)
    };
  });

  readonly jsonSizeKb = computed(() => {
    const str = this.exportJsonString();
    if (!str) return '0.0';
    const bytes = new Blob([str]).size;
    return (bytes / 1024).toFixed(1);
  });

  readonly jsonLines = computed(() => {
    const s = this.exportJsonString();
    return s ? s.split('\n') : [];
  });

  closeExportModal() {
    this.closed.emit();
  }

  downloadExportJson() {
    this.download.emit();
  }

  async copyExportJson() {
    try {
      await navigator.clipboard.writeText(this.exportJsonString());
      this.isExportCopied.set(true);
      setTimeout(() => {
        this.isExportCopied.set(false);
      }, 2500);
    } catch (err) {
      console.error('Failed to copy JSON to clipboard', err);
    }
  }
}
