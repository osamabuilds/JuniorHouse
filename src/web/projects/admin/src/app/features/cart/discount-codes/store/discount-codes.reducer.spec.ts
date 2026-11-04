import { DiscountCodeDto } from '../models';
import { DiscountCodesApiActions, DiscountCodesPageActions } from './discount-codes.actions';
import { discountCodesReducer } from './discount-codes.reducer';
import { initialDiscountCodesState } from './discount-codes.state';

const code: DiscountCodeDto = {
  id: 7, code: 'WELCOME10', kind: 'percentage', value: 10, minimumSpend: null, expiresAt: null, usageCap: null, perCustomerCap: 1,
  includesSaleItems: false, isActive: true, categoryIds: [], usedCount: 0, createdAt: '2026-11-02T10:00:00Z',
};
const apiError = { status: 409, message: 'That code already exists.' } as never;

describe('discountCodesReducer', () => {
  it('opening the page starts from a clean slate', () => {
    const state = discountCodesReducer({ ...initialDiscountCodesState, notice: 'old', codes: [code], editingId: 7 }, DiscountCodesPageActions.opened());

    expect(state.codes).toEqual([]);
    expect(state.editingId).toBeNull();
    expect(state.loading).toBe(true);
  });

  it('FR-57 a loaded page fills the table and the total', () => {
    const state = discountCodesReducer(initialDiscountCodesState, DiscountCodesApiActions.loadSucceeded({ result: { items: [code], page: 1, pageSize: 10, totalCount: 1, totalPages: 1 } }));

    expect(state.codes).toEqual([code]);
    expect(state.total).toBe(1);
    expect(state.loading).toBe(false);
  });

  it('DSC-12 a refused save keeps the form open and shows the server\'s words', () => {
    const waiting = discountCodesReducer(initialDiscountCodesState, DiscountCodesPageActions.submitted({ input: { code: 'X', kind: 'percentage', value: 10, minimumSpend: null, expiresAt: null, usageCap: null, perCustomerCap: 1, includesSaleItems: false, categoryIds: [] } }));
    const refused = discountCodesReducer(waiting, DiscountCodesApiActions.saveFailed({ error: apiError }));

    expect(waiting.saving).toBe(true);
    expect(refused.saving).toBe(false);
    expect(refused.error).toBe(apiError);
  });

  it('a saved code ends the edit, reloads the list and says what happened', () => {
    const editing = discountCodesReducer(initialDiscountCodesState, DiscountCodesPageActions.editStarted({ id: 7 }));
    const saved = discountCodesReducer(editing, DiscountCodesApiActions.saveSucceeded({ code, created: false }));

    expect(saved.editingId).toBeNull();
    expect(saved.loading).toBe(true);
    expect(saved.notice).toContain('WELCOME10');
  });

  it('DSC-9 switching a code off updates its row and tells staff shoppers will see it as not valid', () => {
    const listed = discountCodesReducer(initialDiscountCodesState, DiscountCodesApiActions.loadSucceeded({ result: { items: [code], page: 1, pageSize: 10, totalCount: 1, totalPages: 1 } }));
    const waiting = discountCodesReducer(listed, DiscountCodesPageActions.switched({ id: 7, active: false }));
    const off = discountCodesReducer(waiting, DiscountCodesApiActions.switchSucceeded({ code: { ...code, isActive: false } }));

    expect(waiting.switchingId).toBe(7);
    expect(off.switchingId).toBeNull();
    expect(off.codes[0].isActive).toBe(false);
    expect(off.notice).toContain('not valid');
  });
});
