import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { ApiError } from '@core/http';
import { Page } from '@shared';
import {
  DecideSampleValue,
  DeliveryNoteValue,
  ExpectedCompletionValue,
  ProductionPanel,
  RecordMilestoneValue,
  RunDetailDto,
  RunSummaryDto,
  SampleFileUpload,
  SubmitSampleValue,
} from '../models';

export const ProductionPageActions = createActionGroup({
  source: 'Production Page',
  events: {
    'List Opened': emptyProps(),
    'Search Changed': props<{ searchText: string }>(),
    'Status Filter Changed': props<{ statusId: number | null }>(),
    'At Risk Toggled': emptyProps(),
    'Page Changed': props<{ page: number }>(),
    'Page Size Changed': props<{ pageSize: number }>(),
  },
});

export const ProductionRunActions = createActionGroup({
  source: 'Production Run',
  events: {
    Opened: props<{ id: number }>(),
    'Panel Opened': props<{ panel: Exclude<ProductionPanel, 'none'> }>(),
    'Panel Closed': emptyProps(),
    'Milestone Submitted': props<{ value: RecordMilestoneValue }>(),
    'Sample Submitted': props<{ value: SubmitSampleValue }>(),
    'Sample Decided': props<{ value: DecideSampleValue }>(),
    'Expected Completion Submitted': props<{ value: ExpectedCompletionValue }>(),
    'Delivery Note Submitted': props<{ value: DeliveryNoteValue }>(),
    'Sample File Upload Requested': props<{ upload: SampleFileUpload }>(),
    'Sample File Remove Requested': props<{ roundNo: number; fileId: number }>(),
  },
});

export const ProductionApiActions = createActionGroup({
  source: 'Production API',
  events: {
    'Load Runs Succeeded': props<{ result: Page<RunSummaryDto> }>(),
    'Load Runs Failed': props<{ error: ApiError }>(),
    'Load Run Succeeded': props<{ run: RunDetailDto }>(),
    'Load Run Failed': props<{ error: ApiError }>(),
    'Action Succeeded': props<{ notice: string }>(),
    'Action Failed': props<{ error: ApiError }>(),
    'File Upload Succeeded': emptyProps(),
    'File Upload Failed': props<{ error: ApiError }>(),
  },
});
