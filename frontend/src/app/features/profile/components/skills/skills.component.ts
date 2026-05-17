import { Component, Input, Output, EventEmitter, ViewEncapsulation } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { DragDropModule, CdkDragDrop } from '@angular/cdk/drag-drop';
import { Skill, Language } from '../../profile.types';

@Component({
  selector: 'app-skills',
  standalone: true,
  imports: [CommonModule, FormsModule, MatIconModule, DragDropModule],
  templateUrl: './skills.component.html',
  styleUrl: './skills.component.scss',
  encapsulation: ViewEncapsulation.None
})
export class SkillsComponent {
  @Input({ required: true }) skills: Skill[] = [];
  @Input({ required: true }) languages: Language[] = [];
  @Input({ required: true }) predefinedSkills: { category: string, items: string[] }[] = [];
  @Input({ required: true }) filteredSuggestions: { name: string, category: string }[] = [];
  @Input({ required: true }) newLanguage!: Language;

  @Output() addCustomSkill = new EventEmitter<{name: string, category: string, inputElement: HTMLInputElement}>();
  @Output() addSkill = new EventEmitter<{name: string, category: string}>();
  @Output() removeSkill = new EventEmitter<string>();
  @Output() onSkillInput = new EventEmitter<Event>();
  @Output() selectSuggestion = new EventEmitter<{suggestion: any, inputElement: HTMLInputElement}>();
  @Output() reorder = new EventEmitter<CdkDragDrop<any[]>>();

  @Output() addLanguage = new EventEmitter<void>();
  @Output() removeLanguage = new EventEmitter<string>();
  @Output() updateNewLanguage = new EventEmitter<{field: string, value: any}>();

  languageLevels = [
    { value: 'A1', label: 'A1' },
    { value: 'A2', label: 'A2' },
    { value: 'B1', label: 'B1' },
    { value: 'B2', label: 'B2' },
    { value: 'C1', label: 'C1' },
    { value: 'C2', label: 'C2' },
    { value: 'Native', label: 'Native' }
  ];

  popularLanguages = [
    'English', 'Anglais', 'French', 'Français', 'Spanish', 'Espagnol',
    'German', 'Allemand', 'Arabic', 'Arabe', 'Chinese', 'Chinois',
    'Italian', 'Italien', 'Portuguese', 'Portugais', 'Russian', 'Russe',
    'Japanese', 'Japonais', 'Dutch', 'Néerlandais', 'Turkish', 'Turc',
    'Korean', 'Coréen', 'Polish', 'Polonais', 'Swedish', 'Suédois',
    'Danish', 'Danois', 'Norwegian', 'Norvégien', 'Finnish', 'Finnois',
    'Greek', 'Grec', 'Hebrew', 'Hébreu', 'Hindi', 'Bengali', 'Thai',
    'Vietnamese', 'Vietnamien', 'Indonesian', 'Indonésien'
  ];

  @Input() langTitle: string = 'Languages';

  isSkillSelected(skillName: string): boolean {
    if (!this.skills || !skillName) return false;
    return this.skills.some(s => s.name?.toLowerCase() === skillName.toLowerCase());
  }

  onUpdateNewLanguage(field: string, value: any) {
    this.updateNewLanguage.emit({ field, value });
  }

  getLanguageBadgeColor(level: string): string {
    switch(level) {
      case 'Native':
      case 'Natif': return 'bg-emerald-100 text-emerald-700 border-emerald-200';
      case 'C2':
      case 'C1': return 'bg-blue-100 text-blue-700 border-blue-200';
      case 'B2':
      case 'B1': return 'bg-amber-100 text-amber-700 border-amber-200';
      default: return 'bg-slate-100 text-slate-600 border-slate-200';
    }
  }
}
