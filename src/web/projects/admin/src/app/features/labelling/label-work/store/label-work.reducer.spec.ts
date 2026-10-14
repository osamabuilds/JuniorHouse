import { printer, workItem } from '@app/testing/labelling-fixtures';
import { LabelWorkApiActions, LabelWorkPageActions } from './label-work.actions';
import { labelWorkReducer } from './label-work.reducer';
import { initialLabelWorkState } from './label-work.state';

describe('labelWorkReducer (spec LW-1, LW-4, LW-5, LW-7, PJ-1)', () => {
  it('opening the page starts from a clean slate and loads', () => {
    const dirty = { ...initialLabelWorkState, status: 'ready' as const, page: 4, notice: 'old', queueItem: workItem() };

    const state = labelWorkReducer(dirty, LabelWorkPageActions.opened());

    expect(state.status).toBe('all');
    expect(state.page).toBe(1);
    expect(state.notice).toBeNull();
    expect(state.queueItem).toBeNull();
    expect(state.loading).toBe(true);
  });

  it('changing a filter goes back to the first page and reloads', () => {
    const onPage3 = { ...initialLabelWorkState, page: 3 };

    const byStatus = labelWorkReducer(onPage3, LabelWorkPageActions.statusChanged({ status: 'attention' }));
    expect(byStatus).toMatchObject({ status: 'attention', page: 1, loading: true });

    const byFlag = labelWorkReducer(onPage3, LabelWorkPageActions.priceFlagToggled());
    expect(byFlag).toMatchObject({ priceFlagOnly: true, page: 1, loading: true });
    expect(labelWorkReducer(byFlag, LabelWorkPageActions.priceFlagToggled()).priceFlagOnly).toBe(false);

    expect(labelWorkReducer(onPage3, LabelWorkPageActions.pageSizeChanged({ pageSize: 50 }))).toMatchObject({ pageSize: 50, page: 1 });
  });

  it('keeps the page of items the API returned, and a failure in words', () => {
    const loaded = labelWorkReducer(
      { ...initialLabelWorkState, loading: true },
      LabelWorkApiActions.loadItemsSucceeded({ result: { items: [workItem()], page: 2, pageSize: 10, totalCount: 11, totalPages: 2 } }),
    );
    expect(loaded.items).toHaveLength(1);
    expect(loaded.total).toBe(11);
    expect(loaded.page).toBe(2);
    expect(loaded.loading).toBe(false);

    const failed = labelWorkReducer(loaded, LabelWorkApiActions.loadItemsFailed({ error: { status: 500, message: 'Something went wrong.', fieldErrors: {} } }));
    expect(failed.error?.message).toBe('Something went wrong.');
    expect(failed.loading).toBe(false);
  });

  it('queueing: the form opens for one item, shows saving, and closes with a notice on success (PJ-1)', () => {
    const item = workItem({ id: 9 });
    const opened = labelWorkReducer(initialLabelWorkState, LabelWorkPageActions.queueFormOpened({ item }));
    expect(opened.queueItem?.id).toBe(9);

    const saving = labelWorkReducer(opened, LabelWorkPageActions.queueSubmitted({ itemId: 9, value: { qty: 5, printerCode: null, actingPerson: 'Asha' } }));
    expect(saving.queueSaving).toBe(true);

    const done = labelWorkReducer(saving, LabelWorkApiActions.queueSucceeded({ job: { id: 1, jobNo: 'PJ-2026-00001', qty: 5, statusCode: 'Queued', printerCode: 'FAKE-PDF' } }));
    expect(done.queueItem).toBeNull();
    expect(done.queueSaving).toBe(false);
    expect(done.notice).toContain('Queued 5 tags as job PJ-2026-00001 on FAKE-PDF');
    expect(done.loading).toBe(true);
  });

  it('a refused queue request keeps the form open with the reason, so nothing the user typed is lost', () => {
    const opened = labelWorkReducer(initialLabelWorkState, LabelWorkPageActions.queueFormOpened({ item: workItem() }));
    const saving = labelWorkReducer(opened, LabelWorkPageActions.queueSubmitted({ itemId: 1, value: { qty: 99, printerCode: null, actingPerson: 'Asha' } }));

    const failed = labelWorkReducer(saving, LabelWorkApiActions.queueFailed({ error: { status: 400, message: 'Only 4 more tags are allowed for this item.', fieldErrors: { Qty: ['Only 4 more tags are allowed for this item.'] } } }));

    expect(failed.queueItem).not.toBeNull();
    expect(failed.queueSaving).toBe(false);
    expect(failed.queueError?.fieldErrors['Qty']).toHaveLength(1);
  });

  it('resolving: a parked item that becomes ready says so (LW-4)', () => {
    const resolving = labelWorkReducer(initialLabelWorkState, LabelWorkPageActions.resolveSubmitted({ itemId: 4, value: { confirmRetiredStyle: false, actingPerson: 'Asha' } }));
    expect(resolving.resolvingId).toBe(4);

    const ready = labelWorkReducer(resolving, LabelWorkApiActions.resolveSucceeded({ item: workItem({ id: 4, skuCode: 'ROMP-X' }) }));
    expect(ready.resolvingId).toBeNull();
    expect(ready.notice).toBe('Ready to print: ROMP-X.');

    const stillParked = labelWorkReducer(resolving, LabelWorkApiActions.resolveSucceeded({ item: workItem({ id: 4, statusCode: 'NeedsAttention', skuCode: null }) }));
    expect(stillParked.notice).toBe('Still needs attention.');
  });

  it('remembers the printers and lets the notice be dismissed', () => {
    const withPrinters = labelWorkReducer(initialLabelWorkState, LabelWorkApiActions.loadPrintersSucceeded({ printers: [printer()] }));
    expect(withPrinters.printers).toHaveLength(1);

    const dismissed = labelWorkReducer({ ...withPrinters, notice: 'hello' }, LabelWorkPageActions.noticeDismissed());
    expect(dismissed.notice).toBeNull();
  });
});
