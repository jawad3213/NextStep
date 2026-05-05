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
import { ProjectsComponent } from './components/projects/projects.component';
import { CertificationsComponent } from './components/certifications/certifications.component';
import { ExperienceComponent } from './components/experience/experience.component';
import { FormationComponent } from './components/formation/formation.component';
import { SkillsComponent } from './components/skills/skills.component';
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
    ProjectsComponent,
    CertificationsComponent,
    ExperienceComponent,
    FormationComponent,
    SkillsComponent,
    ResumeComponent,
    FormsModule,
    ReactiveFormsModule
  ],
  templateUrl: './profile.component.html',
  styleUrl: './profile.component.scss',
  encapsulation: ViewEncapsulation.None
})
export class UserProfileComponent implements OnInit, OnDestroy {
  profileService = inject(ProfileService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  
  private readonly destroy$ = new Subject<void>();
  private readonly autoSave$ = new Subject<void>();
  
  ngOnInit() {
    this.profileService.refreshProfile();

    // Recover step from URL query params
    this.route.queryParamMap.pipe(takeUntil(this.destroy$)).subscribe(params => {
      const stepId = params.get('step') as ProfileStepId;
      if (stepId && this.steps.some(s => s.id === stepId)) {
        this.profileService.setStep(stepId);
      }
    });

    // Professional Debounced Auto-save logic
    this.autoSave$.pipe(
      debounceTime(1500),
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
  showCompletionModal = signal(false);
  private lastSavedSnapshot: string = '';

  // Profile completion stats for the finish modal
  profileCompletion = computed(() => {
    const p = this.profile();
    let filled = 0;
    const total = 7;
    if (p.personal?.firstName || p.personal?.lastName) filled++;
    if (p.education?.length > 0) filled++;
    if (p.experience?.length > 0) filled++;
    if (p.skills?.length > 0) filled++;
    if (p.resume) filled++;
    if (p.projets?.length > 0) filled++;
    if (p.certifications?.length > 0) filled++;
    return { filled, total, percent: Math.round((filled / total) * 100) };
  });
  
  // Section States
  isAddingFormation = signal(false);
  isAddingExperience = signal(false);
  isAddingProject = signal(false);
  isAddingCertification = signal(false);
  isGeneratingAI = signal(false);
  isParsing = signal(false);
  parsingStatus = signal<'reading' | 'analyzing' | 'structuring'>('reading');
  parsingProgress = signal(0);
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
    type: 'Internship', description: ''
  });

  newProject = signal<Project>({
    id: '', title: '', description: '', stack: [], 
    githubUrl: '', demoUrl: '', isUniversity: false
  });

  newCertification = signal<Certification>({
    id: '', name: '', issuer: '', date: '', verificationUrl: ''
  });

  newLanguage = signal<any>({ id: '', name: '', level: 'B1' });

  isAddingExtracurricular = signal(false);
  newExtracurricular = signal<Experience>({
    id: '', title: '', company: '', city: '',
    startDate: '', endDate: '', current: false,
    type: 'Extracurricular', description: ''
  });

  activeProjectTab = signal<'projects' | 'extracurriculars'>('projects');
  activeSkillsTab = signal<'skills' | 'languages'>('skills');
  
  private breakpointObserver = inject(BreakpointObserver);

  isMobile = toSignal(
    this.breakpointObserver.observe([Breakpoints.Handset, Breakpoints.TabletPortrait])
      .pipe(map(result => result.matches)),
    { initialValue: false }
  );

  profile = this.profileService.profile;
  currentStep = this.profileService.currentStep;

  // Split experiences
  workExperiences = computed(() => this.profile().experience.filter((e: any) => e.type !== 'Extracurricular'));
  extracurriculars = computed(() => this.profile().experience.filter((e: any) => e.type === 'Extracurricular'));
  
  steps: { id: ProfileStepId, label: string }[] = [
    { id: 'coordonnees', label: 'Contact Info' },
    { id: 'experience', label: 'Experience' },
    { id: 'formation', label: 'Education' },
    { id: 'competences', label: 'Skills' },
    { id: 'resume', label: 'Summary' },
    { id: 'projets', label: 'Projects' },
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
      // Last step → Finish
      await this.finishProfile();
    }
  }

  async finishProfile() {
    await this.save();
    this.showCompletionModal.set(true);
  }

  goToDashboard() {
    this.showCompletionModal.set(false);
    this.router.navigate(['/dashboard']);
  }

  goToOffers() {
    this.showCompletionModal.set(false);
    this.router.navigate(['/cv']);
  }

  dismissCompletionModal() {
    this.showCompletionModal.set(false);
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
    this.autoSave$.next();
  }

  togglePreview() {
    this.isPreviewOpen.update(v => !v);
  }

  async save() {
    const currentData = JSON.stringify(this.profile().personal);
    
    // DIRTY CHECK: Only save if data has actually changed
    if (currentData === this.lastSavedSnapshot) {
      console.log('No changes detected, skipping save.');
      return;
    }

    this.isSaving.set(true);
    try {
      await this.profileService.savePersonalInfo(this.profile().personal);
      this.lastSavedSnapshot = currentData; // Update snapshot after successful save
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
        this.newExperience.update(v => ({ ...v, [field]: value }));
        break;
      case 'project':
        if (field === 'stack' && typeof value === 'string') {
          value = value.split(',').map(s => s.trim()).filter(s => s !== '');
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

  async onFileImported(event: Event) {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;

    this.isParsing.set(true);
    this.parsingStatus.set('reading');
    this.parsingProgress.set(10);

    try {
      // Step 1: Simulate reading (fast)
      await new Promise(resolve => setTimeout(resolve, 800));
      this.parsingStatus.set('analyzing');
      this.parsingProgress.set(40);

      // Step 2: Actual backend call (this takes most of the time)
      await this.profileService.importResume(file);
      
      this.parsingStatus.set('structuring');
      this.parsingProgress.set(85);
      
      // Step 3: Small delay to show completion of structuring
      await new Promise(resolve => setTimeout(resolve, 1000));
      this.parsingProgress.set(100);
      await new Promise(resolve => setTimeout(resolve, 400));

      this.showToast.set(true);
      setTimeout(() => this.showToast.set(false), 3000);
    } catch (error) {
      console.error('Import failed', error);
      // Optional: error toast
    } finally {
      this.isParsing.set(false);
      this.parsingProgress.set(0);
    }
  }

  importLinkedIn() {
    this.isImporting.set(true);
    // : Connecter à un vrai endpoint d'import LinkedIn backend
    console.warn("LinkedIn import is not yet implemented on the backend.");
    setTimeout(() => {
      this.isImporting.set(false);
    }, 1000);
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
        type: 'Internship', description: ''
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
        githubUrl: '', demoUrl: '', isUniversity: false
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
        type: 'Extracurricular', description: ''
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
    this.autoSave$.next();
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
}
