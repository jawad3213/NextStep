import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class SidebarService {
  private readonly isExpandedSubject = new BehaviorSubject<boolean>(false);
  private readonly isMobileOpenSubject = new BehaviorSubject<boolean>(false);
  private readonly isHoveredSubject = new BehaviorSubject<boolean>(false);
  private readonly isEditorFocusModeSubject = new BehaviorSubject<boolean>(false);

  readonly isExpanded$ = this.isExpandedSubject.asObservable();
  readonly isMobileOpen$ = this.isMobileOpenSubject.asObservable();
  readonly isHovered$ = this.isHoveredSubject.asObservable();
  readonly isEditorFocusMode$ = this.isEditorFocusModeSubject.asObservable();

  get expandedValue(): boolean {
    return this.isExpandedSubject.value;
  }

  get editorFocusModeValue(): boolean {
    return this.isEditorFocusModeSubject.value;
  }

  setExpanded(value: boolean): void {
    this.isExpandedSubject.next(value);
  }

  toggleExpanded(): void {
    this.isExpandedSubject.next(!this.isExpandedSubject.value);
  }

  setMobileOpen(value: boolean): void {
    this.isMobileOpenSubject.next(value);
  }

  toggleMobileOpen(): void {
    this.isMobileOpenSubject.next(!this.isMobileOpenSubject.value);
  }

  setHovered(value: boolean): void {
    this.isHoveredSubject.next(value);
  }

  setEditorFocusMode(value: boolean): void {
    this.isEditorFocusModeSubject.next(value);
  }

  toggleEditorFocusMode(): void {
    this.isEditorFocusModeSubject.next(!this.isEditorFocusModeSubject.value);
  }
}
