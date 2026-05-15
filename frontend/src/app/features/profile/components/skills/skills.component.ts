import { Component, Input, Output, EventEmitter, ViewEncapsulation } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { DragDropModule, CdkDragDrop } from '@angular/cdk/drag-drop';
import { Skill } from '../../profile.types';

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
  @Input({ required: true }) predefinedSkills: { category: string, items: string[] }[] = [];
  @Input({ required: true }) filteredSuggestions: { name: string, category: string }[] = [];

  @Output() addCustomSkill = new EventEmitter<{name: string, category: string, inputElement: HTMLInputElement}>();
  @Output() addSkill = new EventEmitter<{name: string, category: string}>();
  @Output() removeSkill = new EventEmitter<string>();
  @Output() onSkillInput = new EventEmitter<Event>();
  @Output() selectSuggestion = new EventEmitter<{suggestion: any, inputElement: HTMLInputElement}>();
  @Output() reorder = new EventEmitter<CdkDragDrop<any[]>>();

  isSkillSelected(skillName: string): boolean {
    return this.skills.some(s => s.name === skillName);
  }
}
