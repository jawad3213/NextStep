import { Component, Input, Output, EventEmitter, ViewEncapsulation } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { DragDropModule, CdkDragDrop } from '@angular/cdk/drag-drop';
import { Project, Experience } from '../../profile.types';

@Component({
  selector: 'app-projects',
  standalone: true,
  imports: [CommonModule, FormsModule, MatIconModule, MatTooltipModule, DragDropModule],
  templateUrl: './projects.component.html',
  styleUrl: './projects.component.scss',
  encapsulation: ViewEncapsulation.None
})
export class ProjectsComponent {
  @Input({ required: true }) projects: Project[] = [];
  @Input({ required: true }) extracurriculars: Experience[] = [];
  @Input({ required: true }) isAddingProject: boolean = false;
  @Input({ required: true }) isAddingExtracurricular: boolean = false;
  
  @Input({ required: true }) newProject!: Project;
  @Input({ required: true }) newExtracurricular!: Experience;
  
  @Input({ required: true }) sectionTitle: string = 'ACADEMIC & PERSONAL PROJECTS';
  @Input({ required: true }) isEditingTitle: boolean = false;
  
  @Input() extraTitle: string = 'EXTRACURRICULAR ACTIVITIES & VOLUNTEERING';
  @Input() isEditingExtraTitle: boolean = false;
  
  @Input() activeTab: 'projects' | 'extracurriculars' = 'projects';

  @Output() tabChange = new EventEmitter<'projects' | 'extracurriculars'>();
  
  @Output() addToggle = new EventEmitter<void>();
  @Output() save = new EventEmitter<void>();
  @Output() updateNew = new EventEmitter<{field: string, value: any}>();
  @Output() edit = new EventEmitter<Project>();
  @Output() duplicate = new EventEmitter<Project>();
  @Output() delete = new EventEmitter<string>();
  @Output() reorder = new EventEmitter<CdkDragDrop<any[]>>();

  @Output() addToggleExtra = new EventEmitter<void>();
  @Output() saveExtra = new EventEmitter<void>();
  @Output() updateNewExtra = new EventEmitter<{field: string, value: any}>();
  @Output() editExtra = new EventEmitter<Experience>();
  @Output() deleteExtra = new EventEmitter<string>();
  
  @Output() titleEditStart = new EventEmitter<void>();
  @Output() titleEditSave = new EventEmitter<string>();

  @Output() extraTitleEditStart = new EventEmitter<void>();
  @Output() extraTitleEditSave = new EventEmitter<string>();

  onUpdateNewItem(field: string, value: any) {
    this.updateNew.emit({ field, value });
  }

  onUpdateNewExtra(field: string, value: any) {
    this.updateNewExtra.emit({ field, value });
  }
}
