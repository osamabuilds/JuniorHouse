/** One page of a server-side list (the API's PagedResult). */
export interface Page<T> {
  readonly items: readonly T[];
  readonly page: number;
  readonly pageSize: number;
  readonly totalCount: number;
  readonly totalPages: number;
}

export const DEFAULT_PAGE_SIZE = 10;

/** The most rows the API returns in one page; used to load a whole pick-list. */
export const MAX_PAGE_SIZE = 100;
