import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {UserProfileResponse } from '../core/auth/models/user-profile.model';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root',
})
export class IdentityService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/identity`;


  getProfile(): Observable<UserProfileResponse> {
    return this.http.get<UserProfileResponse>(`${this.baseUrl}/profile`);
  }
}
