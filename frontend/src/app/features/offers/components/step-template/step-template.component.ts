import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { PipelineStateService } from '../../../../services/pipeline-state.service';
import { OfferApiService } from '../../services/offer-api.service';
import { environment } from '../../../../../environments/environment';

interface CvTemplateDto {
  id: string;
  slug: string;
  name: string;
  description: string | null;
  thumbnailUrl: string | null;
  industries: string[];
  experienceLevels: string[];
  style: string;
  layoutFlags: string[];
  backgroundColor: string;
  tags: string[];
}

@Component({
  selector: 'app-step-template',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './step-template.component.html',
  styleUrl: './step-template.component.scss'
})
export class StepTemplateComponent implements OnInit {
  pipeline = inject(PipelineStateService);
  private http = inject(HttpClient);
  private offerApi = inject(OfferApiService);

  templates: CvTemplateDto[] = [];
  isGenerating = false;

  ngOnInit(): void {
    this.http.get<CvTemplateDto[]>(`${environment.apiBaseUrl}/cv/templates`)
      .subscribe({
        next: (data) => { this.templates = data; },
        error: () => {
          this.templates = [
            { id: '', slug: 'modern', name: 'Modern', description: 'Dark blue header, two-column layout.', thumbnailUrl: null, industries: [], experienceLevels: [], style: 'Modern', layoutFlags: [], backgroundColor: '#1B2A4A', tags: [] },
            { id: '', slug: 'classic', name: 'Classic', description: 'Clean single-column design.', thumbnailUrl: null, industries: [], experienceLevels: [], style: 'Traditional', layoutFlags: [], backgroundColor: '#FFFFFF', tags: [] },
            { id: '', slug: 'executive', name: 'Executive', description: 'Salmon/peach four-quadrant design.', thumbnailUrl: null, industries: [], experienceLevels: [], style: 'Elegant', layoutFlags: [], backgroundColor: '#F4A68C', tags: [] },
            { id: '', slug: 'pro', name: 'Pro', description: 'Navy sidebar with skill bars.', thumbnailUrl: null, industries: [], experienceLevels: [], style: 'Professional', layoutFlags: [], backgroundColor: '#1E2A3A', tags: [] },
            { id: '', slug: 'elegant', name: 'Elegant', description: 'Dark navy sidebar, spaced-letter headings.', thumbnailUrl: null, industries: [], experienceLevels: [], style: 'Elegant', layoutFlags: [], backgroundColor: '#1A1F36', tags: [] },
          ];
        }
      });
  }

  selectTemplate(slug: string): void {
    this.pipeline.selectedTemplateId.set(slug);
  }

  next(): void {
    this.pipeline.markStepDone(3);
    this.pipeline.goToStep(4);
  }
}
