import { Component, Input, Output, EventEmitter, ViewEncapsulation } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { DragDropModule, CdkDragDrop } from '@angular/cdk/drag-drop';
import { Experience, EXPERIENCE_TYPE_LABELS } from '../../profile.types';

@Component({
  selector: 'app-experience',
  standalone: true,
  imports: [CommonModule, FormsModule, MatIconModule, MatTooltipModule, DragDropModule],
  templateUrl: './experience.component.html',
  styleUrl: './experience.component.scss',
  encapsulation: ViewEncapsulation.None
})
export class ExperienceComponent {
  @Input({ required: true }) experiences: Experience[] = [];
  @Input({ required: true }) isAddingExperience: boolean = false;
  @Input({ required: true }) newExperience!: Experience;
  @Input({ required: true }) sectionTitle: string = 'EXPÉRIENCE PROFESSIONNELLE';
  @Input({ required: true }) isEditingTitle: boolean = false;

  @Output() addToggle = new EventEmitter<void>();
  @Output() save = new EventEmitter<void>();
  @Output() updateNew = new EventEmitter<{field: string, value: any}>();
  @Output() edit = new EventEmitter<Experience>();
  @Output() duplicate = new EventEmitter<Experience>();
  @Output() delete = new EventEmitter<string>();
  @Output() reorder = new EventEmitter<CdkDragDrop<any[]>>();
  
  @Output() titleEditStart = new EventEmitter<void>();
  @Output() titleEditSave = new EventEmitter<string>();

  onUpdateNewItem(field: string, value: any) {
    this.updateNew.emit({ field, value });
  }

  getExperienceTypeLabel(t: string): string {
    return EXPERIENCE_TYPE_LABELS[t] || t;
  }

  isEditingExperience(exp: Experience): boolean {
    return !!this.newExperience?.id && this.isAddingExperience && this.newExperience.id === exp.id;
  }

  isCreatingExperience(): boolean {
    return this.isAddingExperience && !this.newExperience?.id;
  }

  addTask() {
    this.updateNew.emit({
      field: 'taches',
      value: [...(this.newExperience.taches || []), '']
    });
  }

  updateTask(index: number, value: string) {
    const nextTasks = [...(this.newExperience.taches || [])];
    nextTasks[index] = value;
    this.updateNew.emit({ field: 'taches', value: nextTasks });
  }

  removeTask(index: number) {
    const nextTasks = [...(this.newExperience.taches || [])];
    nextTasks.splice(index, 1);
    this.updateNew.emit({ field: 'taches', value: nextTasks });
  }
}
