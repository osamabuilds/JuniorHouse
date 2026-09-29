export interface Paged<T> {
  readonly items: readonly T[];
  /** The requested page, clamped into range (a filter can leave fewer pages than before). */
  readonly page: number;
}

export function paginate<T>(items: readonly T[], page: number, pageSize: number): Paged<T> {
  const lastPage = Math.max(1, Math.ceil(items.length / pageSize));
  const current = Math.min(Math.max(1, page), lastPage);
  return { items: items.slice((current - 1) * pageSize, current * pageSize), page: current };
}
