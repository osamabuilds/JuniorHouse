import { ApiError } from '@core/http';
import { DEFAULT_PAGE_SIZE } from '@shared';
import { ProductionPanel, RunDetailDto, RunSummaryDto } from '../models';

export const PRODUCTION_FEATURE_KEY = 'production';

export interface ProductionState {
  // ---- the list ----
  readonly runs: readonly RunSummaryDto[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
  readonly loading: boolean;
  readonly error: ApiError | null;
  readonly statusFilter: number | null;
  readonly searchText: string;
  readonly atRiskOnly: boolean;

  // ---- one run ----
  readonly run: RunDetailDto | null;
  readonly runLoading: boolean;
  readonly runError: ApiError | null;
  readonly panel: ProductionPanel;
  readonly saving: boolean;
  readonly panelError: ApiError | null;
  /** A short confirmation of the last thing saved, e.g. "Milestone recorded." */
  readonly notice: string | null;
  readonly fileUploading: boolean;
  readonly fileError: ApiError | null;
}

export const initialProductionState: ProductionState = {
  runs: [],
  total: 0,
  page: 1,
  pageSize: DEFAULT_PAGE_SIZE,
  loading: false,
  error: null,
  statusFilter: null,
  searchText: '',
  atRiskOnly: false,

  run: null,
  runLoading: false,
  runError: null,
  panel: 'none',
  saving: false,
  panelError: null,
  notice: null,
  fileUploading: false,
  fileError: null,
};
