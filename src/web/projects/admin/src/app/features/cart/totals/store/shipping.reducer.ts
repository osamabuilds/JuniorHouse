import { createReducer, on } from '@ngrx/store';
import { ShippingApiActions, ShippingPageActions } from './shipping.actions';
import { initialShippingState } from './shipping.state';

export const shippingReducer = createReducer(
  initialShippingState,

  // The store outlives the page, so opening it starts from a clean slate.
  on(ShippingPageActions.opened, () => ({ ...initialShippingState, loading: true })),
  on(ShippingPageActions.pageChanged, (state, { page }) => ({ ...state, page, loading: true, error: null })),
  on(ShippingPageActions.pageSizeChanged, (state, { pageSize }) => ({ ...state, pageSize, page: 1, loading: true, error: null })),
  on(ShippingPageActions.settingsSubmitted, (state) => ({ ...state, settingsSaving: true, error: null, notice: null })),
  on(ShippingPageActions.provinceChosen, (state) => ({ ...state, cities: [] })),
  on(ShippingPageActions.rateSubmitted, (state) => ({ ...state, rateSaving: true, error: null, notice: null })),
  on(ShippingPageActions.rateRemoved, (state, { cityId }) => ({ ...state, removingCityId: cityId, error: null, notice: null })),
  on(ShippingPageActions.noticeDismissed, (state) => ({ ...state, notice: null })),

  on(ShippingApiActions.loadRatesSucceeded, (state, { result }) => ({ ...state, rates: result.items, total: result.totalCount, page: result.page, loading: false })),
  on(ShippingApiActions.loadRatesFailed, ShippingApiActions.loadSettingsFailed, (state, { error }) => ({ ...state, error, loading: false })),
  on(ShippingApiActions.loadSettingsSucceeded, (state, { settings }) => ({ ...state, settings })),
  on(ShippingApiActions.saveSettingsSucceeded, (state, { settings }) => ({ ...state, settings, settingsSaving: false, notice: 'The delivery settings were saved. They apply to the next basket.' })),
  on(ShippingApiActions.saveSettingsFailed, (state, { error }) => ({ ...state, settingsSaving: false, error })),
  on(ShippingApiActions.loadProvincesSucceeded, (state, { provinces }) => ({ ...state, provinces })),
  on(ShippingApiActions.loadCitiesSucceeded, (state, { cities }) => ({ ...state, cities })),
  on(ShippingApiActions.saveRateSucceeded, (state, { rate }) => ({
    ...state,
    rateSaving: false,
    loading: true,
    notice: `Delivery to ${rate.cityName} now costs Rs. ${rate.fee}.`,
  })),
  on(ShippingApiActions.saveRateFailed, (state, { error }) => ({ ...state, rateSaving: false, error })),
  on(ShippingApiActions.removeRateSucceeded, (state) => ({ ...state, removingCityId: null, loading: true, notice: 'The city now uses the default fee.' })),
  on(ShippingApiActions.removeRateFailed, (state, { error }) => ({ ...state, removingCityId: null, error })),
);
