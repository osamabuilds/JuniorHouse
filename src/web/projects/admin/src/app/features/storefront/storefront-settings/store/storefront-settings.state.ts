import { ApiError } from '@core/http';
import { DEFAULT_PAGE_SIZE } from '@shared';
import { ContentPage, DraftSettings, PageRow, PageStatus, SnapshotRow } from '../models';

export const STOREFRONT_SETTINGS_FEATURE_KEY = 'storefrontSettings';

export interface StorefrontSettingsState {
  /** The working copy as last loaded or saved. Its `version` is quoted on every save (ST-14). */
  readonly draft: DraftSettings | null;
  readonly loading: boolean;
  readonly loadError: ApiError | null;

  /** One save at a time across the five part screens; each screen reads its own errors by field name. */
  readonly saving: boolean;
  readonly saveError: ApiError | null;
  readonly notice: string | null;

  readonly pages: readonly PageRow[];
  readonly pagesTotal: number;
  readonly pagesPage: number;
  readonly pagesPageSize: number;
  readonly pagesStatus: PageStatus | '';
  readonly pagesLoading: boolean;
  readonly pagesError: ApiError | null;

  /** The page being edited; null for a new page. */
  readonly page: ContentPage | null;
  readonly pageLoading: boolean;
  readonly pageSaving: boolean;
  readonly pageError: ApiError | null;
  readonly pageNotice: string | null;

  readonly snapshots: readonly SnapshotRow[];
  readonly snapshotsTotal: number;
  readonly snapshotsPage: number;
  readonly snapshotsPageSize: number;
  readonly snapshotsLoading: boolean;
  readonly publishing: boolean;
  readonly restoringVersion: number | null;
  readonly publishError: ApiError | null;
  readonly publishNotice: string | null;
}

export const initialStorefrontSettingsState: StorefrontSettingsState = {
  draft: null,
  loading: false,
  loadError: null,
  saving: false,
  saveError: null,
  notice: null,
  pages: [],
  pagesTotal: 0,
  pagesPage: 1,
  pagesPageSize: DEFAULT_PAGE_SIZE,
  pagesStatus: '',
  pagesLoading: false,
  pagesError: null,
  page: null,
  pageLoading: false,
  pageSaving: false,
  pageError: null,
  pageNotice: null,
  snapshots: [],
  snapshotsTotal: 0,
  snapshotsPage: 1,
  snapshotsPageSize: DEFAULT_PAGE_SIZE,
  snapshotsLoading: false,
  publishing: false,
  restoringVersion: null,
  publishError: null,
  publishNotice: null,
};
