import { ShippingApiActions, ShippingPageActions } from './shipping.actions';
import { shippingReducer } from './shipping.reducer';
import { initialShippingState } from './shipping.state';

const rate = { cityId: 1, cityName: 'Karachi', provinceName: 'Sindh', fee: 250 };
const apiError = { status: 400, message: 'That city is not in the list.' } as never;

describe('shippingReducer', () => {
  it('opening the page starts from a clean slate and asks for the list', () => {
    const dirty = { ...initialShippingState, notice: 'old', error: apiError, rates: [rate] };

    const state = shippingReducer(dirty, ShippingPageActions.opened());

    expect(state.rates).toEqual([]);
    expect(state.notice).toBeNull();
    expect(state.loading).toBe(true);
  });

  it('TOT-5 a loaded page fills the table and the total', () => {
    const state = shippingReducer(initialShippingState, ShippingApiActions.loadRatesSucceeded({ result: { items: [rate], page: 1, pageSize: 10, totalCount: 1, totalPages: 1 } }));

    expect(state.rates).toEqual([rate]);
    expect(state.total).toBe(1);
    expect(state.loading).toBe(false);
  });

  it('TOT-5 saving the settings waits, then shows a notice and the saved values', () => {
    const waiting = shippingReducer(initialShippingState, ShippingPageActions.settingsSubmitted({ value: { defaultFee: 450, freeThreshold: 8000, codFee: null } }));
    const saved = shippingReducer(waiting, ShippingApiActions.saveSettingsSucceeded({ settings: { defaultFee: 450, freeThreshold: 8000, codFee: null, defaultFeeIsUnset: false } }));

    expect(waiting.settingsSaving).toBe(true);
    expect(saved.settingsSaving).toBe(false);
    expect(saved.settings?.defaultFee).toBe(450);
    expect(saved.notice).toContain('saved');
  });

  it('TOT-5 a refused rate shows the server\'s words and stops waiting', () => {
    const waiting = shippingReducer(initialShippingState, ShippingPageActions.rateSubmitted({ cityId: 99, fee: 100 }));
    const failed = shippingReducer(waiting, ShippingApiActions.saveRateFailed({ error: apiError }));

    expect(waiting.rateSaving).toBe(true);
    expect(failed.rateSaving).toBe(false);
    expect(failed.error).toBe(apiError);
  });

  it('TOT-5 a saved or removed rate reloads the list with a notice naming the city or the default', () => {
    const saved = shippingReducer(initialShippingState, ShippingApiActions.saveRateSucceeded({ rate }));
    const removed = shippingReducer({ ...initialShippingState, removingCityId: 1 }, ShippingApiActions.removeRateSucceeded({ cityId: 1 }));

    expect(saved.loading).toBe(true);
    expect(saved.notice).toContain('Karachi');
    expect(removed.removingCityId).toBeNull();
    expect(removed.notice).toContain('default fee');
  });

  it('choosing another province clears the cities so a stale city cannot be picked', () => {
    const state = shippingReducer({ ...initialShippingState, cities: [{ id: 1, name: 'Karachi' }] }, ShippingPageActions.provinceChosen({ provinceId: 2 }));

    expect(state.cities).toEqual([]);
  });
});
