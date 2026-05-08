import { Component, Input, Output, EventEmitter, ViewEncapsulation } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { DragDropModule, CdkDragDrop } from '@angular/cdk/drag-drop';
import { Education, MENTION_LABELS, DEGREE_LABELS } from '../../profile.types';

@Component({
  selector: 'app-formation',
  standalone: true,
  imports: [CommonModule, FormsModule, MatIconModule, MatTooltipModule, DragDropModule],
  templateUrl: './formation.component.html',
  styleUrl: './formation.component.scss',
  encapsulation: ViewEncapsulation.None
})
export class FormationComponent {
  @Input({ required: true }) formations: Education[] = [];
  @Input({ required: true }) isAddingFormation: boolean = false;
  @Input({ required: true }) newFormation!: Education;
  @Input({ required: true }) sectionTitle: string = 'FORMATION';
  @Input({ required: true }) isEditingTitle: boolean = false;

  @Output() addToggle = new EventEmitter<void>();
  @Output() save = new EventEmitter<void>();
  @Output() updateNew = new EventEmitter<{field: string, value: any}>();
  @Output() edit = new EventEmitter<Education>();
  @Output() duplicate = new EventEmitter<Education>();
  @Output() delete = new EventEmitter<string>();
  @Output() reorder = new EventEmitter<CdkDragDrop<any[]>>();
  
  @Output() titleEditStart = new EventEmitter<void>();
  @Output() titleEditSave = new EventEmitter<string>();

  onUpdateNewItem(field: string, value: any) {
    this.updateNew.emit({ field, value });
  }

  getMentionLabel(m: string): string {
    return MENTION_LABELS[m] || m;
  }

  getDegreeLabel(d: string): string {
    return DEGREE_LABELS[d] || d;
  }
}
