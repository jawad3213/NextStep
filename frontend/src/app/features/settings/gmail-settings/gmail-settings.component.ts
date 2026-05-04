import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { EmailService, EmailConnectionStatusDto } from '../../../services/email.service';

@Component({
  selector: 'app-gmail-settings',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './gmail-settings.component.html',
  styleUrl: './gmail-settings.component.scss'
})
export class GmailSettingsComponent implements OnInit {
  private readonly emailService = inject(EmailService);

  loading = signal(true);
  connecting = signal(false);
  error = signal<string | null>(null);
  status = signal<EmailConnectionStatusDto | null>(null);

  ngOnInit() {
    this.checkStatus();
  }

  checkStatus() {
    this.loading.set(true);
    this.error.set(null);
    this.emailService.getGmailStatus().subscribe({
      next: (s) => { this.status.set(s); this.loading.set(false); },
      error: () => { this.error.set('Impossible de récupérer le statut Gmail.'); this.loading.set(false); }
    });
  }

  connectGmail() {
    if (this.connecting()) return;
    this.connecting.set(true);
    this.error.set(null);
    this.emailService.getGmailLoginUrl().subscribe({
      next: (res) => {
        if (res?.url) window.location.href = res.url;
        else { this.error.set('URL de connexion invalide.'); this.connecting.set(false); }
      },
      error: () => { this.error.set('Impossible d\'obtenir le lien de connexion Gmail.'); this.connecting.set(false); }
    });
  }
}
