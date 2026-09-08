import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { MatSnackBar } from '@angular/material/snack-bar';
import { apiErrorInterceptor } from '../core/http/api-error.interceptor';
import { ToastService } from '../core/notifications/toast.service';

describe('apiErrorInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let toast: ToastService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([apiErrorInterceptor])),
        provideHttpClientTesting(),
        ToastService,
        { provide: MatSnackBar, useValue: { open: vi.fn(), dismiss: vi.fn() } },
      ],
    });
    http = TestBed.inject(HttpClient);
    toast = TestBed.inject(ToastService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('affiche un toast global pour les erreurs 5xx', () => {
    const errorSpy = vi.spyOn(toast, 'error');
    http.get('/api/test').subscribe({ error: () => {} });
    httpMock.expectOne('/api/test').flush({ error: 'Backend down' }, { status: 502, statusText: 'Bad Gateway' });
    expect(errorSpy).toHaveBeenCalledWith('Backend down');
  });

  it('ré-émet l erreur HTTP telle quelle (pas de transformation)', () => {
    http.get('/api/test').subscribe({
      error: (err: HttpErrorResponse) => {
        expect(err.status).toBe(404);
      },
    });
    httpMock.expectOne('/api/test').flush('Not Found', { status: 404, statusText: 'Not Found' });
  });

  it('ne déclenche pas de toast global pour les 4xx', () => {
    const errorSpy = vi.spyOn(toast, 'error');
    http.get('/api/test').subscribe({ error: () => {} });
    httpMock.expectOne('/api/test').flush({ error: 'Bad request' }, { status: 400, statusText: 'Bad Request' });
    expect(errorSpy).not.toHaveBeenCalled();
  });
});