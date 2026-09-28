// ── API: /api/cv and /api/offers/{id}/cv-draft ─────────────────────────────

export interface CvDesignConfig {
  themeColor: string;
  fontFamily: string;
  fontSize: string;
  lineSpacing: string;
  sectionSpacing: string;
  sidebarWidth: string;
}

export interface CvTemplateDto {
  id: string;
  slug: string;
  name: string;
  description?: string | null;
  thumbnailUrl?: string | null;
  industries: string[];
  experienceLevels: string[];
  style: string;
  layoutFlags: string[];
  backgroundColor: string;
  tags: string[];
}

/** A saved (final) CV. */
export interface CvHistoryItem {
  id: string;
  title?: string | null;
  templateSlug: string;
  templateName?: string | null;
  fileUrl: string;
  fileSizeBytes: number;
  createdAt: string;
  updatedAt?: string | null;
}

export interface CvSaveResponse {
  historyId: string;
  fileUrl: string;
  fileSizeBytes: number;
}

export interface SaveFinalCvRequest {
  templateSlug: string;
  title: string;
  offerId?: string | null;
  data: any;
  designConfig: CvDesignConfig;
  htmlSnapshot?: string | null;
}

export interface CvRenderRequest {
  templateSlug: string;
  data: any;
  designConfig: CvDesignConfig;
}

export interface CvRenderResponse {
  templateSlug: string;
  designConfig: CvDesignConfig;
  html: string;
}

export interface CvExportPdfRequest {
  templateSlug: string;
  data: any;
  designConfig: CvDesignConfig;
  htmlSnapshot?: string | null;
}

/** The editable CV draft of an offer. */
export interface CvDraftResponse {
  offerId: string;
  data: any;
  version: number;
  updatedAtUtc: string;
}
