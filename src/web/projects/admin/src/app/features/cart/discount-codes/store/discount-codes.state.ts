import { ApiError } from '@core/http';
import { DEFAULT_PAGE_SIZE } from '@shared';
import { DiscountCodeDto } from '../models';

export const DISCOUNT_CODES_FEATURE_KEY = 'cartDiscountCodes';

export interface DiscountCodesState {
  readonly codes: readonly DiscountCodeDto[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
  readonly loading: boolean;
  readonly saving: boolean;
  /** The code being changed in the form, or null while a new one is being written. */
  readonly editingId: number | null;
  /** The code whose switch is being flipped, so only its button waits. */
  readonly switchingId: number | null;
  /** A failed load, or a refusal in the server's words (a duplicate code, a percentage over 100). */
  readonly error: ApiError | null;
  readonly notice: string | null;
}

export const initialDiscountCodesState: DiscountCodesState = {
  codes: [],
  total: 0,
  page: 1,
  pageSize: DEFAULT_PAGE_SIZE,
  loading: false,
  saving: false,
  editingId: null,
  switchingId: null,
  error: null,
  notice: null,
};
