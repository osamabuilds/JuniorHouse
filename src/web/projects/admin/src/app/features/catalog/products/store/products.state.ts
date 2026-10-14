import { ApiError } from '@core/http';
import { DEFAULT_PAGE_SIZE } from '@shared';
import {
  CompositionCareDto,
  GateFailureDto,
  PathHistoryDto,
  ProductDto,
  ProductImageDto,
  ProductRowDto,
  ProductSizeChartDto,
  SlugSuggestionDto,
} from '../models';

export const PRODUCTS_FEATURE_KEY = 'catalogProducts';

/** The product list and the product editor share one store: the editor is opened from the list and the list reloads after a create. */
export interface ProductsState {
  // List
  readonly rows: readonly ProductRowDto[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
  readonly search: string;
  readonly statusId: number | null;
  readonly listLoading: boolean;
  readonly listError: ApiError | null;
  readonly createOpen: boolean;
  readonly creating: boolean;
  readonly createError: ApiError | null;

  // Editor
  readonly product: ProductDto | null;
  readonly images: readonly ProductImageDto[];
  readonly composition: CompositionCareDto | null;
  readonly chart: ProductSizeChartDto | null;
  readonly history: readonly PathHistoryDto[];
  readonly suggestion: SlugSuggestionDto | null;
  readonly loading: boolean;
  readonly saving: boolean;
  readonly error: ApiError | null;
  readonly notice: string | null;

  /** Every failure of the last refused publish, each pointing at the tab that fixes it (PRD-4). */
  readonly publishFailures: readonly GateFailureDto[] | null;
}

export const initialProductsState: ProductsState = {
  rows: [],
  total: 0,
  page: 1,
  pageSize: DEFAULT_PAGE_SIZE,
  search: '',
  statusId: null,
  listLoading: false,
  listError: null,
  createOpen: false,
  creating: false,
  createError: null,
  product: null,
  images: [],
  composition: null,
  chart: null,
  history: [],
  suggestion: null,
  loading: false,
  saving: false,
  error: null,
  notice: null,
  publishFailures: null,
};
