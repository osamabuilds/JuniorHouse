/** One low-stock or stock-out alert (docs/specs/warehouse/stock-alerts/spec.md, ALR-8). */
export interface AlertRowDto {
  readonly id: number;
  readonly variantRefId: number;
  readonly skuCode: string;
  readonly gtin: string;
  readonly levelId: number;
  /** LOW or OUT. */
  readonly levelCode: string;
  readonly available: number;
  readonly threshold: number;
  readonly openedAt: string;
  readonly acknowledged: boolean;
  readonly acknowledgedBy: string | null;
  readonly acknowledgedAt: string | null;
}

/** A variant's low-stock threshold: its own override, or the configured default (ALR-6, decision D13). */
export interface ThresholdRowDto {
  readonly variantRefId: number;
  readonly skuCode: string;
  readonly gtin: string;
  readonly available: number;
  readonly effectiveThreshold: number;
  readonly isOverride: boolean;
}

/** The numbers behind the navigation badge (ALR-8). */
export interface OpenAlertCountDto {
  readonly open: number;
  readonly unacknowledged: number;
}

export type AlertLevelFilter = 'all' | 'LOW' | 'OUT';

export interface AlertFilters {
  readonly level: AlertLevelFilter;
  readonly unacknowledgedOnly: boolean;
  readonly page: number;
  readonly pageSize: number;
}

export interface ThresholdFilters {
  readonly searchText: string;
  readonly page: number;
  readonly pageSize: number;
}
