import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';

@Component({
  selector: 'app-personal-info',
  standalone: true,
  imports: [CommonModule, MatIconModule, MatTooltipModule],
  templateUrl: './personal-info.component.html',
  styleUrl: './personal-info.component.scss'
})
export class PersonalInfoComponent {
  @Input({ required: true }) data!: any;
  @Output() dataChange = new EventEmitter<{field: string, value: any}>();
  @Output() photoUpload = new EventEmitter<Event>();

  updateField(field: string, value: any) {
    this.dataChange.emit({ field, value });
  }

  triggerPhotoUpload(input: HTMLInputElement) {
    input.click();
  }

  onPhotoSelected(event: Event) {
    this.photoUpload.emit(event);
  }
}
