import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-offers',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="shell-container">
      <header class="page-header">
        <h1 class="page-title">Job Offers</h1>
        <p class="page-description">Analyze ads and track your opportunities using artificial intelligence.</p>
      </header>
      
      <div class="card card-accent">
        <p class="card-placeholder">
          The job analysis engine is being integrated. 
          You will soon be able to paste a URL or text to get an instant compatibility score.
        </p>
      </div>

      <div style="margin-top: 24px; display: grid; grid-template-columns: 1fr 1fr; gap: 24px;">
        <div class="card">
          <h4 style="margin-top: 0; color: #1e293b;">Latest Searches</h4>
          <p style="color: #64748b; font-size: 14px;">Your recent analyses will appear here for quick access.</p>
        </div>
        <div class="card">
          <h4 style="margin-top: 0; color: #1e293b;">AI Suggestions</h4>
          <p style="color: #64748b; font-size: 14px;">Based on your profile, we will suggest the best offers on the market.</p>
        </div>
      </div>
    </div>
  `,
  styles: [] 
})
export class OffersComponent {}
