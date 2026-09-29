/** A page of results, as returned by the backend's paged endpoints. */
export interface PagedResponse<T> {
  offset: number;
  limit: number;
  total: number;
  hasMore: boolean;
  items: T[];
}
