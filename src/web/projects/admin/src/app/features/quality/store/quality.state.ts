import { ApiError } from '@core/http';
import { DEFAULT_PAGE_SIZE } from '@shared';
import {
  AqlPlanDto,
  GoodsReceiptDto,
  InboundDeliveryDto,
  InboundDeliverySummaryDto,
  QcBatchDto,
  QcBatchSummaryDto,
  QualityPanel,
} from '../models';

export const QUALITY_FEATURE_KEY = 'quality';

export interface QualityState {
  // ---- the receiving queue (inbound deliveries) ----
  readonly deliveries: readonly InboundDeliverySummaryDto[];
  readonly deliveriesTotal: number;
  readonly deliveriesPage: number;
  readonly deliveriesPageSize: number;
  readonly deliveriesLoading: boolean;
  readonly deliveriesError: ApiError | null;
  readonly deliveryStatusFilter: string | null;
  readonly deliverySearch: string;

  // ---- receiving one delivery ----
  readonly delivery: InboundDeliveryDto | null;
  readonly deliveryLoading: boolean;
  readonly deliveryError: ApiError | null;
  readonly receiptSaving: boolean;
  readonly receiptError: ApiError | null;
  /** The goods receipt just recorded, so the page can show the reconciliation and link on to the batch. */
  readonly receipt: GoodsReceiptDto | null;

  // ---- the QC batch list ----
  readonly batches: readonly QcBatchSummaryDto[];
  readonly batchesTotal: number;
  readonly batchesPage: number;
  readonly batchesPageSize: number;
  readonly batchesLoading: boolean;
  readonly batchesError: ApiError | null;
  readonly batchStatusFilter: number | null;
  readonly batchSearch: string;

  // ---- one batch ----
  readonly batch: QcBatchDto | null;
  readonly batchLoading: boolean;
  readonly batchError: ApiError | null;
  readonly panel: QualityPanel;
  /** The sub-batch or inspection the open panel is about. */
  readonly panelTargetId: number | null;
  readonly plan: AqlPlanDto | null;
  readonly saving: boolean;
  readonly panelError: ApiError | null;
  /** A short confirmation of the last thing saved, e.g. "Inspection recorded." */
  readonly notice: string | null;
}

export const initialQualityState: QualityState = {
  deliveries: [],
  deliveriesTotal: 0,
  deliveriesPage: 1,
  deliveriesPageSize: DEFAULT_PAGE_SIZE,
  deliveriesLoading: false,
  deliveriesError: null,
  deliveryStatusFilter: 'open',
  deliverySearch: '',

  delivery: null,
  deliveryLoading: false,
  deliveryError: null,
  receiptSaving: false,
  receiptError: null,
  receipt: null,

  batches: [],
  batchesTotal: 0,
  batchesPage: 1,
  batchesPageSize: DEFAULT_PAGE_SIZE,
  batchesLoading: false,
  batchesError: null,
  batchStatusFilter: null,
  batchSearch: '',

  batch: null,
  batchLoading: false,
  batchError: null,
  panel: 'none',
  panelTargetId: null,
  plan: null,
  saving: false,
  panelError: null,
  notice: null,
};
