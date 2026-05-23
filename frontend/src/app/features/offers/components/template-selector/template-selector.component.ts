import { Component, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-template-selector',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './template-selector.component.html',
  styleUrls: ['./template-selector.component.scss']
})
export class TemplateSelectorComponent {
  @Output() back = new EventEmitter<void>();
  @Output() selectTemplate = new EventEmitter<string>();

  templates = [
    {
      id: 'modern',
      name: 'Modern Minimalist',
      desc: 'Sleek, side-by-side split layout ideal for technical and start-up applications.',
      tags: ['Sleek', 'Two-column', 'Technical']
    },
    {
      id: 'professional',
      name: 'Executive Classic',
      desc: 'Elegant, top-centered serif-balanced structure optimal for corporate fields.',
      tags: ['Traditional', 'Clean', 'Corporate']
    },
    {
      id: 'elegant',
      name: 'Sophisticated Creative',
      desc: 'Asymmetric column structures with soft color accents to stand out stylishly.',
      tags: ['Stylish', 'Accent Header', 'Modern']
    }
  ];

  selectedId = 'modern';

  onSelect(id: string) {
    this.selectedId = id;
  }

  onConfirm() {
    this.selectTemplate.emit(this.selectedId);
  }

  onBack() {
    this.back.emit();
  }
}
