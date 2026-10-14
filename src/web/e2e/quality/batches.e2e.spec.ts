import { Page, expect, test } from '@playwright/test';
import {
  API,
  PERSON,
  ReceivedBatch,
  apiInspect,
  apiReceive,
  apiSafety,
  apiSplit,
  createReceivable,
  openBatch,
  poll,
} from '../support';

/**
 * Sprint 4 (Receiving and quality control): the whole journey a delivery takes from the dock to a decision, driven through the admin
 * UI, plus the cases that must be refused or flagged. Data is set up through the API; the dispatcher moves events between the modules
 * a few seconds after each action (the delivery note reaches QC, the receipt and inspection reach VNDR).
 */

test.describe.configure({ timeout: 120_000 }); // setting a delivery up goes through the event dispatcher, which takes several seconds

const notice = (page: Page) => page.locator('.banner--success');
const alert = (page: Page) => page.locator('.banner--error[role="alert"]');

async function fillInspectionAndSubmit(page: Page, options: { failStitching?: boolean } = {}): Promise<void> {
  await page.getByRole('button', { name: 'Inspect the batch' }).click();
  await expect(page.locator('app-inspection-form')).toBeVisible();
  if (options.failStitching) {
    await page.locator('#chk-1').selectOption({ label: 'Fail' });
    await page.getByRole('button', { name: 'Add a defect' }).click();
    await page.locator('#df-type-1').selectOption({ label: 'Stitching' });
    await page.locator('#df-sev-1').selectOption({ label: 'Minor' });
    await page.locator('#df-qty-1').fill('1');
    await page.locator('#df-note-1').fill('loose thread at the hem');
  }
  await page.locator('#insp-person').fill(PERSON);
  await page.getByRole('button', { name: 'Record inspection' }).click();
  await expect(notice(page)).toContainText('Inspection recorded');
}

async function recordSafetyInUi(page: Page): Promise<void> {
  await page.locator('#sf-person').fill(PERSON);
  await page.getByRole('button', { name: 'Record safety checks' }).click();
  await expect(notice(page)).toContainText('Child-safety checks recorded');
}

