import { describe, it, expect, beforeEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { SnCopilotService } from './sn-copilot.service';

describe('SnCopilotService', () => {
  let service: SnCopilotService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        SnCopilotService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });
    service = TestBed.inject(SnCopilotService);
    httpMock = TestBed.inject(HttpTestingController);

    // Handle initial loadStarterSuggestions request if any
    const req = httpMock.match('http://localhost:5000/api/sn/starter-suggestions');
    if (req.length > 0) {
      req[0].flush([
        {
          title: 'Portfolio Review',
          description: 'Visualiser vos 10 dernières candidatures',
          prompt: 'Affiche mes 10 dernières candidatures',
          category: 'Portfolio',
          icon: 'layers'
        }
      ]);
    }
  });

  it('devrait être initialisé correctement', () => {
    expect(service).toBeTruthy();
    expect(service.isOpen()).toBe(false);
    expect(service.isExpanded()).toBe(false);
    expect(service.loading()).toBe(false);
    expect(service.messages().length).toBe(0);
  });

  it('devrait basculer l état du drawer (open/close)', () => {
    service.toggleDrawer();
    expect(service.isOpen()).toBe(true);

    service.closeDrawer();
    expect(service.isOpen()).toBe(false);

    service.openDrawer('Hello SN');
    expect(service.isOpen()).toBe(true);
  });

  it('devrait basculer le mode étendu (440px / 740px)', () => {
    expect(service.isExpanded()).toBe(false);
    service.toggleExpand();
    expect(service.isExpanded()).toBe(true);
    service.toggleExpand();
    expect(service.isExpanded()).toBe(false);
  });

  it('devrait ajouter un message utilisateur et envoyer la requête', () => {
    service.sendMessage('Affiche mes 10 dernières candidatures');

    expect(service.messages().length).toBe(1);
    expect(service.messages()[0].text).toBe('Affiche mes 10 dernières candidatures');
    expect(service.messages()[0].sender).toBe('user');
    expect(service.loading()).toBe(true);

    const chatReq = httpMock.expectOne('http://localhost:5000/api/sn/chat');
    expect(chatReq.request.method).toBe('POST');
    expect(chatReq.request.body.message).toBe('Affiche mes 10 dernières candidatures');

    chatReq.flush({
      markdownText: 'Voici vos 10 dernières candidatures enregistrées.',
      actionsPerformed: ['Recupération de 10 candidatures'],
      cards: [
        {
          id: 'c1',
          entreprise: 'Doctolib',
          role: 'Lead Dev',
          statut: 'ENTRETIEN',
          channel: 'EMAIL',
          has_response: true
        }
      ],
      followUpSuggestions: ['Passer Doctolib en accepté', 'Voir les statistiques']
    });

    expect(service.loading()).toBe(false);
    expect(service.messages().length).toBe(2);
    expect(service.messages()[1].sender).toBe('sn');
    expect(service.messages()[1].cards?.length).toBe(1);
    expect(service.activeFollowUps().length).toBe(2);
  });

  it('devrait vider la conversation', () => {
    service.sendMessage('Test');
    expect(service.messages().length).toBe(1);

    service.clearHistory();
    expect(service.messages().length).toBe(0);
  });
});
