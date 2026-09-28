import { Component, inject, signal, ViewChild, ElementRef, AfterViewChecked, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { SnCopilotService, SnCardDto, SnStarterSuggestionItem, SnUiMessage } from '../../data-access/sn-copilot.service';

@Component({
  selector: 'app-sn-drawer',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './sn-drawer.component.html',
  styleUrl: './sn-drawer.component.scss'
})
export class SnDrawerComponent implements AfterViewChecked {
  readonly snService = inject(SnCopilotService);
  private readonly router = inject(Router);

  @ViewChild('chatScrollContainer') private scrollContainer?: ElementRef<HTMLDivElement>;

  userInput = signal('');
  isExpandedCards = signal(false);
  isMenuOpen = signal(false);
  showUploadCard = signal(false);
  isDraggingFile = signal(false);
  inputPlaceholder = signal('Ask anything');
  currentTime = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  private shouldScrollToBottom = false;

  toggleExpandCards(): void {
    this.isExpandedCards.update((v) => !v);
  }

  toggleMenu(event?: Event): void {
    if (event) {
      event.stopPropagation();
    }
    this.isMenuOpen.update((v) => !v);
  }

  closeMenu(): void {
    this.isMenuOpen.set(false);
  }

  onMenuAskQuestion(): void {
    this.closeMenu();
    this.inputPlaceholder.set('Ask anything');
    const userMsg: SnUiMessage = {
      id: `user-${Date.now()}`,
      sender: 'user',
      text: 'Ask a question',
      timestamp: new Date(),
      status: 'delivered'
    };
    const botMsg: SnUiMessage = {
      id: `sn-${Date.now() + 1}`,
      sender: 'sn',
      text: 'Voici les questions fréquentes pour piloter vos candidatures avec SN :',
      timestamp: new Date(),
      suggestions: [
        'Quelles sont mes candidatures prioritaires ?',
        'Résume ce que tu connais de mon profil',
        'Comment préparer mes prochains entretiens ?',
        'Quelles relances urgentes dois-je faire ?',
        'Quel est le diagnostic de mon pipeline ?'
      ]
    };
    this.snService.messages.update((list) => [...list, userMsg, botMsg]);
    this.shouldScrollToBottom = true;
  }

  onMenuGuidedSearch(): void {
    this.closeMenu();
    this.inputPlaceholder.set('Ask anything');
    this.snService.sendMessage('Guided Job Search : analyse mes opportunités et guide-moi selon mon profil.');
    this.shouldScrollToBottom = true;
  }

  onMenuUploadResume(): void {
    this.closeMenu();
    this.showUploadCard.set(true);
    this.inputPlaceholder.set('Upload your resume');
    const userMsg: SnUiMessage = {
      id: `user-${Date.now()}`,
      sender: 'user',
      text: 'Upload Resume',
      timestamp: new Date(),
      status: 'delivered'
    };
    const botMsg: SnUiMessage = {
      id: `sn-${Date.now() + 1}`,
      sender: 'sn',
      text: 'Please upload your resume as a Word, PDF or text file, up to 1 MB.',
      timestamp: new Date()
    };
    this.snService.messages.update((list) => [...list, userMsg, botMsg]);
    this.shouldScrollToBottom = true;
  }

  onMenuJobAlerts(): void {
    this.closeMenu();
    this.inputPlaceholder.set('Ask anything');
    this.snService.sendMessage('Quelles sont les alertes, dates limites et relances urgentes pour mes candidatures ?');
    this.shouldScrollToBottom = true;
  }

  cancelUploadCard(): void {
    this.showUploadCard.set(false);
    this.inputPlaceholder.set('Ask anything');
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    const target = event.target as HTMLElement;
    if (this.isMenuOpen() && !target.closest('.sn-menu-btn') && !target.closest('.sn-context-menu')) {
      this.isMenuOpen.set(false);
    }
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.isMenuOpen()) {
      this.isMenuOpen.set(false);
    }
  }

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDraggingFile.set(true);
  }

  onDragLeave(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDraggingFile.set(false);
  }

  onFileDrop(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDraggingFile.set(false);
    if (event.dataTransfer && event.dataTransfer.files && event.dataTransfer.files.length > 0) {
      this.processFile(event.dataTransfer.files[0]);
    }
  }

  ngAfterViewChecked(): void {
    if (this.shouldScrollToBottom) {
      this.scrollToBottom();
      this.shouldScrollToBottom = false;
    }
  }

  sendMessage(): void {
    const text = this.userInput().trim();
    if (!text || this.snService.loading()) return;
    this.snService.sendMessage(text);
    this.userInput.set('');
    this.shouldScrollToBottom = true;
  }

  onKeyDown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.sendMessage();
    }
  }

  selectSuggestion(prompt: string): void {
    this.snService.sendMessage(prompt);
    this.shouldScrollToBottom = true;
  }

  close(): void {
    this.snService.closeDrawer();
  }

  toggleExpand(): void {
    this.snService.toggleExpand();
  }

  clearConversation(): void {
    this.snService.clearHistory();
  }

  navigateToCard(card: SnCardDto): void {
    this.snService.closeDrawer();
    if (card.id) {
      this.router.navigate(['/applications', card.id]);
    } else {
      this.router.navigate(['/applications']);
    }
  }

  navigateToApplications(): void {
    this.snService.closeDrawer();
    this.router.navigate(['/applications']);
  }

  formatStatut(statut?: string): string {
    if (!statut) return 'En cours';
    const s = statut.toUpperCase();
    if (s.includes('ENTRETIEN')) return 'Entretien';
    if (s.includes('ACCEPTE')) return 'Accepté';
    if (s.includes('REFUSE')) return 'Refusé';
    if (s.includes('RELANCE')) return 'Relance requise';
    if (s.includes('EXAMEN') || s.includes('ATTENTE') || s.includes('ENVOYE')) return 'En cours d’examen';
    return statut.replace(/_/g, ' ');
  }

  getInitials(name: string): string {
    if (!name) return 'CO';
    const parts = name.trim().split(/\s+/);
    if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
    return (parts[0][0] + parts[1][0]).toUpperCase();
  }

  onFileUpload(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (!input.files || input.files.length === 0) return;
    this.processFile(input.files[0]);
    input.value = '';
  }

  processFile(file: File): void {
    const maxSize = 5 * 1024 * 1024; // 5MB

    if (file.size > maxSize) {
      this.snService.sendMessage('[CV_UPLOAD] Erreur : Le fichier est trop volumineux (max 5 Mo).');
      return;
    }

    this.showUploadCard.set(false);
    this.inputPlaceholder.set('Ask anything');

    // For text files, read directly
    if (file.type === 'text/plain' || file.name.endsWith('.txt')) {
      const reader = new FileReader();
      reader.onload = () => {
        const text = reader.result as string;
        if (text.trim().length < 50) {
          this.snService.sendMessage('[CV_UPLOAD] Le fichier semble vide ou trop court pour être un CV.');
        } else {
          this.snService.sendMessage(`[CV_UPLOAD] ${text}`);
        }
        this.shouldScrollToBottom = true;
      };
      reader.readAsText(file);
    } else {
      // For PDF / Word files, read as Base64 DataURL so backend PyMuPDF extracts the full text
      const reader = new FileReader();
      reader.onload = () => {
        const dataUrl = reader.result as string;
        this.snService.sendMessage(`[CV_UPLOAD_BASE64:${file.name}] ${dataUrl}`);
        this.shouldScrollToBottom = true;
      };
      reader.readAsDataURL(file);
    }
  }

  formatMarkdown(text: string): string {
    if (!text) return '';

    // Normalize newlines
    let src = text.replace(/\r\n/g, '\n').trim();

    // Escape basic HTML
    src = src
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;');

    // Inline formatting: Bold, Italic, Code
    src = src
      .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
      .replace(/\*(.*?)\*/g, '<em>$1</em>')
      .replace(/`([^`]+)`/g, '<code class="sn-code">$1</code>');

    // Headers
    src = src
      .replace(/^### (.*$)/gim, '<h4 class="sn-h4">$1</h4>')
      .replace(/^## (.*$)/gim, '<h3 class="sn-h3">$1</h3>')
      .replace(/^# (.*$)/gim, '<h2 class="sn-h2">$1</h2>');

    // Blockquotes
    src = src.replace(/^(&gt;|>)\s*(.*$)/gim, '<blockquote class="sn-quote">$2</blockquote>');

    // Line-by-line structure processing for pristine typography
    const lines = src.split('\n');
    const result: string[] = [];
    let inUl = false;
    let inOl = false;

    for (let i = 0; i < lines.length; i++) {
      const line = lines[i].trim();

      // Bullet list item (- item or * item)
      const bulletMatch = line.match(/^[-•*]\s+(.*)$/);
      if (bulletMatch) {
        if (inOl) {
          result.push('</ol>');
          inOl = false;
        }
        if (!inUl) {
          result.push('<ul class="sn-ul">');
          inUl = true;
        }
        result.push(`<li class="sn-li">${bulletMatch[1]}</li>`);
        continue;
      }

      // Ordered list item (1. item)
      const numMatch = line.match(/^(\d+)\.\s+(.*)$/);
      if (numMatch) {
        if (inUl) {
          result.push('</ul>');
          inUl = false;
        }
        if (!inOl) {
          result.push('<ol class="sn-ol">');
          inOl = true;
        }
        result.push(`<li class="sn-li">${numMatch[2]}</li>`);
        continue;
      }

      // Close open list if line is not a list item
      if (inUl) {
        result.push('</ul>');
        inUl = false;
      }
      if (inOl) {
        result.push('</ol>');
        inOl = false;
      }

      if (!line) {
        result.push('<div class="sn-spacing"></div>');
      } else if (line.startsWith('<h2') || line.startsWith('<h3') || line.startsWith('<h4') || line.startsWith('<blockquote')) {
        result.push(line);
      } else {
        result.push(`<p class="sn-p">${line}</p>`);
      }
    }

    if (inUl) result.push('</ul>');
    if (inOl) result.push('</ol>');

    return result.join('');
  }

  private scrollToBottom(): void {
    if (this.scrollContainer) {
      const el = this.scrollContainer.nativeElement;
      el.scrollTop = el.scrollHeight;
    }
  }
}
