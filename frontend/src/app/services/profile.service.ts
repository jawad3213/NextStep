import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface FullProfile {
  personalInfo: any;
  objectif: string;
  niveau: string;
  secteur: string;
  onboardingCompleted: boolean;
  experiences: any[];
  formations: any[];
  projets: any[];
  competences: any[];
}

@Injectable({
  providedIn: 'root',
})
export class ProfileService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/profile`;

  getFullProfile(): Observable<FullProfile> {
    return this.http.get<FullProfile>(this.baseUrl);
  }

  updatePersonalInfo(data: any): Observable<any> {
    return this.http.put(`${this.baseUrl}/personal-info`, data);
  }

  // Experiences
  addExperience(data: any): Observable<any> {
    return this.http.post(`${this.baseUrl}/experiences`, data);
  }
  updateExperience(data: any): Observable<any> {
    return this.http.put(`${this.baseUrl}/experiences`, data);
  }
  deleteExperience(id: string): Observable<any> {
    return this.http.delete(`${this.baseUrl}/experiences/${id}`);
  }

  // Projects
  addProject(data: any): Observable<any> {
    return this.http.post(`${this.baseUrl}/projets`, data);
  }
  updateProject(data: any): Observable<any> {
    return this.http.put(`${this.baseUrl}/projets`, data);
  }
  deleteProject(id: string): Observable<any> {
    return this.http.delete(`${this.baseUrl}/projets/${id}`);
  }

  // Skills
  addSkill(data: any): Observable<any> {
    return this.http.post(`${this.baseUrl}/competences`, data);
  }
  updateSkill(data: any): Observable<any> {
    return this.http.put(`${this.baseUrl}/competences`, data);
  }
  deleteSkill(id: string): Observable<any> {
    return this.http.delete(`${this.baseUrl}/competences/${id}`);
  }

  // Educations
  addEducation(data: any): Observable<any> {
    return this.http.post(`${this.baseUrl}/formations`, data);
  }
  updateEducation(data: any): Observable<any> {
    return this.http.put(`${this.baseUrl}/formations`, data);
  }
  deleteEducation(id: string): Observable<any> {
    return this.http.delete(`${this.baseUrl}/formations/${id}`);
  }
}
