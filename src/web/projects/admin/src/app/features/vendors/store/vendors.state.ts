import { ApiError } from '@core/http';
import { PageMode } from '@shared';
import { VendorDto, VendorSummaryDto } from '../models';

export const VENDORS_FEATURE_KEY = 'vendors';

export interface VendorsState {
  readonly vendors: readonly VendorSummaryDto[];
  readonly loading: boolean;
  readonly error: ApiError | null;
  readonly mode: PageMode;
  readonly editingId: number | null;
  /** The full record being edited, once it has loaded. */
  readonly editingVendor: VendorDto | null;
  readonly saving: boolean;
  readonly searchText: string;
  readonly specialisationFilter: number | null;
  readonly activeOnly: boolean;
}

export const initialVendorsState: VendorsState = {
  vendors: [],
  loading: false,
  error: null,
  mode: 'list',
  editingId: null,
  editingVendor: null,
  saving: false,
  searchText: '',
  specialisationFilter: null,
  activeOnly: true,
};
