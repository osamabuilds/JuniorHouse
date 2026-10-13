import { PoDto, PoRevisionDto } from '../models';
import { PurchaseOrdersApiActions, PurchaseOrdersPageActions } from './purchase-orders.actions';
import { purchaseOrdersReducer } from './purchase-orders.reducer';
import { selectCurrentRevisionNumber, selectHasTechPack, selectPendingRevision } from './purchase-orders.selectors';
import { PURCHASE_ORDERS_FEATURE_KEY, initialPurchaseOrdersState } from './purchase-orders.state';

const po = (overrides: Partial<PoDto> = {}): PoDto => ({
  id: 10,
  poNo: 'PO-2026-00001',
  vendorId: 1,
  styleId: 5,
  unitCost: 500,
  expectedDeliveryDate: '2026-12-01',
  paymentTermId: 1,
  advancePercent: 40,
  latestAcceptableDate: null,
  overTolerancePercent: null,
  underTolerancePercent: null,
  fabricResponsibilityId: null,
  statusId: 1,
  lines: [],
  statusHistory: [],
  ...overrides,
});

const revision = (statusId: number, revisionNumber: number) => ({ statusId, revisionNumber }) as PoRevisionDto;
const root = (state: ReturnType<typeof purchaseOrdersReducer>) => ({ [PURCHASE_ORDERS_FEATURE_KEY]: state });

describe('purchaseOrdersReducer', () => {
  it('opens a fresh page even if a previous visit left the detail view open', () => {
    const detail = purchaseOrdersReducer(initialPurchaseOrdersState, PurchaseOrdersApiActions.loadDetailSucceeded({ po: po() }));
    const state = purchaseOrdersReducer(detail, PurchaseOrdersPageActions.opened());

    expect(state.mode).toBe('list');
    expect(state.current).toBeNull();
    expect(state.loading).toBe(true);
  });

  it('shows the detail view when a PO loads, and clears revisions for a Draft', () => {
    const withRevisions = { ...initialPurchaseOrdersState, revisions: [revision(2, 0)] };
    const state = purchaseOrdersReducer(withRevisions, PurchaseOrdersApiActions.loadDetailSucceeded({ po: po({ statusId: 1 }) }));

    expect(state.mode).toBe('detail');
    expect(state.revisions).toEqual([]);
  });

  it('goes back to the detail view from the form when a PO is open, otherwise to the list', () => {
    const editing = purchaseOrdersReducer(
      purchaseOrdersReducer(initialPurchaseOrdersState, PurchaseOrdersApiActions.loadDetailSucceeded({ po: po() })),
      PurchaseOrdersPageActions.draftEditStarted(),
    );
    expect(purchaseOrdersReducer(editing, PurchaseOrdersPageActions.formCancelled()).mode).toBe('detail');

    const creating = purchaseOrdersReducer(initialPurchaseOrdersState, PurchaseOrdersPageActions.createStarted());
    expect(purchaseOrdersReducer(creating, PurchaseOrdersPageActions.formCancelled()).mode).toBe('list');
  });

  it('pre-fills the cancel reason after a declined vendor response (AC-29)', () => {
    const state = purchaseOrdersReducer(
      initialPurchaseOrdersState,
      PurchaseOrdersApiActions.vendorResponseSucceeded({ suggestedCancelReasonId: 3 }),
    );

    expect(state.cancelReasonId).toBe(3);
    expect(state.panel).toBe('none');
  });

  it('derives the pending revision, the in-force revision number and the tech-pack flag', () => {
    const state = {
      ...initialPurchaseOrdersState,
      revisions: [revision(2, 1), revision(1, 2)],
      files: [{ categoryId: 1 }] as never,
    };

    expect(selectPendingRevision(root(state))?.revisionNumber).toBe(2);
    expect(selectCurrentRevisionNumber(root(state))).toBe(1);
    expect(selectHasTechPack(root(state))).toBe(true);
  });
});
