import { describe, it, expect, beforeEach, vi } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ToastService } from '../core/notifications/toast.service';

describe('ToastService', () => {
  let service: ToastService;
  let snackBar: MatSnackBar;

  beforeEach(() => {
    snackBar = {
      open: () => ({} as any),
      dismiss: () => {},
    } as any;

    TestBed.configureTestingModule({
      providers: [ToastService, { provide: MatSnackBar, useValue: snackBar }],
    });
    service = TestBed.inject(ToastService);
  });

  it('devrait être créé', () => {
    expect(service).toBeTruthy();
  });

  it('affiche un toast succès avec la classe toast-success', () => {
    const openSpy = vi.spyOn(snackBar, 'open');
    service.success('OK');
    expect(openSpy).toHaveBeenCalledWith('OK', 'Fermer', expect.objectContaining({ panelClass: ['toast-success'] }));
  });

  it('affiche un toast erreur avec la classe toast-error', () => {
    const openSpy = vi.spyOn(snackBar, 'open');
    service.error('Problème');
    expect(openSpy).toHaveBeenCalledWith('Problème', 'Fermer', expect.objectContaining({ panelClass: ['toast-error'] }));
  });

  it('apiError extrait le message du contrat d erreur normalisé', () => {
    const openSpy = vi.spyOn(snackBar, 'open');
    service.apiError({ message: 'Erreur serveur' });
    expect(openSpy).toHaveBeenCalledWith('Erreur serveur', 'Fermer', expect.objectContaining({ panelClass: ['toast-error'] }));
  });

  it('apiError utilise le message de repli si aucun message exploitable', () => {
    const openSpy = vi.spyOn(snackBar, 'open');
    service.apiError({ foo: 'bar' }, 'Repli');
    expect(openSpy).toHaveBeenCalledWith('Repli', 'Fermer', expect.anything());
  });

  it('dismiss appelle snackBar.dismiss', () => {
    const dismissSpy = vi.spyOn(snackBar, 'dismiss');
    service.dismiss();
    expect(dismissSpy).toHaveBeenCalledTimes(1);
  });
});