test.describe('Receiving and QC: the whole journey', () => {
  test('count the goods short, inspect, record the safety checks, split the outcome, and follow it to the scorecard and the PO', async ({ page, request }) => {
    test.setTimeout(240_000);
    const receivable = await createReceivable(request);
    const { poNo, sizeId, colourId } = receivable.run.po;

    // The delivery note is in the receiving queue, waiting.
    await page.goto('/quality');
    await page.getByPlaceholder('Search by PO or delivery note number').fill(poNo);
    const row = page.locator('app-inbound-table tbody tr', { hasText: receivable.dnNo });
    await expect(row).toContainText('Waiting to be received');
    await expect(row).toContainText('100 pieces');
    await row.getByRole('link', { name: 'Receive goods' }).click();

    // Count 96 of the 100 claimed: the page says "Short by 4" as it is typed, and the receipt is recorded with that.
    await expect(page.getByRole('heading', { level: 1, name: `Receive goods: ${receivable.dnNo}` })).toBeVisible();
    await page.locator(`#grn-${sizeId}-${colourId}`).fill('96');
    await expect(page.locator('app-grn-form')).toContainText('Short by 4');
    await page.locator('#grn-person').fill(PERSON);
    await page.getByRole('button', { name: 'Record goods receipt' }).click();
    await expect(page.getByRole('heading', { level: 1, name: /^Goods received: GRN-\d{4}-\d{5}$/ })).toBeVisible();
    const grnNo = ((await page.getByRole('heading', { level: 1 }).textContent()) ?? '').replace('Goods received: ', '').trim();
    await expect(page.locator('app-reconciliation-badge')).toContainText('Short by 4');

    // On to the QC batch it opened.
    await page.getByRole('link', { name: 'Go to the QC batch' }).click();
    await page.getByRole('link', { name: grnNo }).click();
    await expect(page.getByRole('heading', { level: 1, name: `QC batch: ${grnNo}` })).toBeVisible();
    await expect(page.locator('.facts')).toContainText('96');

    // Inspect: lot 96 is code F (sample 20, accept up to 1). A failed stitching check with one minor defect still passes.
    await page.getByRole('button', { name: 'Inspect the batch' }).click();
    await expect(page.locator('app-inspection-form')).toContainText('Inspect 20 of the 96 pieces');
    await page.getByRole('button', { name: 'Cancel' }).click();
    await fillInspectionAndSubmit(page, { failStitching: true });
    await expect(page.locator('article', { hasText: 'Whole batch: round 1' })).toContainText('Passed');

    // Nothing can be accepted before both child-safety checks are recorded: the API refuses, in words.
    await page.getByRole('button', { name: 'Record the outcome' }).click();
    await expect(page.locator('app-outcome-panel')).toContainText('Both child-safety checks must be recorded before anything can be accepted');
    await page.locator('#oc-person').fill(PERSON);
    await page.getByRole('button', { name: 'Record outcome' }).click();
    await expect(alert(page)).toContainText('child-safety');
    await page.getByRole('button', { name: 'Cancel' }).last().click();

    await recordSafetyInUi(page);

    // Split it: 90 accepted and released now, 6 rejected. The balance says what is still to place.
    await page.getByRole('button', { name: 'Record the outcome' }).click();
    await page.locator('input[type="radio"][value="split"]').check();
    await page.locator(`#oc-q-2-${sizeId}-${colourId}`).fill('90');
    await expect(page.locator('app-outcome-panel')).toContainText('6 still to place');
    await expect(page.getByRole('button', { name: 'Record outcome' })).toBeDisabled();
    await page.locator('#oc-out-3').selectOption({ label: 'Reject and return to vendor' });
    await page.locator('#oc-reason-3').fill('torn sleeves');
    await page.locator(`#oc-q-3-${sizeId}-${colourId}`).fill('6');
    await expect(page.locator('app-outcome-panel')).toContainText('All placed');
    await page.locator('#oc-person').fill(PERSON);
    await page.getByRole('button', { name: 'Record outcome' }).click();
    await expect(notice(page)).toContainText('Outcome recorded');
    const parts = page.locator('app-sub-batch-table');
    await expect(parts).toContainText('Accepted');
    await expect(parts).toContainText('Rejected');
    await expect(parts).toContainText('torn sleeves');

    // The PO screen shows received against ordered; the scorecard counts the delivery and the first inspection.
    const po = await (await request.get(`${API}/api/purchase-orders/${receivable.run.po.id}`)).json();
    await poll(async () => {
      const progress = await request.get(`${API}/api/purchase-orders/${receivable.run.po.id}/receipts`);
      return progress.ok() && (await progress.json()).receivedTotal === 96 ? true : null;
    }, 'the PO to show what was received');
    await page.goto(`/purchase-orders?po=${receivable.run.po.id}`);
    await expect(page.locator('app-po-receipts-panel')).toContainText('96 of 100 pieces counted in so far');

    await poll(async () => {
      const card = await (await request.get(`${API}/api/vendors/${po.vendorId}/scorecard`)).json();
      return card.inspectedUnits >= 20 && card.deliveriesCounted >= 1 ? card : null;
    }, 'the scorecard to count the inspection');
    await page.goto('/vendors');
    await page.getByPlaceholder('Search by name').fill(receivable.run.po.vendorName);
    await page.locator('tr', { hasText: receivable.run.po.vendorName }).getByRole('button', { name: 'Scorecard' }).click();
    await expect(page.locator('app-scorecard-panel')).toContainText('scorecard');
    await expect(page.locator('app-scorecard-panel')).toContainText('20 pieces inspected');
  });
});

