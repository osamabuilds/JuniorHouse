import { ApiError } from '@core/http';
import { DEFAULT_PAGE_SIZE } from '@shared';
import { SizeChartDto, SizeChartRowDto } from '../models';

export const SIZE_CHARTS_FEATURE_KEY = 'catalogSizeCharts';

export type SizeChartMode = 'list' | 'create' | 'edit';

export interface SizeChartsState {
  readonly rows: readonly SizeChartRowDto[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
  readonly loading: boolean;
  readonly error: ApiError | null;
  readonly mode: SizeChartMode;
  /** The chart being edited; null while creating or listing. */
  readonly chart: SizeChartDto | null;
  readonly saving: boolean;
  readonly formError: ApiError | null;
  readonly notice: string | null;
}

export const initialSizeChartsState: SizeChartsState = {
  rows: [],
  total: 0,
  page: 1,
  pageSize: DEFAULT_PAGE_SIZE,
  loading: false,
  error: null,
  mode: 'list',
  chart: null,
  saving: false,
  formError: null,
  notice: null,
};
