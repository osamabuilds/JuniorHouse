import { ApiError } from '@core/http';
import { DEFAULT_PAGE_SIZE } from '@shared';
import { AlertLevelFilter, AlertRowDto, ThresholdRowDto } from '../models';

export const STOCK_ALERTS_FEATURE_KEY = 'warehouseStockAlerts';

export interface StockAlertsState {
  readonly alerts: readonly AlertRowDto[];
  readonly alertsTotal: number;
  readonly alertsPage: number;
  readonly alertsLoading: boolean;
  readonly level: AlertLevelFilter;
  readonly unacknowledgedOnly: boolean;
  /** The alert being acknowledged, so only that button is disabled. */
  readonly acknowledgingId: number | null;

  readonly thresholds: readonly ThresholdRowDto[];
  readonly thresholdsTotal: number;
  readonly thresholdsPage: number;
  readonly thresholdsLoading: boolean;
  readonly searchText: string;
  /** The variant whose threshold is being saved. */
  readonly savingVariantId: number | null;

  readonly pageSize: number;
  readonly error: ApiError | null;
  readonly notice: string | null;
}

export const initialStockAlertsState: StockAlertsState = {
  alerts: [],
  alertsTotal: 0,
  alertsPage: 1,
  alertsLoading: false,
  level: 'all',
  unacknowledgedOnly: false,
  acknowledgingId: null,
  thresholds: [],
  thresholdsTotal: 0,
  thresholdsPage: 1,
  thresholdsLoading: false,
  searchText: '',
  savingVariantId: null,
  pageSize: DEFAULT_PAGE_SIZE,
  error: null,
  notice: null,
};
