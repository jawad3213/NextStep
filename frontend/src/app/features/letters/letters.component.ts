import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-letters',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="shell-container">
      <header class="page-header">
        <h1 class="page-title">Email & Letter</h1>
        <p class="page-description">Generated email preview, cover letter, sending status.</p>
      </header>
      <div class="placeholder-card">✉️ Email & Letter section under development...</div>
    </div>
  `,
  styles: [`.shell-container { .page-header { margin-bottom: 32px; .page-title { font-size: 28px; font-weight: 800; color: #002D5B; margin: 0; } .page-description { color: #64748B; margin: 4px 0 0 0; font-size: 16px; } } .placeholder-card { background: white; border: 2px dashed #CBD5E1; border-radius: 12px; height: 300px; display: flex; align-items: center; justify-content: center; color: #94A3B8; font-weight: 600; font-size: 18px; } }`]
})
export class LettersComponent {}
