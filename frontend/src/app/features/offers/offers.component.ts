import { Component, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { OffersService, JobOffer } from './offers.service';

// Import our modular child components
import { AnalysisReportComponent } from './components/analysis-report/analysis-report.component';
import { TemplateSelectorComponent } from './components/template-selector/template-selector.component';
import { ResumeEditorComponent } from './components/resume-editor/resume-editor.component';

@Component({
  selector: 'app-offers',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    AnalysisReportComponent,
    TemplateSelectorComponent,
    ResumeEditorComponent
  ],
  templateUrl: './offers.component.html',
  styleUrls: ['./offers.component.scss']
})
export class OffersComponent {
  // Inject the state management service
  readonly offersService = inject(OffersService);

  // Modular stage progression: 'dashboard' | 'new-offer' | 'analysis' | 'templates' | 'editor'
  readonly currentStage = signal<'dashboard' | 'new-offer' | 'analysis' | 'templates' | 'editor'>('dashboard');

  // Search & filter panel states
  readonly searchTerm = signal<string>('');
  readonly filterContract = signal<string>('');
  readonly filterStatus = signal<string>('');
  readonly viewMode = signal<'list' | 'grid'>('list');
  readonly isSyncing = signal<boolean>(false);

  // New offer dedicated page controls (Stage: 'new-offer')
  readonly activeFormTab = signal<'paste' | 'import' | 'url'>('paste');
  readonly newTitle = signal<string>('');
  readonly newCompany = signal<string>('');
  readonly newLocation = signal<string>('');
  readonly newContractType = signal<string>('Stage');
  readonly newRawText = signal<string>('');
  readonly newUrl = signal<string>('');
  readonly selectedFileName = signal<string>('');
  readonly isDragging = signal<boolean>(false);

  // Visual layout selection state
  readonly selectedTemplate = signal<string>('modern');

  // Word counter for text analyzer
  readonly wordCount = computed(() => {
    const text = this.newRawText().trim();
    if (!text) return 0;
    return text.split(/\s+/).filter(word => word.length > 0).length;
  });

  // Computed pipeline signals
  readonly pipelineProgress = computed(() => {
    return this.offersService.activePipelineStep() * 25;
  });

  readonly pipelineStep = computed(() => {
    const step = this.offersService.activePipelineStep();
    switch (step) {
      case 1: return "Analyse du Poste (offer_analyzer_node)";
      case 2: return "Récupération du Profil (profile_retriever_node)";
      case 3: return "Calcul du Skill Gap (skill_gap_node)";
      case 4: return "Génération du CV (optimisation_node)";
      default: return "En attente";
    }
  });

