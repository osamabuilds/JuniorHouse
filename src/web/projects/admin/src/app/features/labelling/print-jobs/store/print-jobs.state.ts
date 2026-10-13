import { ApiError } from '@core/http';
import { DEFAULT_PAGE_SIZE } from '@shared';
import { JobStatusFilter, PrintJobDto, PrinterDto } from '../models';

export const PRINT_JOBS_FEATURE_KEY = 'printJobs';

export interface PrintJobsState {
  readonly jobs: readonly PrintJobDto[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
  readonly loading: boolean;
  readonly error: ApiError | null;
  readonly status: JobStatusFilter;
  /** The variant whose history is shown instead of the queue, if any. */
  readonly variantId: number | null;
  readonly printers: readonly PrinterDto[];

  /** The reprint form: which variant (and the SKU to show), and how the submit is going. */
  readonly reprintVariantId: number | null;
  readonly reprintSku: string | null;
  readonly reprintSaving: boolean;
  readonly reprintError: ApiError | null;

  /** The job a retry or cancel is running for, and a short message after something worked. */
  readonly busyJobId: number | null;
  readonly notice: string | null;
}

export const initialPrintJobsState: PrintJobsState = {
  jobs: [],
  total: 0,
  page: 1,
  pageSize: DEFAULT_PAGE_SIZE,
  loading: false,
  error: null,
  status: 'all',
  variantId: null,
  printers: [],
  reprintVariantId: null,
  reprintSku: null,
  reprintSaving: false,
  reprintError: null,
  busyJobId: null,
  notice: null,
};
