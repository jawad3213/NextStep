import { Component, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { DragDropModule, CdkDragDrop, moveItemInArray } from '@angular/cdk/drag-drop';
import { ProfileService } from './profile.service';
import { ProfileStepId, Profile } from './profile.types';

type SectionTitleKey = keyof NonNullable<Profile['sectionTitles']>;

// Sub-components
import { ProfileStepperComponent } from './stepper/profile-stepper.component';


@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [
    CommonModule,
    MatIconModule,
    MatTooltipModule,
    DragDropModule,
    ProfileStepperComponent,
  ],
  templateUrl: './profile.component.html',
  styleUrl: './profile.component.scss'
})
export class ProfileComponent {
  profileService = inject(ProfileService);
  
  // UI State
  isPreviewOpen = signal(false);
  isSaving = signal(false);
  lastSaved = signal<Date | null>(new Date());
  showToast = signal(false);
  
  // Section States
  isAddingFormation = signal(false);
  isAddingExperience = signal(false);
  isAddingProject = signal(false);
  isAddingCertification = signal(false);
  isGeneratingAI = signal(false);
  editingSection = signal<SectionTitleKey | null>(null);
  skillSearchQuery = signal('');
  filteredSuggestions = signal<string[]>([]);
  
  profile = this.profileService.profile;
  currentStep = this.profileService.currentStep;
  
  steps: { id: ProfileStepId, label: string }[] = [
    { id: 'coordonnees', label: 'Coordonnées' },
    { id: 'formation', label: 'Formation' },
    { id: 'experience', label: 'Expérience' },
    { id: 'competences', label: 'Compétences' },
    { id: 'resume', label: 'Résumé' },
    { id: 'projets', label: 'Projets' },
    { id: 'certifications', label: 'Certifications' }
  ];

  currentIndex = computed(() => this.steps.findIndex(s => s.id === this.currentStep()));
  
  nextStepName = computed(() => {
    const nextIdx = this.currentIndex() + 1;
    return nextIdx < this.steps.length ? this.steps[nextIdx].label : 'Terminer';
  });

  // Section Avancement (Progress in active section)
  sectionProgress = signal(45); // Mock 45% progress in current section

