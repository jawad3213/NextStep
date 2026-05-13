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

  getAgentStatus(agentName: string): 'done' | 'running' | 'todo' {
    const current = this.progress?.agentName;
    if (!current) return 'todo';

    const order = [
      'offer_analyzer',
      'profile_retriever',
      'skill_gap',
      'company_intel',
      'cv_optimizer',
      'cv_engine',
      'db_persist'
    ];

    const currentIndex = order.indexOf(current);
    const targetIndex = order.indexOf(agentName);

    if (targetIndex < currentIndex) return 'done';
    if (targetIndex === currentIndex) return 'running';
    return 'todo';
  }

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
