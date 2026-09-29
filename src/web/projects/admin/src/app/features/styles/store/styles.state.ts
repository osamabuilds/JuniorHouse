import { ApiError } from '@core/http';
import { PageMode } from '@shared';
import { StyleDto, StyleSummaryDto } from '../models';

export const STYLES_FEATURE_KEY = 'styles';

export interface StylesState {
  readonly styles: readonly StyleSummaryDto[];
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
