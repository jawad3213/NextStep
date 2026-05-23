import { Component, inject, signal, computed, OnInit, OnDestroy, ViewEncapsulation } from '@angular/core';
import { Router, ActivatedRoute } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { DragDropModule, CdkDragDrop, moveItemInArray } from '@angular/cdk/drag-drop';
import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { map } from 'rxjs/operators';
import { toSignal } from '@angular/core/rxjs-interop';
import { Subject, debounceTime, takeUntil } from 'rxjs';
import { ProfileService } from './profile.service';
import { ProfileStepId, Profile, Education, Experience, Project, Certification } from './profile.types';

type SectionTitleKey = keyof NonNullable<Profile['sectionTitles']>;

// Sub-components
import { ProfileStepperComponent } from './stepper/profile-stepper.component';
import { PersonalInfoComponent } from './components/personal-info/personal-info.component';

import { CertificationsComponent } from './components/certifications/certifications.component';
import { ExperienceComponent } from './components/experience/experience.component';
import { FormationComponent } from './components/formation/formation.component';
import { SkillsComponent } from './components/skills/skills.component';
import { ProjectsComponent } from './components/projects/projects.component';
import { ResumeComponent } from './components/resume/resume.component';


@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [
    CommonModule,
    MatIconModule,
    MatTooltipModule,
    DragDropModule,
    ProfileStepperComponent,
    PersonalInfoComponent,

    CertificationsComponent,
    ExperienceComponent,
    FormationComponent,
    SkillsComponent,
    ProjectsComponent,
    ResumeComponent,
    FormsModule,
    ReactiveFormsModule
  ],
  templateUrl: './profile.component.html',
  styleUrl: './profile.component.scss',
  encapsulation: ViewEncapsulation.None
})
export class UserProfileComponent implements OnInit, OnDestroy {
  private readonly profileUnlockedKey = 'nextstep_profile_unlocked';
  profileService = inject(ProfileService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  
  private readonly destroy$ = new Subject<void>();
  private readonly autoSave$ = new Subject<void>();
  
  ngOnInit() {
    this.profileService.refreshProfile();
    this.refreshOnboardingStatus();

    // Recover step from URL query params
    this.route.queryParamMap.pipe(takeUntil(this.destroy$)).subscribe(params => {
      const stepId = params.get('step') as ProfileStepId;
      if (stepId && this.steps.some(s => s.id === stepId)) {
        this.profileService.setStep(stepId);
      }
    });

    // Save on blur with a short debounce to coalesce rapid blur events
    this.autoSave$.pipe(
      debounceTime(300),
      takeUntil(this.destroy$)
    ).subscribe(() => {
      this.save();
    });
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }
  
  // UI State
  isPreviewOpen = signal(false);
  isSaving = signal(false);
  lastSaved = signal<Date | null>(new Date());
  showToast = signal(false);
  isForcedOnboarding = signal(false);
  toastMessage = signal('Changes saved');
  showInsufficientToast = signal(false);
  private lastSavedSnapshot: string = '';

  
  // Section States
  isAddingFormation = signal(false);
  isAddingExperience = signal(false);
  isAddingProject = signal(false);
  isAddingCertification = signal(false);
  isGeneratingAI = signal(false);
  isParsing = signal(false);
  isApplyingData = signal(false);
  parsingStatus = signal<'reading' | 'analyzing' | 'structuring'>('reading');
  parsingProgress = signal(0);
  terminalFeed = signal<{timestamp: string, status: string, message: string}[]>([]);
  editingSection = signal<SectionTitleKey | null>(null);
  skillSearchQuery = signal('');
  filteredSuggestions = signal<{name: string, category: string}[]>([]);

  // Form Signals for adding new items
  newFormation = signal<Education>({
    id: '', degree: '', institution: '', city: '', 
    startYear: '2024', endYear: '2024', current: false, 
    specialization: '', mention: 'Passable'
  });

  newExperience = signal<Experience>({
    id: '', title: '', company: '', city: '', 
    startDate: '', endDate: '', current: false, 
    type: 'Internship', description: '', taches: []
  });

  newProject = signal<Project>({
    id: '', title: '', description: '', stack: [], 
    githubUrl: '', demoUrl: '', isUniversity: false, taches: []
  });

  newCertification = signal<Certification>({
    id: '', name: '', issuer: '', date: '', verificationUrl: ''
  });

  newLanguage = signal<any>({ id: '', name: '', level: 'B1' });

  isAddingExtracurricular = signal(false);
  newExtracurricular = signal<Experience>({
    id: '', title: '', company: '', city: '',
    startDate: '', endDate: '', current: false,
    type: 'Extracurricular', description: '', taches: []
  });

  activeProjectTab = signal<'projects' | 'extracurriculars'>('projects');
  
  private breakpointObserver = inject(BreakpointObserver);

  isMobile = toSignal(
    this.breakpointObserver.observe([Breakpoints.Handset, Breakpoints.TabletPortrait])
      .pipe(map(result => result.matches)),
    { initialValue: false }
  );

  profile = this.profileService.profile;
  currentStep = this.profileService.currentStep;
  completionPercentage = this.profileService.completionPercentage;
  missingSections = this.profileService.missingSections;

  // Split experiences
  workExperiences = computed(() => this.profile().experience.filter((e: any) => e.type !== 'Extracurricular'));
  extracurriculars = computed(() => this.profile().experience.filter((e: any) => e.type === 'Extracurricular'));
  
  steps: { id: ProfileStepId, label: string }[] = [
    { id: 'coordonnees', label: 'Contact Info' },
    { id: 'experience', label: 'Experience' },
    { id: 'formation', label: 'Education' },
    { id: 'competences', label: 'Skills' },
    { id: 'projets', label: 'Projects' },
    { id: 'resume', label: 'Summary' },
    { id: 'certifications', label: 'Certifications' }
  ];

  currentIndex = computed(() => this.steps.findIndex(s => s.id === this.currentStep()));
  
  nextStepName = computed(() => {
    const nextIdx = this.currentIndex() + 1;
    return nextIdx < this.steps.length ? this.steps[nextIdx].label : 'Finish';
  });

  // Section Avancement (Progress in active section)
  // sectionProgress supprimé car non utilisé

  // Step Logic
  goToStep(id: ProfileStepId) {
    this.profileService.setStep(id);
    // Update URL query params without reloading
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { step: id },
      queryParamsHandling: 'merge'
    });
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  async next() {
    const nextIdx = this.currentIndex() + 1;
    if (nextIdx < this.steps.length) {
      this.goToStep(this.steps[nextIdx].id);
    } else {
      if (this.completionPercentage() < 85) {
        this.toastMessage.set('Complete at least 85% of your profile to continue');
        this.showToast.set(true);
        setTimeout(() => {
          this.showToast.set(false);
          this.toastMessage.set('Changes saved');
        }, 4000);
        return;
      }
      await this.finishProfile();
    }
  }

