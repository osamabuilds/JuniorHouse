/** A group of terms that mean the same to a shopper (frock, dress, gown). `rowVersion` is quoted back on update, so a stale save is refused. */
export interface SynonymGroup {
  readonly id: number;
  readonly name: string;
  readonly isActive: boolean;
  readonly terms: readonly string[];
  readonly rowVersion: number;
}

/** What staff type: the terms one per line (or separated by commas). */
export interface SynonymFormValue {
  readonly name: string;
  readonly isActive: boolean;
  readonly terms: readonly string[];
}

/** A search that found nothing: the text, the day and how often (most frequent first). */
export interface MissRow {
  readonly text: string;
  readonly day: string;
  readonly count: number;
}

export interface ListFilters {
  readonly page: number;
  readonly pageSize: number;
}
