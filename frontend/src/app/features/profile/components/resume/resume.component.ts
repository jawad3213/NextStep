import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-resume',
  standalone: true,
  imports: [CommonModule, MatIconModule],
  templateUrl: './resume.component.html',
  styleUrl: './resume.component.scss'
})
export class ResumeComponent {
  @Input({ required: true }) resume: string = '';
  @Input({ required: true }) isGeneratingAI: boolean = false;

  @Output() updateResume = new EventEmitter<string>();
  @Output() generateAI = new EventEmitter<void>();

  onResumeChange(event: Event) {
    const value = (event.target as HTMLTextAreaElement).value;
    this.updateResume.emit(value);
  }
}
