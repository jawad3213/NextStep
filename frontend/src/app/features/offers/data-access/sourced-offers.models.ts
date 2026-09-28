// ── API: /api/sourced-offers (offers found on job boards) ───────────────────

export type ScrapeProvider = 'linkedin' | 'indeed' | 'glassdoor';

export type PostedWindow = '24h' | '3d' | '7d' | '14d' | '30d' | 'any';

export type NormalizedContractType =
  | 'internship'
  | 'cdi'
  | 'cdd'
  | 'freelance'
  | 'alternance'
  | 'part_time'
  | 'full_time'
  | 'temporary'
  | 'other';

export interface SourcedOfferSearchRequest {
  keywords?: string | null;
  location?: string | null;
  providers?: ScrapeProvider[];
  limit?: number;
  postedWindow?: PostedWindow;
  contractTypes?: NormalizedContractType[];
  indeedCountryCode?: string | null;
  workflowState?: 'saved' | 'shortlisted' | 'archived' | null;
}

export interface ScrapeSessionDto {
  id: string;
  keywords?: string | null;
  location?: string | null;
  providers: string[];
  countryCode?: string | null;
  postedWindow: PostedWindow;
  contractTypes: NormalizedContractType[];
  limit: number;
  resultCount: number;
  warnings: string[];
  errors: string[];
  createdAtUtc: string;
}

export interface SourcedOfferListItemDto {
  id: string;
  provider: ScrapeProvider;
  providerJobId?: string | null;
  externalUrl?: string | null;
  title: string;
  company?: string | null;
  location?: string | null;
  description?: string | null;
  postedAtText?: string | null;
  postedWindow?: PostedWindow | null;
  rawContractType?: string | null;
  normalizedContractType?: NormalizedContractType | null;
  employmentType?: string | null;
  seniorityLevel?: string | null;
  matchedItTerms: string[];
  isSaved: boolean;
  isShortlisted: boolean;
  isArchived: boolean;
  promotedOfferId?: string | null;
  firstSeenAtUtc: string;
  lastSeenAtUtc: string;
  scrapedAtUtc: string;
}

export interface SourcedOfferDetailDto extends SourcedOfferListItemDto {
  sourceQuery: Record<string, unknown>;
  similarOffers: SourcedOfferListItemDto[];
}

export interface SourcedOfferSearchResponse {
  session?: ScrapeSessionDto | null;
  offers: SourcedOfferListItemDto[];
  warnings: string[];
}

export interface SourcedOfferUpdateRequest {
  isSaved?: boolean;
  isShortlisted?: boolean;
  isArchived?: boolean;
}

export interface PromoteSourcedOfferResponse {
  offerId: string;
  alreadyPromoted: boolean;
}
