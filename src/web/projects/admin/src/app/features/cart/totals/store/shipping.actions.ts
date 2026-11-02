import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { ApiError } from '@core/http';
import { Page } from '@shared';
import { CityDto, ProvinceDto, SettingsValue, ShippingRateDto, ShippingSettingsDto } from '../models';

export const ShippingPageActions = createActionGroup({
  source: 'Shipping Page',
  events: {
    Opened: emptyProps(),
    'Page Changed': props<{ page: number }>(),
    'Page Size Changed': props<{ pageSize: number }>(),
    'Settings Submitted': props<{ value: SettingsValue }>(),
    'Province Chosen': props<{ provinceId: number }>(),
    'Rate Submitted': props<{ cityId: number; fee: number }>(),
    'Rate Removed': props<{ cityId: number }>(),
    'Notice Dismissed': emptyProps(),
  },
});

export const ShippingApiActions = createActionGroup({
  source: 'Shipping API',
  events: {
    'Load Rates Succeeded': props<{ result: Page<ShippingRateDto> }>(),
    'Load Rates Failed': props<{ error: ApiError }>(),
    'Load Settings Succeeded': props<{ settings: ShippingSettingsDto }>(),
    'Load Settings Failed': props<{ error: ApiError }>(),
    'Save Settings Succeeded': props<{ settings: ShippingSettingsDto }>(),
    'Save Settings Failed': props<{ error: ApiError }>(),
    'Load Provinces Succeeded': props<{ provinces: readonly ProvinceDto[] }>(),
    'Load Cities Succeeded': props<{ cities: readonly CityDto[] }>(),
    'Save Rate Succeeded': props<{ rate: ShippingRateDto }>(),
    'Save Rate Failed': props<{ error: ApiError }>(),
    'Remove Rate Succeeded': props<{ cityId: number }>(),
    'Remove Rate Failed': props<{ error: ApiError }>(),
  },
});
