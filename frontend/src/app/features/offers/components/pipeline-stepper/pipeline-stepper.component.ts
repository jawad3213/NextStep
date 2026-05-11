import { Component, inject, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PipelineStateService } from '../../../../services/pipeline-state.service';
import { SignalRService } from '../../../../services/signalr.service';
import { StepSubmitComponent } from '../step-submit/step-submit.component';
import { StepAnalysisComponent } from '../step-analysis/step-analysis.component';
import { StepTemplateComponent } from '../step-template/step-template.component';
import { StepGenerationComponent } from '../step-generation/step-generation.component';
import { StepResultsComponent } from '../step-results/step-results.component';

@Component({
  selector: 'app-pipeline-stepper',
  standalone: true,
  imports: [
    CommonModule,
    StepSubmitComponent,
    StepAnalysisComponent,
    StepTemplateComponent,
    StepGenerationComponent,
    StepResultsComponent
  ],
  templateUrl: './pipeline-stepper.component.html',
  styleUrl: './pipeline-stepper.component.scss'
})
export class PipelineStepperComponent implements OnDestroy {
  pipeline = inject(PipelineStateService);
  private signalR = inject(SignalRService);

  ngOnDestroy(): void {
    this.signalR.disconnect();
  }
}
