import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PipelineStateService } from '../../../../services/pipeline-state.service';
import { ResumeEditorComponent } from '../resume-editor/resume-editor.component';

@Component({
  selector: 'app-step-generation',
  standalone: true,
  imports: [CommonModule, ResumeEditorComponent],
  templateUrl: './step-generation.component.html',
  styleUrl: './step-generation.component.scss'
})
export class StepGenerationComponent {
  pipeline = inject(PipelineStateService);

  private readonly templateMap: Record<string, string> = {
    modern: 'modern',
    classic: 'professional',
    executive: 'professional',
    pro: 'modern',
    elegant: 'elegant',
  };

  get selectedTemplate(): string {
    const id = this.pipeline.selectedTemplateId();
    return this.templateMap[id] || 'modern';
  }

  continueToResults(): void {
    this.pipeline.markStepDone(4);
    this.pipeline.goToStep(5);
  }

  goBack(): void {
    this.pipeline.goToStep(3);
  }
}
