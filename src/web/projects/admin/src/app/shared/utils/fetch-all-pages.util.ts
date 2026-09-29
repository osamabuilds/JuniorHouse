import { EMPTY, Observable, expand, map, reduce } from 'rxjs';
import { MAX_PAGE_SIZE, Page } from '../models';

/**
 * Loads every page of a server-side list, for the few places that need the whole set (a
 * pick-list). Pages are requested one after another at the API's maximum page size.
 */
export function fetchAllPages<T>(fetchPage: (page: number, pageSize: number) => Observable<Page<T>>): Observable<T[]> {
  return fetchPage(1, MAX_PAGE_SIZE).pipe(
    expand((result) => (result.page < result.totalPages ? fetchPage(result.page + 1, MAX_PAGE_SIZE) : EMPTY)),
    map((result) => result.items),
    reduce((all, items) => [...all, ...items], [] as T[]),
  );
}
