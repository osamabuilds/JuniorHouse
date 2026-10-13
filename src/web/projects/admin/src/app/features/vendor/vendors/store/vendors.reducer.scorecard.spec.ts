import { VendorsApiActions, VendorsPageActions } from './vendors.actions';
import { vendorsReducer } from './vendors.reducer';
import { initialVendorsState } from './vendors.state';

describe('vendorsReducer: the scorecard (spec AC-35, AC-39)', () => {
  const scorecard = {
    vendorId: 3,
    windowDays: 90,
    deliveriesCounted: 2,
    deliveriesOnTime: 1,
    deliveriesOnQuantity: 2,
    deliveriesExcludedRompDelay: 0,
    inspectedUnits: 40,
    defectiveUnits: 2,
    onTimePct: 50,
    onQuantityPct: 100,
    defectRatePct: 5,
    onTimePctRolling: 50,
    onQuantityPctRolling: 100,
    defectRatePctRolling: 5,
  };

  it('starts loading the scorecard of the chosen vendor, clearing any earlier one', () => {
    const state = vendorsReducer({ ...initialVendorsState, scorecard, scorecardVendorId: 9 }, VendorsPageActions.scorecardRequested({ id: 3 }));

    expect(state.scorecardVendorId).toBe(3);
    expect(state.scorecard).toBeNull();
    expect(state.scorecardLoading).toBe(true);
  });

  it('keeps the scorecard the API returned, and shows a failure in words', () => {
    const loaded = vendorsReducer(initialVendorsState, VendorsApiActions.loadScorecardSucceeded({ scorecard }));
    expect(loaded.scorecard?.onTimePct).toBe(50);
    expect(loaded.scorecardLoading).toBe(false);

    const failed = vendorsReducer(initialVendorsState, VendorsApiActions.loadScorecardFailed({ error: { status: 404, message: 'Vendor 3 was not found.', fieldErrors: {} } }));
    expect(failed.scorecardError?.message).toContain('not found');
  });

  it('closing the panel forgets the scorecard, and opening the page starts clean', () => {
    const open = vendorsReducer(initialVendorsState, VendorsApiActions.loadScorecardSucceeded({ scorecard }));

    expect(vendorsReducer({ ...open, scorecardVendorId: 3 }, VendorsPageActions.scorecardClosed()).scorecard).toBeNull();
    expect(vendorsReducer({ ...open, scorecardVendorId: 3 }, VendorsPageActions.opened()).scorecardVendorId).toBeNull();
  });
});
