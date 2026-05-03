import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-company-intel',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="shell-container">
      <header class="page-header">
        <h1 class="page-title">Company Intelligence</h1>
        <p class="page-description">Company profile, salaries, culture score, news.</p>
      </header>
      <div class="placeholder-card">🏢 Company Intel section under development...</div>
    </div>
  `,
  styles: [`.shell-container { .page-header { margin-bottom: 32px; .page-title { font-size: 28px; font-weight: 800; color: #002D5B; margin: 0; } .page-description { color: #64748B; margin: 4px 0 0 0; font-size: 16px; } } .placeholder-card { background: white; border: 2px dashed #CBD5E1; border-radius: 12px; height: 300px; display: flex; align-items: center; justify-content: center; color: #94A3B8; font-weight: 600; font-size: 18px; } }`]
})
export class CompanyIntelComponent {}