  // Step Logic
  goToStep(id: ProfileStepId) {
    this.profileService.setStep(id);
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  next() {
    const nextIdx = this.currentIndex() + 1;
    if (nextIdx < this.steps.length) {
      this.goToStep(this.steps[nextIdx].id);
    }
  }

  previous() {
    const prevIdx = this.currentIndex() - 1;
    if (prevIdx >= 0) {
      this.goToStep(this.steps[prevIdx].id);
    }
  }

  updateField(field: string, value: any) {
    this.isSaving.set(true);
    this.profileService.updateProfile({ 
      personal: { ...this.profile().personal, [field]: value } 
    });
    
    // Simulate auto-save feedback
    setTimeout(() => {
      this.isSaving.set(false);
      this.lastSaved.set(new Date());
    }, 1000);
  }

  togglePreview() {
    this.isPreviewOpen.update(v => !v);
  }

  save() {
    this.isSaving.set(true);
    // Simulate backend save
    setTimeout(() => {
      this.isSaving.set(false);
      this.lastSaved.set(new Date());
      this.showToast.set(true);
      
      // Hide toast after 3s
      setTimeout(() => {
        this.showToast.set(false);
      }, 3000);
    }, 600);
  }


  // LinkedIn Import State
  showImportBlock = signal(true);
  importLinkedIn() {
    // Simulate import
    setTimeout(() => this.showImportBlock.set(false), 800);
  }

  // Formation Methods
  toggleAddFormation() {
    this.isAddingFormation.update(v => !v);
  }

  saveFormation() {
    this.isSaving.set(true);
    setTimeout(() => {
      this.isSaving.set(false);
      this.isAddingFormation.set(false);
      this.showToast.set(true);
      setTimeout(() => this.showToast.set(false), 3000);
    }, 600);
  }

  // Experience Methods
  toggleAddExperience() {
    this.isAddingExperience.update(v => !v);
  }

  saveExperience() {
    this.isSaving.set(true);
    setTimeout(() => {
      this.isSaving.set(false);
      this.isAddingExperience.set(false);
      this.showToast.set(true);
      setTimeout(() => this.showToast.set(false), 3000);
    }, 600);
  }

  // Project Methods
  toggleAddProject() {
    this.isAddingProject.update(v => !v);
  }

  saveProject() {
    this.isSaving.set(true);
    setTimeout(() => {
      this.isSaving.set(false);
      this.isAddingProject.set(false);
      this.showToast.set(true);
      setTimeout(() => this.showToast.set(false), 3000);
    }, 600);
  }

  // Certification Methods
  toggleAddCertification() {
    this.isAddingCertification.update(v => !v);
  }

  saveCertification() {
    this.isSaving.set(true);
    setTimeout(() => {
      this.isSaving.set(false);
      this.isAddingCertification.set(false);
      this.showToast.set(true);
      setTimeout(() => this.showToast.set(false), 3000);
    }, 600);
  }

  removeCertification(id: string) {
    const current = this.profile().certifications.filter(c => c.id !== id);
    this.profileService.updateProfile({ certifications: current });
    this.save();
  }

  // Resume Methods
  updateResume(text: string) {
    this.profileService.updateProfile({ resume: text });
    this.save();
  }

  generateAIResume() {
    this.isGeneratingAI.set(true);
    
    // Simulation d'appel IA
    setTimeout(() => {
      const personal = this.profile().personal;
      const experience = this.profile().experience[0];
      const skills = this.profile().skills.slice(0, 3).map(s => s.name).join(', ');
      
      const generatedText = `Passionné par le développement logiciel avec une expertise en ${skills}. Actuellement ${personal.jobTitle} chez ${experience?.company || 'Freelance'}, j'ai développé une solide expérience dans la conception d'architectures robustes et d'interfaces utilisateur intuitives. Mon parcours m'a permis de maîtriser les cycles complets de développement et de contribuer à des projets innovants à fort impact technique.`;
      
      this.updateResume(generatedText);
      this.isGeneratingAI.set(false);
      this.showToast.set(true);
      setTimeout(() => this.showToast.set(false), 3000);
    }, 2000);
  }

  // Skills Methods
  predefinedSkills = [
    { category: 'Frontend', items: ['Angular', 'React', 'Vue.js', 'TypeScript', 'HTML/CSS', 'Tailwind'] },
    { category: 'Backend', items: ['Node.js', 'Spring Boot', 'Python', 'Java', 'C#', 'PHP'] },
    { category: 'DevOps & Cloud', items: ['Docker', 'Kubernetes', 'AWS', 'GCP', 'Azure', 'CI/CD'] },
    { category: 'Outils', items: ['Git', 'Figma', 'Jira', 'Postman', 'Linux'] }
  ];

  addSkill(skillName: string, category: string = 'Technique') {
    const currentSkills = this.profile().skills;
    if (!currentSkills.find(s => s.name.toLowerCase() === skillName.toLowerCase())) {
      const newSkill = { id: Date.now().toString(), name: skillName, category };
      this.profileService.updateProfile({ skills: [...currentSkills, newSkill] });
      this.save();
    }
    this.filteredSuggestions.set([]);
    this.skillSearchQuery.set('');
  }

  onSkillInput(event: Event) {
    const query = (event.target as HTMLInputElement).value.toLowerCase();
    this.skillSearchQuery.set(query);
    
    if (query.length < 1) {
      this.filteredSuggestions.set([]);
      return;
    }

    const allSkills = this.predefinedSkills.flatMap(s => s.items);
    const filtered = allSkills.filter(skill => 
      skill.toLowerCase().includes(query) && 
      !this.profile().skills.find(s => s.name.toLowerCase() === skill.toLowerCase())
    ).slice(0, 5); // Limit to 5 suggestions
    
    this.filteredSuggestions.set(filtered);
  }

  selectSuggestion(skill: string, inputElement: HTMLInputElement) {
    this.addSkill(skill);
    inputElement.value = '';
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
      this.save();
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
        this.save();
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

  removeSkill(skillId: string) {
    const currentSkills = this.profile().skills.filter(s => s.id !== skillId);
    this.profileService.updateProfile({ skills: currentSkills });
    this.save();
  }

  // Drag & Drop
  drop(event: CdkDragDrop<any[]>, type: 'education' | 'experience' | 'projets' | 'skills' | 'certifications') {
    if (event.previousIndex !== event.currentIndex) {
      const currentArray = [...this.profile()[type]];
      moveItemInArray(currentArray, event.previousIndex, event.currentIndex);
      this.profileService.updateProfile({ [type]: currentArray });
      this.save();
    }
  }
}
