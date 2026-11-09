import { ApiError } from '@core/http';
import { PackingSlip } from '../models';

export const PACKING_SLIPS_FEATURE_KEY = 'packingSlips';

export interface PackingSlipsState {
  readonly slip: PackingSlip | null;
  readonly loading: boolean;
  /** A send to the printer is on its way, so its button waits. */
  readonly printing: boolean;
  readonly error: ApiError | null;
  readonly notice: string | null;
}

export const initialPackingSlipsState: PackingSlipsState = { slip: null, loading: false, printing: false, error: null, notice: null };
