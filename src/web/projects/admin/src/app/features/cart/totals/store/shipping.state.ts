import { ApiError } from '@core/http';
import { DEFAULT_PAGE_SIZE } from '@shared';
import { CityDto, ProvinceDto, ShippingRateDto, ShippingSettingsDto } from '../models';

export const SHIPPING_FEATURE_KEY = 'cartShipping';

export interface ShippingState {
  readonly settings: ShippingSettingsDto | null;
  readonly settingsSaving: boolean;
  readonly rates: readonly ShippingRateDto[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
  readonly loading: boolean;
  /** A failed load, or a refusal in the server's words (an unknown city, a fee out of range). */
  readonly error: ApiError | null;
  readonly provinces: readonly ProvinceDto[];
  readonly cities: readonly CityDto[];
  readonly rateSaving: boolean;
  /** The city whose rate is being removed, so only that button waits. */
  readonly removingCityId: number | null;
  readonly notice: string | null;
}

export const initialShippingState: ShippingState = {
  settings: null,
  settingsSaving: false,
  rates: [],
  total: 0,
  page: 1,
  pageSize: DEFAULT_PAGE_SIZE,
  loading: false,
  error: null,
  provinces: [],
  cities: [],
  rateSaving: false,
  removingCityId: null,
  notice: null,
};
