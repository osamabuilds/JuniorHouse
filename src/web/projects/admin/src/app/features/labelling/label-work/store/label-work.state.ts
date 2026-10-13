import { ApiError } from '@core/http';
import { DEFAULT_PAGE_SIZE } from '@shared';
import { LabelWorkItemDto, PrinterDto, WorkStatusFilter } from '../models';

export const LABEL_WORK_FEATURE_KEY = 'labelWork';

export interface LabelWorkState {
  readonly items: readonly LabelWorkItemDto[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
  readonly loading: boolean;
  readonly error: ApiError | null;
  readonly status: WorkStatusFilter;
  readonly priceFlagOnly: boolean;
  readonly printers: readonly PrinterDto[];

  /** The item whose "queue tags" form is open, and how the submit is going. */
  readonly queueItem: LabelWorkItemDto | null;
  readonly queueSaving: boolean;
  readonly queueError: ApiError | null;

  /** The item being resolved right now, and a short message after something was queued or resolved. */
  readonly resolvingId: number | null;
  readonly notice: string | null;
}

export const initialLabelWorkState: LabelWorkState = {
  items: [],
  total: 0,
  page: 1,
  pageSize: DEFAULT_PAGE_SIZE,
  loading: false,
  error: null,
  status: 'all',
  priceFlagOnly: false,
  printers: [],
  queueItem: null,
  queueSaving: false,
  queueError: null,
  resolvingId: null,
  notice: null,
};
