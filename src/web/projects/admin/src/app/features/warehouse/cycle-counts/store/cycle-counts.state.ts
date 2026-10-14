import { ApiError } from '@core/http';
import { DEFAULT_PAGE_SIZE } from '@shared';
import { BinAgeDto, CountReviewDto, CountStatusFilter, CycleCountDto, CycleCountRowDto, ReasonChoice } from '../models';
import { AdjustmentReasonDto } from '@features/warehouse/adjustments';

export const CYCLE_COUNTS_FEATURE_KEY = 'warehouseCycleCounts';

export interface CycleCountsState {
  /** The counts list. */
  readonly counts: readonly CycleCountRowDto[];
  readonly countsTotal: number;
  readonly countsPage: number;
  readonly countsLoading: boolean;
  readonly statusFilter: CountStatusFilter;

  /** The bins to choose from when starting a count, oldest-counted first, and the ones ticked. */
  readonly bins: readonly BinAgeDto[];
  readonly binsTotal: number;
  readonly binsPage: number;
  readonly binsLoading: boolean;
  /** Narrows the bins to choose from to one zone, so a specific bin can be found among many. */
  readonly binZone: string;
  readonly selectedBinIds: readonly number[];
  readonly creating: boolean;

  /** The count being worked on: the blind read, the review, and what the person has chosen for each variance. */
  readonly detail: CycleCountDto | null;
  readonly detailLoading: boolean;
  readonly submitting: boolean;
  readonly review: CountReviewDto | null;
  readonly reviewLoading: boolean;
  readonly choices: Readonly<Record<string, ReasonChoice>>;
  readonly confirming: boolean;
  readonly reasons: readonly AdjustmentReasonDto[];

  readonly pageSize: number;
  readonly error: ApiError | null;
  readonly notice: string | null;
}

export const initialCycleCountsState: CycleCountsState = {
  counts: [],
  countsTotal: 0,
  countsPage: 1,
  countsLoading: false,
  statusFilter: 'all',
  bins: [],
  binsTotal: 0,
  binsPage: 1,
  binsLoading: false,
  binZone: '',
  selectedBinIds: [],
  creating: false,
  detail: null,
  detailLoading: false,
  submitting: false,
  review: null,
  reviewLoading: false,
  choices: {},
  confirming: false,
  reasons: [],
  pageSize: DEFAULT_PAGE_SIZE,
  error: null,
  notice: null,
};
