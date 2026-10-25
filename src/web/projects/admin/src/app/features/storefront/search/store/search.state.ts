import { ApiError } from '@core/http';
import { DEFAULT_PAGE_SIZE, PageMode } from '@shared';
import { MissRow, SynonymGroup } from '../models';

export const SEARCH_FEATURE_KEY = 'storefrontSearch';

export interface SearchAdminState {
  readonly synonyms: readonly SynonymGroup[];
  readonly synonymsTotal: number;
  readonly synonymsPage: number;
  readonly synonymsPageSize: number;
  readonly synonymsLoading: boolean;
  readonly synonymsError: ApiError | null;

  /** list, or the create / edit form. */
  readonly mode: PageMode;
  readonly editing: SynonymGroup | null;
  readonly saving: boolean;
  readonly formError: ApiError | null;
  /** The group whose delete is waiting for a second click. */
  readonly deletingId: number | null;

  readonly misses: readonly MissRow[];
  readonly missesTotal: number;
  readonly missesPage: number;
  readonly missesPageSize: number;
  readonly missesLoading: boolean;
  readonly missesError: ApiError | null;
}

export const initialSearchAdminState: SearchAdminState = {
  synonyms: [],
  synonymsTotal: 0,
  synonymsPage: 1,
  synonymsPageSize: DEFAULT_PAGE_SIZE,
  synonymsLoading: false,
  synonymsError: null,
  mode: 'list',
  editing: null,
  saving: false,
  formError: null,
  deletingId: null,
  misses: [],
  missesTotal: 0,
  missesPage: 1,
  missesPageSize: DEFAULT_PAGE_SIZE,
  missesLoading: false,
  missesError: null,
};
