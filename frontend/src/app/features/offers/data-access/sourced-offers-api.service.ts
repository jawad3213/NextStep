import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { apiUrl } from '@core/http/api-url';
import {
  PromoteSourcedOfferResponse,
  SourcedOfferDetailDto,
  SourcedOfferListItemDto,
  SourcedOfferSearchRequest,
  SourcedOfferSearchResponse,
  SourcedOfferUpdateRequest,
} from './sourced-offers.models';

/** Offers scraped from job boards: /api/sourced-offers. */
@Injectable({ providedIn: 'root' })
export class SourcedOffersApiService {
  private readonly http = inject(HttpClient);
  private readonly base = apiUrl('/sourced-offers');

  searchSourcedOffers(payload: SourcedOfferSearchRequest): Observable<SourcedOfferSearchResponse> {
    return this.http.post<SourcedOfferSearchResponse>(`${this.base}/search`, payload);
  }

  getSourcedOffers(params: SourcedOfferSearchRequest = {}): Observable<SourcedOfferListItemDto[]> {
    const query = new URLSearchParams();
    if (params.keywords) query.set('keywords', params.keywords);
    if (params.location) query.set('location', params.location);
    for (const provider of params.providers ?? []) query.append('providers', provider);
    if (params.limit) query.set('limit', String(params.limit));
    if (params.postedWindow) query.set('postedWindow', params.postedWindow);
    for (const contractType of params.contractTypes ?? []) query.append('contractTypes', contractType);
    if (params.indeedCountryCode) query.set('indeedCountryCode', params.indeedCountryCode);
    if (params.workflowState) query.set('workflowState', params.workflowState);
    const suffix = query.toString() ? `?${query.toString()}` : '';
    return this.http.get<SourcedOfferListItemDto[]>(`${this.base}${suffix}`);
  }

  getSourcedOffer(id: string): Observable<SourcedOfferDetailDto> {
    return this.http.get<SourcedOfferDetailDto>(`${this.base}/${id}`);
  }

  updateSourcedOffer(id: string, payload: SourcedOfferUpdateRequest): Observable<SourcedOfferDetailDto> {
    return this.http.patch<SourcedOfferDetailDto>(`${this.base}/${id}`, payload);
  }

  promoteSourcedOffer(id: string): Observable<PromoteSourcedOfferResponse> {
    return this.http.post<PromoteSourcedOfferResponse>(`${this.base}/${id}/promote`, {});
  }
}