  async finishProfile() {
    if (this.isForcedOnboarding()) {
      await this.profileService.flushOnboardingData();
      this.showToast.set(true);
      setTimeout(() => this.showToast.set(false), 3000);
    } else {
      await this.save();
    }
    this.isForcedOnboarding.set(false);
    this.profileService.isOnboarding.set(false);
    localStorage.setItem(this.profileUnlockedKey, 'true');
    localStorage.removeItem('nextstep_soft_onboarding_done');
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { step: 'coordonnees' },
      queryParamsHandling: 'merge'
    });
  }

  async prev() {
    const prevIdx = this.currentIndex() - 1;
    if (prevIdx >= 0) {
      this.goToStep(this.steps[prevIdx].id);
    }
  }

  updateField(field: string, value: any) {
    const currentPersonal = { ...this.profile().personal };
    (currentPersonal as any)[field] = value;
    this.profileService.updateProfile({ personal: currentPersonal });
  }

  onFieldBlur(field: string, value: any) {
    const currentPersonal = { ...this.profile().personal };
    (currentPersonal as any)[field] = value;
    this.profileService.updateProfile({ personal: currentPersonal });
  }

  togglePreview() {
    this.isPreviewOpen.update(v => !v);
  }

  async save() {
    if (this.isForcedOnboarding()) return;

    const snapshot = {
      personal: this.profile().personal,
      resume: this.profile().resume,
      sectionTitles: this.profile().sectionTitles
    };
    const currentData = JSON.stringify(snapshot);
    
    if (currentData === this.lastSavedSnapshot) {
      console.log('No changes detected, skipping save.');
      return;
    }

    this.isSaving.set(true);
    try {
      await this.profileService.savePersonalInfo(this.profile().personal);
      this.lastSavedSnapshot = currentData;
      this.lastSaved.set(new Date());
      this.showToast.set(true);
      setTimeout(() => this.showToast.set(false), 3000);
    } catch (error) {
      console.error('Erreur sauvegarde:', error);
    } finally {
      this.isSaving.set(false);
    }
  }

  updateNewItem(type: 'formation' | 'experience' | 'project' | 'certification', field: string, value: any) {
    switch(type) {
      case 'formation': 
        this.newFormation.update(v => ({ ...v, [field]: value }));
        break;
      case 'experience':
        if (field === 'taches' && typeof value === 'string') {
          value = value.split(/\r?\n/).map(s => s.trim()).filter(s => s !== '');
        }
        this.newExperience.update(v => ({ ...v, [field]: value }));
        break;
      case 'project':
        if (field === 'stack' && typeof value === 'string') {
          value = value.split(',').map(s => s.trim()).filter(s => s !== '');
        }
        if (field === 'taches' && typeof value === 'string') {
          value = value.split(/\r?\n/).map(s => s.trim()).filter(s => s !== '');
        }
        this.newProject.update(v => ({ ...v, [field]: value }));
        break;
      case 'certification':
        this.newCertification.update(v => ({ ...v, [field]: value }));
        break;
    }
  }


  // LinkedIn Import State
  showImportBlock = signal(true);
  isImporting = signal(false);
  showLinkedInModal = signal(false);
  linkedinUrl = signal('');
  linkedinRawText = signal('');

  parsingEvents = this.profileService.parsingEvents;
  importSummary = this.profileService.lastImportSummary;
  parsingStageCopy = computed(() => {
    switch (this.parsingStatus()) {
      case 'analyzing':
        return {
          headline: 'AI Extraction Engine',
          description: 'We are analyzing the structure of your resume and grouping it by profile step.'
        };
      case 'structuring':
        return {
          headline: 'Preparing Your Profile',
          description: 'The extracted information is being organized so each profile section can be filled automatically.'
        };
      default:
        return {
          headline: 'AI Extraction Engine',
          description: 'Our agents are scanning your document for key experiences and skills.'
        };
    }
  });
  importSummaryCards = computed(() => {
    const summary = this.importSummary();
    if (!summary) return [];

    return [
      { key: 'contact', label: 'Contact fields', count: summary.personalFields },
      { key: 'experience', label: 'Experience', count: summary.experienceCount },
      { key: 'education', label: 'Education', count: summary.educationCount },
      { key: 'skills', label: 'Skills', count: summary.skillCount + summary.languageCount },
      { key: 'projects', label: 'Projects', count: summary.projectCount },
      { key: 'certifications', label: 'Certifications', count: summary.certificationCount },
      { key: 'summary', label: 'Summary', count: summary.hasSummary ? 1 : 0 }
    ];
  });

  async onFileImported(event: any) {
    const input = event.target as HTMLInputElement;
    const file = event.target?.files?.[0] || input?.files?.[0];
    if (!file) return;

    this.isParsing.set(true);
    this.isApplyingData.set(false);
    this.parsingStatus.set('reading');
    this.parsingProgress.set(10);

    try {
      // Small UX delay to show the start
      await new Promise(resolve => setTimeout(resolve, 800));
      this.parsingStatus.set('analyzing');
      this.parsingProgress.set(30);

      // Actual Import call (emits events into parsingEvents signal)
      await this.profileService.importResume(file);

      this.parsingStatus.set('structuring');
      this.parsingProgress.set(90);
      
      // Delay before closing modal to show the last feed lines
      await new Promise(resolve => setTimeout(resolve, 1500));
      
      this.parsingProgress.set(100);
      this.isParsing.set(false);
      this.isApplyingData.set(true);
      
      // Keep skeletons for a moment to signify data integration
      setTimeout(async () => {
        this.isApplyingData.set(false);
        await this.afterImportCheck();
      }, 2000);

    } catch (error) {
      console.error('Import failed', error);
      this.isParsing.set(false);
      this.isApplyingData.set(false);
    } finally {
      this.parsingProgress.set(0);
      if (input) {
        input.value = '';
      }
    }
  }

  openLinkedInModal() {
    this.linkedinUrl.set('');
    this.linkedinRawText.set('');
    this.showLinkedInModal.set(true);
  }

  closeLinkedInModal() {
    this.showLinkedInModal.set(false);
  }

  async executeLinkedInImport() {
    const url = this.linkedinUrl().trim();

    if (!url) {
      alert("Veuillez entrer votre URL LinkedIn.");
      return;
    }

    this.showLinkedInModal.set(false);
    this.isParsing.set(true);
    this.isApplyingData.set(false);
    this.parsingStatus.set('reading');
    this.parsingProgress.set(10);

    try {
      await new Promise(resolve => setTimeout(resolve, 800));
      this.parsingStatus.set('analyzing');
      this.parsingProgress.set(40);

      // Call our robust backend import via service
      await this.profileService.importLinkedIn(url, '');

      this.parsingStatus.set('structuring');
      this.parsingProgress.set(90);
      
      await new Promise(resolve => setTimeout(resolve, 1500));
      
      this.parsingProgress.set(100);
      this.isParsing.set(false);
      this.isApplyingData.set(true);
      
      setTimeout(async () => {
        this.isApplyingData.set(false);
        await this.afterImportCheck();
      }, 2000);

    } catch (error) {
      console.error('LinkedIn import failed', error);
      this.isParsing.set(false);
      this.isApplyingData.set(false);
      alert("Une erreur s'est produite lors de l'import. Veuillez reessayer avec une URL LinkedIn valide.");
    } finally {
      this.parsingProgress.set(0);
    }
  }

  private async afterImportCheck() {
    if (!this.isForcedOnboarding()) return;
    const pct = this.completionPercentage();
    if (pct >= 85) {
      await this.finishProfile();
    } else {
      this.toastMessage.set(`Profile ${pct}% filled. Complete the remaining steps to finish.`);
      this.showToast.set(true);
      setTimeout(() => {
        this.showToast.set(false);
        this.toastMessage.set('Changes saved');
      }, 4000);
    }
  }

  // Formation Methods
  toggleAddFormation() {
    this.isAddingFormation.update(v => !v);
  }

  async saveFormation() {
    this.isSaving.set(true);
    try {
      if (this.newFormation().id) {
        await this.profileService.updateEducation(this.newFormation());
      } else {
        await this.profileService.addEducation(this.newFormation());
      }
      this.isAddingFormation.set(false);
      this.newFormation.set({
        id: '', degree: '', institution: '', city: '', 
        startYear: '2024', endYear: '2024', current: false, 
        specialization: '', mention: 'Passable'
      });
      this.showToast.set(true);
      setTimeout(() => this.showToast.set(false), 3000);
    } catch (error) {
      console.error('Erreur formation:', error);
    } finally {
      this.isSaving.set(false);
    }
  }

  // Experience Methods
  toggleAddExperience() {
    this.isAddingExperience.update(v => !v);
  }

  async saveExperience() {
    this.isSaving.set(true);
    try {
      if (this.newExperience().id) {
        await this.profileService.updateExperience(this.newExperience());
      } else {
        await this.profileService.addExperience(this.newExperience());
      }
      this.isAddingExperience.set(false);
      this.newExperience.set({
        id: '', title: '', company: '', city: '', 
        startDate: '', endDate: '', current: false, 
        type: 'Internship', description: '', taches: []
      });
      this.showToast.set(true);
      setTimeout(() => this.showToast.set(false), 3000);
    } catch (error) {
      console.error('Erreur expérience:', error);
    } finally {
      this.isSaving.set(false);
    }
  }

  // Project Methods
  toggleAddProject() {
    this.isAddingProject.update(v => !v);
  }

  async saveProject() {
    this.isSaving.set(true);
    try {
      if (this.newProject().id) {
        await this.profileService.updateProject(this.newProject());
      } else {
        await this.profileService.addProject(this.newProject());
      }
      this.isAddingProject.set(false);
      this.newProject.set({
        id: '', title: '', description: '', stack: [], 
        githubUrl: '', demoUrl: '', isUniversity: false, taches: []
      });
      this.showToast.set(true);
      setTimeout(() => this.showToast.set(false), 3000);
    } catch (error) {
      console.error('Erreur projet:', error);
    } finally {
      this.isSaving.set(false);
    }
  }

  // Extracurricular Methods
  toggleAddExtracurricular() {
    this.isAddingExtracurricular.update(v => !v);
  }

  updateNewExtracurricular(field: string, value: any) {
    this.newExtracurricular.update(v => ({ ...v, [field]: value }));
  }

  async saveExtracurricular() {
    this.isSaving.set(true);
    try {
      if (this.newExtracurricular().id) {
        await this.profileService.updateExperience(this.newExtracurricular());
      } else {
        await this.profileService.addExperience(this.newExtracurricular());
      }
      this.isAddingExtracurricular.set(false);
      this.newExtracurricular.set({
        id: '', title: '', company: '', city: '', 
        startDate: '', endDate: '', current: false, 
        type: 'Extracurricular', description: '', taches: []
      });
      this.showToast.set(true);
      setTimeout(() => this.showToast.set(false), 3000);
    } catch (error) {
      console.error('Erreur activité parascolaire:', error);
    } finally {
      this.isSaving.set(false);
    }
  }

  editExtracurricular(item: Experience) {
    this.newExtracurricular.set({ ...item });
    this.isAddingExtracurricular.set(true);
  }

  async deleteExtracurricular(id: string) {
    await this.profileService.deleteExperience(id);
  }

  onProjectTabChange(tab: 'projects' | 'extracurriculars') {
    this.activeProjectTab.set(tab);
  }

  // Certification Methods
  toggleAddCertification() {
    this.isAddingCertification.update(v => !v);
  }

  async saveCertification() {
    this.isSaving.set(true);
    try {
      if (this.newCertification().id) {
        await this.profileService.updateCertification(this.newCertification());
      } else {
        await this.profileService.addCertification(this.newCertification());
      }
      this.isAddingCertification.set(false);
      this.newCertification.set({
        id: '', name: '', issuer: '', date: '', verificationUrl: ''
      });
      this.showToast.set(true);
      setTimeout(() => this.showToast.set(false), 3000);
    } catch (error) {
      console.error('Erreur certification:', error);
    } finally {
      this.isSaving.set(false);
    }
  }

  editItem(item: any, type: 'formation' | 'experience' | 'project' | 'certification') {
    switch(type) {
      case 'formation':
        this.newFormation.set({ ...item });
        this.isAddingFormation.set(true);
        break;
      case 'experience':
        this.newExperience.set({ ...item });
        this.isAddingExperience.set(true);
        break;
      case 'project':
        this.newProject.set({ ...item });
        this.isAddingProject.set(true);
        break;
      case 'certification':
        this.newCertification.set({ ...item });
        this.isAddingCertification.set(true);
        break;
    }
  }

  async duplicateItem(item: any, type: 'formation' | 'experience' | 'project' | 'certification') {
    this.isSaving.set(true);
    try {
      const clonedItem = { ...item, id: undefined }; // Retirer l'ID pour forcer la création
      switch(type) {
        case 'formation':
          await this.profileService.addEducation(clonedItem);
          break;
        case 'experience':
          await this.profileService.addExperience(clonedItem);
          break;
        case 'project':
          await this.profileService.addProject(clonedItem);
          break;
        case 'certification':
          await this.profileService.addCertification(clonedItem);
          break;
      }
      this.showToast.set(true);
      setTimeout(() => this.showToast.set(false), 3000);
    } catch(err) {
      console.error('Erreur lors de la duplication', err);
    } finally {
      this.isSaving.set(false);
    }
  }

  async removeCertification(id: string) {
    await this.deleteItem(id, 'certifications');
  }

  async removeSkill(id: string) {
    await this.deleteItem(id, 'skills');
  }

  async deleteItem(id: string, type: 'education' | 'experience' | 'projets' | 'skills' | 'certifications') {
    try {
      switch(type) {
        case 'education': await this.profileService.deleteEducation(id); break;
        case 'experience': await this.profileService.deleteExperience(id); break;
        case 'projets': await this.profileService.deleteProject(id); break;
        case 'skills': await this.profileService.deleteSkill(id); break;
        case 'certifications': await this.profileService.deleteCertification(id); break;
      }
    } catch (error) {
      console.error(`Erreur suppression ${type}:`, error);
    }
  }

  // Languages Methods
  updateNewLanguage(field: string, value: any) {
    this.newLanguage.update(v => ({ ...v, [field]: value }));
  }

  async saveLanguage() {
    this.isSaving.set(true);
    try {
      if (this.newLanguage().id) {
        await this.profileService.updateLanguage(this.newLanguage());
      } else {
        await this.profileService.addLanguage(this.newLanguage());
      }
      this.newLanguage.set({ id: '', name: '', level: 'B1' });
      this.showToast.set(true);
      setTimeout(() => this.showToast.set(false), 3000);
    } catch (error) {
      console.error('Erreur langue:', error);
    } finally {
      this.isSaving.set(false);
    }
  }

  async removeLanguage(id: string) {
    await this.profileService.deleteLanguage(id);
  }

  // Resume Methods
  updateResume(text: string) {
    this.profileService.updateProfile({ resume: text });
  }

  onResumeBlur() {
  }

  async generateAIResume() {
    this.isGeneratingAI.set(true);
    
    try {
      // Appel au vrai endpoint backend qui proxifie vers l'agent Python
      const generatedText = await this.profileService.generateResume(this.profile());
      this.updateResume(generatedText);
      this.showToast.set(true);
      setTimeout(() => this.showToast.set(false), 3000);
    } catch (e) {
      console.error(e);
    } finally {
      this.isGeneratingAI.set(false);
    }
  }

  // Skills Methods
  predefinedSkills: { category: string, items: string[] }[] = [];

  constructor() {
    // Initialisation
    this.loadKeywords();
  }

  async loadKeywords() {
    const keywords = await this.profileService.getKeywords();
    // Grouper les mots clés par catégorie
    const groups: { [key: string]: string[] } = {};
    for (const kw of keywords) {
      if (!groups[kw.categorie]) groups[kw.categorie] = [];
      groups[kw.categorie].push(kw.mot);
    }
    
    this.predefinedSkills = Object.keys(groups).map(k => ({
      category: k,
      items: groups[k]
    }));
  }

  async addSkill(skillName: string, category: string = 'Technique') {
    // Clear suggestions immediately for responsive UI
    this.filteredSuggestions.set([]);
    this.skillSearchQuery.set('');
    
    try {
      await this.profileService.addSkill({ id: '', name: skillName, category });
      this.showToast.set(true);
      setTimeout(() => this.showToast.set(false), 3000);
    } catch (error) {
      console.error('Erreur compétence:', error);
    }
  }

  onSkillInput(event: Event) {
    const query = (event.target as HTMLInputElement).value.toLowerCase();
    this.skillSearchQuery.set(query);
    
    if (query.length < 1) {
      this.filteredSuggestions.set([]);
      return;
    }

    const suggestions: {name: string, category: string}[] = [];
    this.predefinedSkills.forEach(cat => {
      cat.items.forEach(item => {
        if (item.toLowerCase().includes(query) && !this.isSkillSelected(item)) {
          suggestions.push({ name: item, category: cat.category });
        }
      });
    });

    this.filteredSuggestions.set(suggestions);
  }

  async selectSuggestion(suggestion: {name: string, category: string}, inputElement: HTMLInputElement) {
    // Clear input and dropdown immediately
    inputElement.value = '';
    this.filteredSuggestions.set([]);
    this.skillSearchQuery.set('');
    
    // Then add the skill (async)
    await this.addSkill(suggestion.name, suggestion.category);
  }

  isSkillSelected(name: string): boolean {
    return this.profile().skills.some(s => s.name.toLowerCase() === name.toLowerCase());
  }

  getSectionTitle(section: SectionTitleKey, defaultTitle: string): string {
    return this.profile().sectionTitles?.[section] || defaultTitle;
  }

  startEditingSection(section: SectionTitleKey) {
    this.editingSection.set(section);
  }

  updateSectionTitle(section: SectionTitleKey, newTitle: string) {
    if (newTitle.trim()) {
      const currentTitles = this.profile().sectionTitles || {};
      this.profileService.updateProfile({
        sectionTitles: {
          ...currentTitles,
          [section]: newTitle.trim()
        }
      });
      this.autoSave$.next();
    }
    this.editingSection.set(null);
  }

  triggerPhotoUpload(input: HTMLInputElement) {
    input.click();
  }

  onPhotoSelected(event: Event) {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (file) {
      const reader = new FileReader();
      reader.onload = (e) => {
        const photoUrl = e.target?.result as string;
        this.updateField('photoUrl', photoUrl);
        this.autoSave$.next();
      };
      reader.readAsDataURL(file);
    }
  }

  addCustomSkill(name: string, category: string, nameInput: HTMLInputElement) {
    if (name.trim()) {
      this.addSkill(name.trim(), category);
      nameInput.value = ''; // Reset input after adding
    }
  }

  // Logic moved to deleteItem

  // Drag & Drop
  drop(event: CdkDragDrop<any[]>, type: 'education' | 'experience' | 'projets' | 'skills' | 'certifications') {
    if (event.previousIndex !== event.currentIndex) {
      const currentArray = [...this.profile()[type]];
      moveItemInArray(currentArray, event.previousIndex, event.currentIndex);
      this.profileService.updateProfile({ [type]: currentArray });
      this.autoSave$.next();
    }
  }

  private refreshOnboardingStatus() {
    const profileUnlocked = localStorage.getItem(this.profileUnlockedKey) === 'true';
    const forced = !profileUnlocked;
    this.isForcedOnboarding.set(forced);
    this.profileService.isOnboarding.set(forced);
  }
}
