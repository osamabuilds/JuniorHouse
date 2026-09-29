import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { ApiError } from '@core/http';
import { Page } from '@shared';
import {
  AqlPlanDto,
  DecideBatchValue,
  DecideSubBatchValue,
  GoodsReceiptDto,
  InboundDeliveryDto,
  InboundDeliverySummaryDto,
  QcBatchDto,
  QcBatchSummaryDto,
  QualityPanel,
  RecordInspectionValue,
  RecordReceiptValue,
  RecordSafetyValue,
  ReworkReturnValue,
} from '../models';

export const ReceivingQueueActions = createActionGroup({
  source: 'Receiving Queue',
  events: {
    Opened: emptyProps(),
    'Search Changed': props<{ searchText: string }>(),
    'Status Filter Changed': props<{ status: string | null }>(),
    'Page Changed': props<{ page: number }>(),
    'Page Size Changed': props<{ pageSize: number }>(),
  },
});

export const ReceivePageActions = createActionGroup({
  source: 'Receive Page',
  events: {
    Opened: props<{ id: number }>(),
    'Receipt Submitted': props<{ value: RecordReceiptValue }>(),
  },
});

export const BatchListActions = createActionGroup({
  source: 'QC Batch List',
  events: {
    Opened: emptyProps(),
    'Search Changed': props<{ searchText: string }>(),
    'Status Filter Changed': props<{ statusId: number | null }>(),
    'Page Changed': props<{ page: number }>(),
    'Page Size Changed': props<{ pageSize: number }>(),
  },
});

export const BatchPageActions = createActionGroup({
  source: 'QC Batch Page',
  events: {
    Opened: props<{ id: number }>(),
    'Panel Opened': props<{ panel: Exclude<QualityPanel, 'none'>; targetId: number | null }>(),
    'Panel Closed': emptyProps(),
    'Plan Requested': props<{ lotQty: number }>(),
    'Inspection Submitted': props<{ value: RecordInspectionValue }>(),
    'Safety Checks Submitted': props<{ value: RecordSafetyValue }>(),
    'Decision Submitted': props<{ value: DecideBatchValue }>(),
    'Part Decision Submitted': props<{ value: DecideSubBatchValue }>(),
    'Rework Return Submitted': props<{ value: ReworkReturnValue }>(),
  },
});

export const QualityApiActions = createActionGroup({
  source: 'Quality API',
  events: {
    'Load Deliveries Succeeded': props<{ result: Page<InboundDeliverySummaryDto> }>(),
    'Load Deliveries Failed': props<{ error: ApiError }>(),
    'Load Delivery Succeeded': props<{ delivery: InboundDeliveryDto }>(),
    'Load Delivery Failed': props<{ error: ApiError }>(),
    'Receipt Succeeded': props<{ receipt: GoodsReceiptDto }>(),
    'Receipt Failed': props<{ error: ApiError }>(),
    'Load Batches Succeeded': props<{ result: Page<QcBatchSummaryDto> }>(),
    'Load Batches Failed': props<{ error: ApiError }>(),
    'Load Batch Succeeded': props<{ batch: QcBatchDto }>(),
    'Load Batch Failed': props<{ error: ApiError }>(),
    'Plan Loaded': props<{ plan: AqlPlanDto }>(),
    'Plan Failed': props<{ error: ApiError }>(),
    'Action Succeeded': props<{ notice: string }>(),
    'Action Failed': props<{ error: ApiError }>(),
  },
});
