import { Component, inject, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { OffersService, JobOffer } from './offers.service';
import { PipelineStateService } from '../../services/pipeline-state.service';
import { PipelineStepperComponent } from './components/pipeline-stepper/pipeline-stepper.component';

@Component({
  selector: 'app-offers',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    PipelineStepperComponent
  ],
  templateUrl: './offers.component.html',
  styleUrls: ['./offers.component.scss']
})
export class OffersComponent {
  readonly offersService = inject(OffersService);
  readonly pipeline = inject(PipelineStateService);

  // Search & filter panel states
  readonly searchTerm = this.offersService.searchTerm;
  readonly filterContract = this.offersService.filterContract;
  readonly filterStatus = this.offersService.filterStatus;
  readonly viewMode = this.offersService.viewMode;
  readonly isSyncing = this.offersService.isSyncing;

  // Reactive filtered offers selector
  readonly filteredOffers = computed(() => {
    const search = this.searchTerm().trim().toLowerCase();
    const contract = this.filterContract();
    const status = this.filterStatus();

    return this.offersService.offers().filter(offer => {
      const matchesSearch = !search || 
        offer.title.toLowerCase().includes(search) ||
        offer.company.toLowerCase().includes(search) ||
        offer.location.toLowerCase().includes(search) ||
        offer.tags.some(t => t.toLowerCase().includes(search));

      const matchesContract = !contract || offer.contractType === contract;
      const matchesStatus = !status || offer.status === status;

      return matchesSearch && matchesContract && matchesStatus;
    });
  });

  // SETTERS & STATE TRANSITIONS
  setViewMode(mode: 'list' | 'grid') {
    this.offersService.viewMode.set(mode);
  }

  // SYNC ACTION ANIMATION
  triggerGlobalSync() {
    this.offersService.isSyncing.set(true);
    setTimeout(() => {
      this.offersService.isSyncing.set(false);
    }, 1500);
  }

  // PIPELINE LAUNCH
  openPipeline() {
    this.pipeline.openFlow();
  }

  // AVATAR BUBBLE BG GRADIENTS
  getBubbleGradient(company: string): string {
    const colors = [
      'linear-gradient(135deg, #1A91F0 0%, #0C1986 100%)',
      'linear-gradient(135deg, #00B0FF 0%, #00B0FF 100%)',
      'linear-gradient(135deg, #00E676 0%, #00A250 100%)',
      'linear-gradient(135deg, #FF9100 0%, #FF6D00 100%)',
      'linear-gradient(135deg, #651FFF 0%, #4615B2 100%)',
      'linear-gradient(135deg, #D500F9 0%, #9C00AF 100%)',
      'linear-gradient(135deg, #37474F 0%, #212121 100%)'
    ];
    let sum = 0;
    for (let i = 0; i < company.length; i++) sum += company.charCodeAt(i);
    return colors[sum % colors.length];
  }

  getInitials(company: string): string {
    if (!company) return 'CO';
    const parts = company.trim().split(' ');
    if (parts.length > 1) {
      return (parts[0][0] + parts[1][0]).toUpperCase();
    }
    return company.substring(0, 2).toUpperCase();
  }
}