  readonly pipelineLogs = computed(() => {
    return this.offersService.pipelineLog().map(l => l.message);
  });

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
    this.viewMode.set(mode);
  }

  setFormTab(tab: 'paste' | 'import' | 'url') {
    this.activeFormTab.set(tab);
  }

  // NAVIGATION METHODS
  goToStage(stage: 'dashboard' | 'new-offer' | 'analysis' | 'templates' | 'editor') {
    this.currentStage.set(stage);
    if (stage === 'dashboard') {
      this.offersService.activeOfferId.set(null);
    }
  }

  onTemplateSelected(templateId: string) {
    this.selectedTemplate.set(templateId);
    this.currentStage.set('editor');
  }

  // FILE DRAG AND DROP HANDLERS
  onDragOver(event: DragEvent) {
    event.preventDefault();
    this.isDragging.set(true);
  }

  onDragLeave(event: DragEvent) {
    event.preventDefault();
    this.isDragging.set(false);
  }

  onDrop(event: DragEvent) {
    event.preventDefault();
    this.isDragging.set(false);
    if (event.dataTransfer?.files && event.dataTransfer.files.length > 0) {
      const file = event.dataTransfer.files[0];
      this.selectedFileName.set(file.name);
    }
  }

  onFileSelected(event: any) {
    if (event.target.files && event.target.files.length > 0) {
      this.selectedFileName.set(event.target.files[0].name);
    }
  }

  clearSelectedFile() {
    this.selectedFileName.set('');
  }

  // EVALUATE DESCRIPTION STRENGTH (TEXT QUALITY INDICATOR)
  getQualityText(): 'WEAK' | 'MEDIUM' | 'EXCELLENT' {
    const count = this.wordCount();
    if (count === 0) return 'WEAK';
    if (count < 60) return 'WEAK';
    if (count < 180) return 'MEDIUM';
    return 'EXCELLENT';
  }

  getQualityClass(): 'quality-weak' | 'quality-medium' | 'quality-strong' {
    const q = this.getQualityText();
    if (q === 'WEAK') return 'quality-weak';
    if (q === 'MEDIUM') return 'quality-medium';
    return 'quality-strong';
  }

  // VALIDATE NEW OFFER DATA
  isFormValid(): boolean {
    const isBaseValid = !!(this.newTitle().trim() && this.newCompany().trim() && this.newLocation().trim());
    if (!isBaseValid) return false;

    if (this.activeFormTab() === 'paste') {
      return this.newRawText().trim().length > 10;
    }
    if (this.activeFormTab() === 'import') {
      return !!this.selectedFileName();
    }
    if (this.activeFormTab() === 'url') {
      return this.newUrl().trim().startsWith('http');
    }
    return false;
  }

  // ADD NEW OFFER AND START ANALYSIS PIPELINE
  async submitNewOffer() {
    if (!this.isFormValid()) return;

    const tagsList = ['ATS', this.newTitle().split(' ')[0]];
    if (this.newTitle().toLowerCase().includes('react')) tagsList.push('React');
    if (this.newTitle().toLowerCase().includes('java')) tagsList.push('Java', 'Spring');
    if (this.newTitle().toLowerCase().includes('data')) tagsList.push('Python', 'SQL');

    const created = await this.offersService.createOfferApi({
      title: this.newTitle().trim(),
      company: this.newCompany().trim(),
      location: this.newLocation().trim(),
      contractType: this.newContractType(),
      tags: tagsList,
      rawText: this.newRawText(),
      url: this.newUrl()
    });

    // REDIRECT back to the dashboard landing page to view live loading telemetry logs
    this.goToStage('dashboard');

    // Launch pipeline orchestration simulation
    this.runPipeline(created.id);

    // Reset form values
    this.newTitle.set('');
    this.newCompany.set('');
    this.newLocation.set('');
    this.newRawText.set('');
    this.newUrl.set('');
    this.selectedFileName.set('');
  }

  // EXECUTE SIMULATED LANGRAPH ANALYSIS PIPELINE
  runPipeline(id: string) {
    this.offersService.triggerInteractivePipeline(id, () => {
      // SET ACTIVE OPPORTUNITY ID
      this.offersService.activeOfferId.set(id);
      // REDIRECT TO STAGE 2: SEMANTIC ANALYSIS REPORT
      this.currentStage.set('analysis');
    });
  }

  // CLICK AN OPPORTUNITY CARD
  selectOffer(id: string) {
    if (this.offersService.pipelineRunning() && this.offersService.runningOfferId() === id) return;
    
    this.offersService.activeOfferId.set(id);
    this.currentStage.set('analysis');
  }

  // GENERATE CV LINK DIRECTLY FROM DASHBOARD
  generateCvDirect(event: Event, offer: JobOffer) {
    event.stopPropagation();
    this.offersService.activeOfferId.set(offer.id);
    this.currentStage.set('templates');
  }

  // SYNC ACTION ANIMATION
  triggerGlobalSync() {
    this.isSyncing.set(true);
    setTimeout(() => {
      this.isSyncing.set(false);
    }, 1500);
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

  getFormattedTime(): string {
    const d = new Date();
    return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' });
  }
}
