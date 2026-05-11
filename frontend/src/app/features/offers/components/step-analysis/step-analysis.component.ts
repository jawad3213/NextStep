import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PipelineStateService } from '../../../../services/pipeline-state.service';

@Component({
  selector: 'app-step-analysis',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './step-analysis.component.html',
  styleUrl: './step-analysis.component.scss'
})
export class StepAnalysisComponent {
  pipeline = inject(PipelineStateService);

  get result() { return this.pipeline.pipelineResult(); }
  get progress() { return this.pipeline.currentAgentProgress(); }
  get error() { return this.pipeline.pipelineError(); }

  goBack(): void {
    this.pipeline.pipelineError.set(null);
    this.pipeline.goToStep(1);
  }

  goToSkillGap(): void {
    this.pipeline.goToStep(3);
  }

  retry(): void {
    this.pipeline.pipelineError.set(null);
    this.pipeline.goToStep(1);
  }
}
