import { ApiError } from '@core/http';
import { DEFAULT_PAGE_SIZE, PageMode } from '@shared';
import { StyleDto, StyleSummaryDto } from '../models';

export const STYLES_FEATURE_KEY = 'styles';

export interface StylesState {
  readonly styles: readonly StyleSummaryDto[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
  readonly loading: boolean;
  readonly error: ApiError | null;
  readonly mode: PageMode;
  readonly editingId: number | null;
  /** The full record being edited, once it has loaded. */
  readonly editingStyle: StyleDto | null;
  readonly saving: boolean;
  readonly searchText: string;
  readonly categoryFilter: number | null;
  readonly activeOnly: boolean;
}

export const initialStylesState: StylesState = {
  styles: [],
  total: 0,
  page: 1,
  pageSize: DEFAULT_PAGE_SIZE,
  loading: false,
  error: null,
  mode: 'list',
  editingId: null,
  editingStyle: null,
  saving: false,
  searchText: '',
  categoryFilter: null,
  activeOnly: true,
};
