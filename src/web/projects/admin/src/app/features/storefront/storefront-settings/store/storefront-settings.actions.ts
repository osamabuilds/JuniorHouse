import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { ApiError } from '@core/http';
import { Page } from '@shared';
import { ContentPage, DraftPart, DraftPatch, DraftSettings, PageRow, PageStatus, PageValue, PublishResult, SnapshotRow } from '../models';

export const StorefrontSettingsPageActions = createActionGroup({
  source: 'Storefront Settings Page',
  events: {
    'Draft Opened': emptyProps(),
    'Save Submitted': props<{ part: DraftPart }>(),
    'Notice Dismissed': emptyProps(),

    'Pages Opened': emptyProps(),
    'Pages Status Filtered': props<{ status: PageStatus | '' }>(),
    'Pages Page Changed': props<{ page: number }>(),
    'Pages Page Size Changed': props<{ pageSize: number }>(),

    'Page Editor Opened': props<{ id: number | null }>(),
    'Page Save Submitted': props<{ value: PageValue }>(),
    'Page Status Submitted': props<{ status: PageStatus }>(),

    'Publish Opened': emptyProps(),
    'Snapshots Page Changed': props<{ page: number }>(),
    'Publish Submitted': props<{ note: string }>(),
    'Restore Submitted': props<{ versionNo: number; note: string }>(),
  },
});

export const StorefrontSettingsApiActions = createActionGroup({
  source: 'Storefront Settings API',
  events: {
    'Load Draft Succeeded': props<{ draft: DraftSettings }>(),
    'Load Draft Failed': props<{ error: ApiError }>(),
    'Save Succeeded': props<{ version: number; patch: DraftPatch }>(),
    'Save Failed': props<{ error: ApiError }>(),

    'Load Pages Succeeded': props<{ result: Page<PageRow> }>(),
    'Load Pages Failed': props<{ error: ApiError }>(),
    'Load Page Succeeded': props<{ page: ContentPage }>(),
    'Load Page Failed': props<{ error: ApiError }>(),
    'Save Page Succeeded': props<{ page: ContentPage; created: boolean }>(),
    'Save Page Failed': props<{ error: ApiError }>(),

    'Load Snapshots Succeeded': props<{ result: Page<SnapshotRow> }>(),
    'Load Snapshots Failed': props<{ error: ApiError }>(),
    'Publish Succeeded': props<{ result: PublishResult }>(),
    'Publish Failed': props<{ error: ApiError }>(),
    'Restore Succeeded': props<{ result: PublishResult }>(),
    'Restore Failed': props<{ error: ApiError }>(),
  },
});
