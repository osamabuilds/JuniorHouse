import { createFeatureSelector, createSelector } from '@ngrx/store';
import { SHIPPING_FEATURE_KEY, ShippingState } from './shipping.state';

const selectShippingState = createFeatureSelector<ShippingState>(SHIPPING_FEATURE_KEY);

export const selectSettings = createSelector(selectShippingState, (s) => s.settings);
export const selectSettingsSaving = createSelector(selectShippingState, (s) => s.settingsSaving);
export const selectRates = createSelector(selectShippingState, (s) => s.rates);
export const selectTotal = createSelector(selectShippingState, (s) => s.total);
export const selectPage = createSelector(selectShippingState, (s) => s.page);
export const selectPageSize = createSelector(selectShippingState, (s) => s.pageSize);
export const selectLoading = createSelector(selectShippingState, (s) => s.loading);
export const selectError = createSelector(selectShippingState, (s) => s.error);
export const selectProvinces = createSelector(selectShippingState, (s) => s.provinces);
export const selectCities = createSelector(selectShippingState, (s) => s.cities);
export const selectRateSaving = createSelector(selectShippingState, (s) => s.rateSaving);
export const selectRemovingCityId = createSelector(selectShippingState, (s) => s.removingCityId);
export const selectNotice = createSelector(selectShippingState, (s) => s.notice);

export const selectPaging = createSelector(selectPage, selectPageSize, (page, pageSize) => ({ page, pageSize }));
