import { ApiError } from '@core/http';
import { DEFAULT_PAGE_SIZE } from '@shared';
import { StockMovementDto, StockVariantRowDto } from '../models';

export const STOCK_FEATURE_KEY = 'warehouseStock';

export interface StockState {
  readonly rows: readonly StockVariantRowDto[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
  readonly loading: boolean;
  readonly error: ApiError | null;
  readonly searchText: string;

  /** The variant whose movement history is open, and its page of movements. */
  readonly historyFor: StockVariantRowDto | null;
  readonly movements: readonly StockMovementDto[];
  readonly movementsTotal: number;
  readonly movementsPage: number;
  readonly movementsLoading: boolean;
}

export const initialStockState: StockState = {
  rows: [],
  total: 0,
  page: 1,
  pageSize: DEFAULT_PAGE_SIZE,
  loading: false,
  error: null,
  searchText: '',
  historyFor: null,
  movements: [],
  movementsTotal: 0,
  movementsPage: 1,
  movementsLoading: false,
};
