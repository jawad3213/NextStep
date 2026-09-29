import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { apiUrl } from '@core/http/api-url';
import {
  CertificationDto,
  CompetenceDto,
  ExperienceDto,
  FormationDto,
  FullProfileDto,
  GenerateResumeResponse,
  KeywordDto,
  MessageResponse,
  PersonalInfoDto,
  PhotoUploadResponse,
  ProjetDto,
  SignedPhotoUrlResponse,
} from './profile-api.models';

/** Profile endpoints (/api/profile). State and UI mapping live in ProfileService. */
@Injectable({ providedIn: 'root' })
export class ProfileApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = apiUrl('/profile');

  getFullProfile(): Observable<FullProfileDto> {
    return this.http.get<FullProfileDto>(this.baseUrl);
  }

  updatePersonalInfo(data: PersonalInfoDto): Observable<MessageResponse> {
    return this.http.put<MessageResponse>(`${this.baseUrl}/personal-info`, data);
  }

  updateLanguagePreference(language: 'en' | 'fr'): Observable<MessageResponse> {
    return this.http.put<MessageResponse>(`${this.baseUrl}/language`, { language });
  }

  // ── Experiences ────────────────────────────────────────────────────────────
  addExperience(data: ExperienceDto): Observable<MessageResponse> {
    return this.http.post<MessageResponse>(`${this.baseUrl}/experiences`, data);
  }
  updateExperience(data: ExperienceDto): Observable<MessageResponse> {
    return this.http.put<MessageResponse>(`${this.baseUrl}/experiences`, data);
  }
  deleteExperience(id: string): Observable<MessageResponse> {
    return this.http.delete<MessageResponse>(`${this.baseUrl}/experiences/${id}`);
  }

  // ── Education ──────────────────────────────────────────────────────────────
  addEducation(data: FormationDto): Observable<MessageResponse> {
    return this.http.post<MessageResponse>(`${this.baseUrl}/formations`, data);
  }
  updateEducation(data: FormationDto): Observable<MessageResponse> {
    return this.http.put<MessageResponse>(`${this.baseUrl}/formations`, data);
  }
  deleteEducation(id: string): Observable<MessageResponse> {
    return this.http.delete<MessageResponse>(`${this.baseUrl}/formations/${id}`);
  }

  // ── Skills and languages (both are "competences") ──────────────────────────
  addSkill(data: CompetenceDto): Observable<MessageResponse> {
    return this.http.post<MessageResponse>(`${this.baseUrl}/competences`, data);
  }
  updateSkill(data: CompetenceDto): Observable<MessageResponse> {
    return this.http.put<MessageResponse>(`${this.baseUrl}/competences`, data);
  }
  deleteSkill(id: string): Observable<MessageResponse> {
    return this.http.delete<MessageResponse>(`${this.baseUrl}/competences/${id}`);
  }

  // ── Projects ───────────────────────────────────────────────────────────────
  addProject(data: ProjetDto): Observable<MessageResponse> {
    return this.http.post<MessageResponse>(`${this.baseUrl}/projets`, data);
  }
  updateProject(data: ProjetDto): Observable<MessageResponse> {
    return this.http.put<MessageResponse>(`${this.baseUrl}/projets`, data);
  }
  deleteProject(id: string): Observable<MessageResponse> {
    return this.http.delete<MessageResponse>(`${this.baseUrl}/projets/${id}`);
  }

  // ── Certifications ─────────────────────────────────────────────────────────
  addCertification(data: CertificationDto): Observable<MessageResponse> {
    return this.http.post<MessageResponse>(`${this.baseUrl}/certifications`, data);
  }
  updateCertification(data: CertificationDto): Observable<MessageResponse> {
    return this.http.put<MessageResponse>(`${this.baseUrl}/certifications`, data);
  }
  deleteCertification(id: string): Observable<MessageResponse> {
    return this.http.delete<MessageResponse>(`${this.baseUrl}/certifications/${id}`);
  }

  // ── Photo ──────────────────────────────────────────────────────────────────
  uploadPhoto(file: File): Observable<PhotoUploadResponse> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<PhotoUploadResponse>(`${this.baseUrl}/photo`, formData);
  }
  getPhoto(): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/photo`, { responseType: 'blob' });
  }
  getSignedPhotoUrl(): Observable<SignedPhotoUrlResponse> {
    return this.http.get<SignedPhotoUrlResponse>(`${this.baseUrl}/photo/signed`);
  }

  // ── Reference data, AI and import ──────────────────────────────────────────
  getKeywords(): Observable<KeywordDto[]> {
    return this.http.get<KeywordDto[]>(`${this.baseUrl}/keywords`);
  }
  generateResume(profileData: unknown): Observable<GenerateResumeResponse> {
    return this.http.post<GenerateResumeResponse>(`${this.baseUrl}/generate-resume`, profileData);
  }
  /** Returns the agents' raw extraction (shape normalised by profile-import.normalizer). */
  parseResume(file: File): Observable<unknown> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<unknown>(`${this.baseUrl}/parse-resume`, formData);
  }
  importLinkedIn(url: string, rawText?: string): Observable<unknown> {
    return this.http.post<unknown>(`${this.baseUrl}/import-linkedin`, { url, rawText });
  }

  exportProfileJson(): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/export`, { responseType: 'blob' });
  }

  /** Deletes every profile section (keeps the account). */
  clearProfile(): Observable<MessageResponse> {
    return this.http.delete<MessageResponse>(`${this.baseUrl}/clear`);
  }
}