test.describe('Receiving and QC: refusals and flags', () => {
  test('a second receipt for a delivery note is refused, and a received delivery offers no receive link', async ({ page, request }) => {
    const receivable = await createReceivable(request);
    const batch = await apiReceive(request, receivable);

    await page.goto(`/quality/receive/${receivable.deliveryId}`);

    await expect(page.locator('.banner--info')).toContainText(`already received as ${batch.grnNo}`);
    await expect(page.locator('app-grn-form')).toHaveCount(0);
    await page.goto('/quality');
    await page.getByPlaceholder('Search by PO or delivery note number').fill(receivable.run.po.poNo);
    await page.locator('select').selectOption({ label: 'All deliveries' });
    const row = page.locator('app-inbound-table tbody tr', { hasText: receivable.dnNo });
    await expect(row).toContainText(`Received: ${batch.grnNo}`);
    await expect(row.getByRole('link', { name: 'Receive goods' })).toHaveCount(0);
  });

  test('more than the delivery note says is shown as over-delivery and still recorded', async ({ page, request }) => {
    const receivable = await createReceivable(request);
    const { sizeId, colourId } = receivable.run.po;

    await page.goto(`/quality/receive/${receivable.deliveryId}`);
    await page.locator(`#grn-${sizeId}-${colourId}`).fill('105');
    await expect(page.locator('app-grn-form')).toContainText('Over by 5');
    await page.locator('#grn-person').fill(PERSON);
    await page.getByRole('button', { name: 'Record goods receipt' }).click();

    await expect(page.locator('app-reconciliation-badge')).toContainText('Over by 5');
    await expect(page.locator('table')).toContainText('extra to inspect');
  });

  test('an outcome cannot be recorded before the batch is inspected', async ({ page, request }) => {
    const batch = await apiReceive(request, await createReceivable(request));

    await openBatch(page, batch);

    await expect(page.locator('.state-message', { hasText: 'Inspect the batch first' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Record the outcome' })).toHaveCount(0);
  });

  test('a failed choking-hazard check blocks acceptance, and only reject is allowed', async ({ page, request }) => {
    const batch = await apiReceive(request, await createReceivable(request));
    const inspectionId = await apiInspect(request, batch);
    await apiSafety(request, inspectionId, true);

    await openBatch(page, batch);
    await expect(page.locator('app-safety-checks-panel')).toContainText('Fail');
    await expect(page.locator('app-safety-checks-panel')).toContainText('can only be rejected or sent back for rework');

    await page.getByRole('button', { name: 'Record the outcome' }).click();
    await page.locator('#oc-person').fill(PERSON);
    await page.getByRole('button', { name: 'Record outcome' }).click();
    await expect(alert(page)).toContainText('can only be rejected or sent back for rework');

    await page.locator('#oc-out-1').selectOption({ label: 'Reject and return to vendor' });
    await page.locator('#oc-reason-1').fill('choking hazard on the buttons');
    await page.getByRole('button', { name: 'Record outcome' }).click();
    await expect(notice(page)).toContainText('Outcome recorded');
    await expect(page.locator('app-sub-batch-table')).toContainText('Rejected');
  });

  test('a split that does not add up is not offered, and a reason is needed for anything but Accept', async ({ page, request }) => {
    const batch = await apiReceive(request, await createReceivable(request));
    const inspectionId = await apiInspect(request, batch);
    await apiSafety(request, inspectionId);
    const { sizeId, colourId } = batch.run.po;

    await openBatch(page, batch);
    await page.getByRole('button', { name: 'Record the outcome' }).click();
    await page.locator('input[type="radio"][value="split"]').check();
    await page.locator(`#oc-q-2-${sizeId}-${colourId}`).fill('101');

    await expect(page.locator('app-outcome-panel')).toContainText('too many');
    await expect(page.getByRole('button', { name: 'Record outcome' })).toBeDisabled();

    await page.locator(`#oc-q-2-${sizeId}-${colourId}`).fill('99');
    await page.locator(`#oc-q-3-${sizeId}-${colourId}`).fill('1');
    await expect(page.getByRole('button', { name: 'Record outcome' })).toBeDisabled(); // part 2 is a rejection with no reason yet
    await page.locator('#oc-reason-3').fill('one torn');
    await expect(page.getByRole('button', { name: 'Record outcome' })).toBeEnabled();
  });

  test('a failed checklist item needs a defect of its type, and every defect needs a note', async ({ page, request }) => {
    const batch = await apiReceive(request, await createReceivable(request));
    await openBatch(page, batch);
    await page.getByRole('button', { name: 'Inspect the batch' }).click();
    await page.locator('#insp-person').fill(PERSON);
    await page.locator('#chk-1').selectOption({ label: 'Fail' });

    await page.getByRole('button', { name: 'Record inspection' }).click();
    await expect(alert(page)).toContainText('"Stitching" failed, so add at least one defect of that type');

    await page.getByRole('button', { name: 'Add a defect' }).click();
    await page.locator('#df-type-1').selectOption({ label: 'Stitching' });
    await page.getByRole('button', { name: 'Record inspection' }).click();
    await expect(alert(page)).toContainText('written note');
  });
});

test.describe('Receiving and QC: rework comes back', () => {
  test('part is sent for rework, comes back short then in full, is re-inspected with fresh safety checks and accepted', async ({ page, request }) => {
    test.setTimeout(180_000);
    const batch: ReceivedBatch = await apiReceive(request, await createReceivable(request));
    const inspectionId = await apiInspect(request, batch);
    await apiSafety(request, inspectionId);
    const parts = await apiSplit(request, batch, 80, 4); // 80 accepted, 20 sent back for rework
    const rework = parts.find((p) => p.outcomeId === 4)!;
    const { sizeId, colourId } = batch.run.po;

    await openBatch(page, batch);
    const table = page.locator('app-sub-batch-table');
    await expect(table).toContainText('Sent back for rework');
    await expect(table).toContainText('20 pieces');

    // Only 12 of the 20 come back: the other 8 stay outstanding, neither accepted nor rejected.
    await table.getByRole('button', { name: 'Record return' }).click();
    await expect(page.locator('#rt-' + sizeId + '-' + colourId)).toHaveAttribute('max', '20');
    await page.locator(`#rt-${sizeId}-${colourId}`).fill('21');
    await page.locator('#rt-person').fill(PERSON);
    await page.getByRole('button', { name: 'Record return' }).last().click();
    await expect(alert(page)).toContainText('only 20 are still with the vendor');
    await page.locator(`#rt-${sizeId}-${colourId}`).fill('12');
    await page.getByRole('button', { name: 'Record return' }).last().click();
    await expect(notice(page)).toContainText('Return recorded');
    await expect(table).toContainText('8 pieces'); // still with the vendor
    await expect(table).toContainText('came back from rework');

    // The returned part is re-inspected (12 pieces, all inspected), needs its own safety checks, then a decision.
    await table.getByRole('button', { name: 'Re-inspect' }).click();
    await expect(page.locator('app-inspection-form')).toContainText('inspect all 12 pieces');
    await page.locator('#insp-person').fill(PERSON);
    await page.getByRole('button', { name: 'Record inspection' }).click();
    await expect(notice(page)).toContainText('Inspection recorded');

    await table.getByRole('button', { name: 'Record outcome' }).click();
    await page.locator('#pd-person').fill(PERSON);
    await page.getByRole('button', { name: 'Record outcome' }).last().click();
    await expect(alert(page)).toContainText('child-safety'); // this round has no safety results yet

    await page.locator('#sf-person').fill(PERSON);
    await page.locator('app-safety-checks-panel button[type="submit"]').click();
    await expect(notice(page)).toContainText('Child-safety checks recorded');

    await table.getByRole('button', { name: 'Record outcome' }).click();
    await page.locator('#pd-person').fill(PERSON);
    await page.getByRole('button', { name: 'Record outcome' }).last().click();
    await expect(notice(page)).toContainText('Outcome recorded for the returned part');
    await expect(table).toContainText('Accepted');
    expect(rework.id).toBeGreaterThan(0);
  });
});
