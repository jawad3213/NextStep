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
          description: 'View your last 10 applications',
          prompt: 'Show my last 10 applications',
          category: 'Portfolio',
          icon: 'layers'
        }
      ]);
    }
  });

  it('should initialize correctly', () => {
    expect(service).toBeTruthy();
    expect(service.isOpen()).toBe(false);
    expect(service.isExpanded()).toBe(false);
    expect(service.loading()).toBe(false);
    expect(service.messages().length).toBe(0);
  });

  it('should toggle drawer state (open/close)', () => {
    service.toggleDrawer();
    expect(service.isOpen()).toBe(true);

    service.closeDrawer();
    expect(service.isOpen()).toBe(false);

    service.openDrawer('Hello SN');
    expect(service.isOpen()).toBe(true);
  });

  it('should toggle expanded mode (440px / 740px)', () => {
    expect(service.isExpanded()).toBe(false);
    service.toggleExpand();
    expect(service.isExpanded()).toBe(true);
    service.toggleExpand();
    expect(service.isExpanded()).toBe(false);
  });

  it('should add user message and send request', () => {
    service.sendMessage('Show my last 10 applications');

    expect(service.messages().length).toBe(1);
    expect(service.messages()[0].text).toBe('Show my last 10 applications');
    expect(service.messages()[0].sender).toBe('user');
    expect(service.loading()).toBe(true);

    const chatReq = httpMock.expectOne('http://localhost:5000/api/sn/chat');
    expect(chatReq.request.method).toBe('POST');
    expect(chatReq.request.body.message).toBe('Show my last 10 applications');

    chatReq.flush({
      markdownText: 'Here are your last 10 recorded applications.',
      actionsPerformed: ['Fetched 10 applications'],
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
      followUpSuggestions: ['Set Doctolib to accepted', 'View statistics']
    });

    expect(service.loading()).toBe(false);
    expect(service.messages().length).toBe(2);
    expect(service.messages()[1].sender).toBe('sn');
    expect(service.messages()[1].cards?.length).toBe(1);
    expect(service.activeFollowUps().length).toBe(2);
  });

  it('should clear conversation history', () => {
    service.sendMessage('Test');
    expect(service.messages().length).toBe(1);

    service.clearHistory();
    expect(service.messages().length).toBe(0);
  });
});
