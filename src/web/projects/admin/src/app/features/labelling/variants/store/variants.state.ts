import { ApiError } from '@core/http';
import { DEFAULT_PAGE_SIZE } from '@shared';
import { GtinCapacityDto, VariantDetailDto, VariantRowDto, VariantStatusFilter } from '../models';

export const VARIANTS_FEATURE_KEY = 'variants';

export interface VariantsState {
  readonly variants: readonly VariantRowDto[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
  readonly loading: boolean;
  readonly error: ApiError | null;
  readonly searchText: string;
  readonly status: VariantStatusFilter;
  readonly capacity: GtinCapacityDto | null;

  /** The variant whose GTIN history is open. */
  readonly detail: VariantDetailDto | null;
  readonly detailLoading: boolean;

  /** The variant whose void form is open, and how the submit is going. */
  readonly voidTarget: VariantRowDto | null;
  readonly voidSaving: boolean;
  readonly voidError: ApiError | null;
  readonly notice: string | null;
}

export const initialVariantsState: VariantsState = {
  variants: [],
  total: 0,
  page: 1,
  pageSize: DEFAULT_PAGE_SIZE,
  loading: false,
  error: null,
  searchText: '',
  status: 'all',
  capacity: null,
  detail: null,
  detailLoading: false,
  voidTarget: null,
  voidSaving: false,
  voidError: null,
  notice: null,
};
