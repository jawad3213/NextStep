import { Component, Input, Output, EventEmitter, ViewEncapsulation } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { DragDropModule, CdkDragDrop } from '@angular/cdk/drag-drop';
import { Certification } from '../../profile.types';

@Component({
  selector: 'app-certifications',
  standalone: true,
  imports: [CommonModule, FormsModule, MatIconModule, MatTooltipModule, DragDropModule],
  templateUrl: './certifications.component.html',
  styleUrl: './certifications.component.scss',
  encapsulation: ViewEncapsulation.None
})
export class CertificationsComponent {
  @Input({ required: true }) certifications: Certification[] = [];
  @Input({ required: true }) isAddingCertification: boolean = false;
  @Input({ required: true }) newCertification!: Certification;

  @Output() addToggle = new EventEmitter<void>();
  @Output() save = new EventEmitter<void>();
  @Output() updateNew = new EventEmitter<{field: string, value: any}>();
  @Output() edit = new EventEmitter<Certification>();
  @Output() duplicate = new EventEmitter<Certification>();
  @Output() delete = new EventEmitter<string>();
  @Output() reorder = new EventEmitter<CdkDragDrop<any[]>>();

  onUpdateNewItem(field: string, value: any) {
    this.updateNew.emit({ field, value });
  }
}
