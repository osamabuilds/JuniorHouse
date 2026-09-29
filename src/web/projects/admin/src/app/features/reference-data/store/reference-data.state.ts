import { ApiError } from '@core/http';
import { PageMode } from '@shared';
import { LOOKUP_TYPES } from '../reference-data.constants';
import { LookupDto } from '../models';

export const REFERENCE_DATA_FEATURE_KEY = 'referenceData';

export interface ReferenceDataState {
  readonly selectedTypeKey: string;
  readonly items: readonly LookupDto[];
  readonly loading: boolean;
  readonly error: ApiError | null;
  readonly includeInactive: boolean;
  readonly mode: PageMode;
  readonly editingId: number | null;
  readonly saving: boolean;
  readonly searchText: string;
  /** Lookup lists other screens choose from, keyed by `lookupCacheKey`. */
  readonly lookups: Readonly<Record<string, readonly LookupDto[]>>;
}

export const initialReferenceDataState: ReferenceDataState = {
  selectedTypeKey: LOOKUP_TYPES[0].key,
  items: [],
  loading: false,
  error: null,
  includeInactive: false,
  mode: 'list',
  editingId: null,
  saving: false,
  searchText: '',
  lookups: {},
};
