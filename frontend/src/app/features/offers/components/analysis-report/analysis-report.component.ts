import { Component, inject, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { OffersService } from '../../offers.service';

@Component({
  selector: 'app-analysis-report',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './analysis-report.component.html',
  styleUrls: ['./analysis-report.component.scss']
})
export class AnalysisReportComponent {
  readonly offersService = inject(OffersService);

  @Output() back = new EventEmitter<void>();
  @Output() generateCv = new EventEmitter<void>();

  onBack() {
    this.back.emit();
  }

  onGenerateCv() {
    this.generateCv.emit();
  }
}
