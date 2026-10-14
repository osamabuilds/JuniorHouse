import { adjustment, adjustmentValue, pageOf, REASONS } from '@app/testing/warehouse-fixtures';
import { ApiError } from '@core/http';
import { AdjustmentsApiActions, AdjustmentsPageActions } from './adjustments.actions';
import { adjustmentsReducer } from './adjustments.reducer';
import { initialAdjustmentsState } from './adjustments.state';

const largeChange: ApiError = {
  status: 409,
  message: 'Confirmation required: changing 30 units is above 20. Confirm it to continue.',
  fieldErrors: {},
};
const belowZero: ApiError = { status: 409, message: 'Not enough stock in the bin for this change.', fieldErrors: {} };

describe('adjustmentsReducer (spec ADJ-0, ADJ-1, ADJ-4, ADJ-4b, ADJ-8, ADJ-10)', () => {
  it('opening the page starts from a clean slate', () => {
    const state = adjustmentsReducer({ ...initialAdjustmentsState, reasonId: 3, notice: 'old', formOpen: true }, AdjustmentsPageActions.opened());

    expect(state).toMatchObject({ reasonId: null, notice: null, formOpen: false, loading: true });
  });

  it('filtering by reason goes back to the first page and reloads (ADJ-10)', () => {
    const state = adjustmentsReducer({ ...initialAdjustmentsState, page: 3 }, AdjustmentsPageActions.reasonFilterChanged({ reasonId: 3 }));

    expect(state).toMatchObject({ reasonId: 3, page: 1, loading: true });
  });

  it('keeps the history page and the reasons (ADJ-2)', () => {
    const loaded = adjustmentsReducer(initialAdjustmentsState, AdjustmentsApiActions.loadAdjustmentsSucceeded({ result: pageOf([adjustment()], { totalCount: 14 }) }));
    const withReasons = adjustmentsReducer(loaded, AdjustmentsApiActions.loadReasonsSucceeded({ reasons: REASONS }));

    expect(withReasons).toMatchObject({ total: 14, loading: false });
    expect(withReasons.reasons.map((r) => r.code)).toEqual(['DAMAGE', 'MISCOUNT', 'THEFT', 'FOUND_STOCK']);
  });

  it('a recorded adjustment closes the form, says what happened and reloads the history (ADJ-0)', () => {
    const state = adjustmentsReducer({ ...initialAdjustmentsState, formOpen: true, saving: true }, AdjustmentsApiActions.postSucceeded({ adjustment: adjustment() }));

    expect(state).toMatchObject({ formOpen: false, saving: false, loading: true });
    expect(state.notice).toContain('-3 unit(s) of ROMP-STY-DRESS-RED-2-3Y in A-03-2 (DAMAGE)');
    expect(state.notice).toContain('now holds 7');
  });

  it('a change above the limit is a question, not an error: the screen keeps the attempt and asks to confirm (ADJ-4b)', () => {
    const value = adjustmentValue();

    const state = adjustmentsReducer({ ...initialAdjustmentsState, formOpen: true, saving: true }, AdjustmentsApiActions.postFailed({ error: largeChange, value }));

    expect(state.reconfirm).toEqual({ value, message: largeChange.message });
    expect(state.formError).toBeNull();
    expect(state.saving).toBe(false);
    expect(state.formOpen).toBe(true);
  });

  it('declining the confirmation goes back to the form (ADJ-4b)', () => {
    const asked = adjustmentsReducer(initialAdjustmentsState, AdjustmentsApiActions.postFailed({ error: largeChange, value: adjustmentValue() }));

    const declined = adjustmentsReducer(asked, AdjustmentsPageActions.reconfirmDeclined());

    expect(declined.reconfirm).toBeNull();
  });

  it('removing more than the bin holds is shown as an error on the form, not as a question (ADJ-4)', () => {
    const state = adjustmentsReducer({ ...initialAdjustmentsState, formOpen: true, saving: true }, AdjustmentsApiActions.postFailed({ error: belowZero, value: adjustmentValue() }));

    expect(state).toMatchObject({ formError: belowZero, reconfirm: null, saving: false, formOpen: true });
  });
});
