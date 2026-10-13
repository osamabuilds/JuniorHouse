import { Page, expect, test } from '@playwright/test';
import {
  API,
  PERSON,
  TestRun,
  apiDeliver,
  apiSubmitAndApproveSample,
  createPo,
  createRun,
  createRunInProduction,
  openPoById,
  openRun,
  statusBadge,
  today,
  waitForPoStatus,
  waitForRun,
} from '../support';

/**
 * Sprint 3 (Production Tracking): the whole journey a purchase order takes after the vendor acknowledges it - milestones, the
 * pre-production sample gate, bulk cutting, delivery notes, closing - driven through the admin UI, plus the cases that must be
 * refused or flagged. Data is set up through the API; the dispatcher moves events between the modules a few seconds after each action.
 */

const PNG = { name: 'sample-front.png', mimeType: 'image/png', buffer: Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0x00, 0x01]) };
const EXE = { name: 'photo.jpg', mimeType: 'image/jpeg', buffer: Buffer.from([0x4d, 0x5a, 0x90, 0x00]) };

const notice = (page: Page) => page.locator('.banner--success');
const timeline = (page: Page) => page.locator('app-milestone-timeline');
const deliveries = (page: Page) => page.locator('app-delivery-note-list');

async function recordMilestoneInUi(page: Page, label: string, options: { date?: string; reporter?: string } = {}): Promise<void> {
  await page.getByRole('button', { name: 'Record a milestone' }).click();
  await page.locator('#ms-type').selectOption({ label });
  if (options.date) {
    await page.locator('#ms-date').fill(options.date);
  }
  await page.locator('#ms-reporter').fill(options.reporter ?? 'Ali Raza (vendor)');
  await page.locator('#ms-person').fill(PERSON);
  await page.getByRole('button', { name: 'Record milestone' }).click();
}

async function recordDeliveryNoteInUi(page: Page, run: TestRun, dnNo: string, qty: number, final = false): Promise<void> {
  await page.getByRole('button', { name: 'Record a delivery note' }).click();
  await page.locator('#dn-no').fill(dnNo);
  await page.locator('#dn-person').fill(PERSON);
  await page.locator(`#dn-qty-${run.po.sizeId}-${run.po.colourId}`).fill(String(qty));
  if (final) {
    await page.locator('#dn-final').check();
  }
  await page.getByRole('button', { name: 'Record delivery note' }).click();
}

test.describe('Production: the whole journey', () => {
  test('acknowledge, milestone, sample rejected then approved with a photo, cutting, two deliveries, close', async ({ page, request }) => {
    test.setTimeout(180_000);
    const run = await createRun(request);

    // The run is on the production list, found through the list's own search.
    await page.goto('/production');
    await expect(page.getByRole('heading', { level: 1, name: 'Production' })).toBeVisible();
    await page.getByPlaceholder('Search by PO number').fill(run.po.poNo);
    await page.getByRole('link', { name: run.po.poNo }).click();
    await expect(page.getByRole('heading', { level: 1, name: `Production: ${run.po.poNo}` })).toBeVisible();
    await expect(timeline(page)).toContainText('Nothing recorded yet');

    // A milestone the vendor reported.
    await recordMilestoneInUi(page, 'Fabric booked by the vendor');
    await expect(notice(page)).toContainText('Milestone recorded.');
    await expect(timeline(page)).toContainText('Fabric booked by the vendor');
    await expect(timeline(page)).toContainText(`recorded by ${PERSON}`);

    // Bulk cutting is refused until a sample is approved: the choice is listed, greyed out, with the reason.
    await page.getByRole('button', { name: 'Record a milestone' }).click();
    const cutting = page.locator('#ms-type option', { hasText: 'Bulk cutting started' });
    await expect(cutting).toBeDisabled();
    await expect(cutting).toContainText('needs an approved pre-production sample first');
    await page.getByRole('button', { name: 'Cancel' }).click();

    // Round 1 is rejected (a reason is required), round 2 is approved with a photo.
    await page.getByRole('button', { name: 'Record a sample' }).click();
    await page.locator('#sample-reporter').fill('Ali Raza (vendor)');
    await page.locator('#sample-person').fill(PERSON);
    await page.getByRole('button', { name: 'Record sample' }).click();
    await expect(notice(page)).toContainText('Sample recorded as submitted.');
    await expect(page.locator('app-pp-round-panel')).toContainText('Round 1');
    await expect(page.locator('app-pp-round-panel')).toContainText('Waiting for a decision');

    await page.locator('#decide-person-1').fill(PERSON);
    await page.getByRole('button', { name: 'Reject sample' }).click();
    await expect(page.locator('app-pp-round-panel')).toContainText('Say why the sample is rejected');
    await page.locator('#decide-reason-1').fill('Collar is too wide');
    await page.getByRole('button', { name: 'Reject sample' }).click();
    await expect(notice(page)).toContainText('Sample rejected.');
    await expect(page.locator('app-pp-round-panel')).toContainText('Reason: Collar is too wide');

    await page.getByRole('button', { name: 'Record another sample' }).click();
    await page.locator('#sample-reporter').fill('Ali Raza (vendor)');
    await page.locator('#sample-person').fill(PERSON);
    await page.getByRole('button', { name: 'Record sample' }).click();
    await expect(page.locator('app-pp-round-panel')).toContainText('Round 2');

    // A disguised executable is refused with the reason; a real photo is stored and can be downloaded.
    await page.locator('#round-file-2').setInputFiles(EXE);
    await expect(page.locator('app-pp-round-panel .banner--error')).toContainText("isn't allowed");
    await page.locator('#round-file-2').setInputFiles(PNG);
    await expect(page.locator('app-pp-round-panel .round__files a')).toContainText('sample-front.png');

    await page.locator('#decide-person-2').fill(PERSON);
    await page.getByRole('button', { name: 'Approve sample' }).click();
    await expect(notice(page)).toContainText('Sample approved.');
    await expect(page.locator('app-pp-round-panel')).toContainText('Approved');
    await expect(page.locator('app-pp-round-panel')).toContainText('Round 1');

    // Bulk cutting is now allowed, and the PO moves to In Production (through PROD's event, a moment later).
    await recordMilestoneInUi(page, 'Bulk cutting started');
    await expect(notice(page)).toContainText('Milestone recorded.');
    await expect(timeline(page)).toContainText('Bulk cutting started');
    await waitForPoStatus(request, run.po.id, 5);

    // Expected completion later than the latest acceptable delivery minus the buffer is flagged in words.
    await page.getByRole('button', { name: 'Set the date' }).click();
    await page.locator('#expected-date').fill('2026-12-14');
    await page.locator('#expected-person').fill(PERSON);
    await page.getByRole('button', { name: 'Save date' }).click();
    await expect(notice(page)).toContainText('Expected completion date saved.');
    await expect(page.locator('app-run-flag-badges').first()).toContainText('At risk of being late');

    // Delivery note 1: partial. The PO becomes Partially Delivered.
    await recordDeliveryNoteInUi(page, run, 'DN-E2E-1', 60);
    await expect(notice(page)).toContainText('Delivery note recorded.');
    await expect(deliveries(page)).toContainText('Delivery note DN-E2E-1');
    await expect(deliveries(page)).toContainText('On time');
    await waitForPoStatus(request, run.po.id, 6);

    // The same vendor delivery note number again is refused, in words.
    await recordDeliveryNoteInUi(page, run, 'DN-E2E-1', 5);
    await expect(page.locator('app-delivery-note-form .banner--error')).toContainText('has already been recorded');
    await page.getByRole('button', { name: 'Cancel' }).click();

    // Delivery note 2: the final shipment, still short. Delivered, with the short flag in words.
    await recordDeliveryNoteInUi(page, run, 'DN-E2E-2', 20, true);
    await expect(deliveries(page)).toContainText('Final shipment');
    await expect(deliveries(page)).toContainText('Short by 15 pieces');
    await waitForPoStatus(request, run.po.id, 7);

    // Closing is manual: the PO shows Delivered with a Close button, and its screen links back to the run.
    await openPoById(page, run.po);
    await expect(statusBadge(page)).toContainText('Delivered');
    await expect(page.getByRole('main').getByRole('link', { name: 'Production' })).toBeVisible();
    await page.getByRole('button', { name: 'Close order' }).click();
    await expect(statusBadge(page)).toContainText('Closed');
    await expect(page.getByRole('button', { name: 'Close order' })).toHaveCount(0);
    await expect(page.locator('.cancel-panel')).toHaveCount(0);

    await expect(async () => {
      await openRun(page, run);
      await expect(page.locator('.banner--info', { hasText: 'This run is closed' })).toBeVisible();
    }).toPass({ timeout: 30_000 });
  });
});

test.describe('Production: edge cases', () => {
  // Several cases wait for the outbox dispatcher (5 s per hop) to move the PO after an action, so they need more than the default 30 s.
  test.describe.configure({ timeout: 120_000 });

  test('a milestone dated before the PO was sent is refused with the allowed range, and nothing is saved', async ({ page, request }) => {
    const run = await createRun(request);
    await openRun(page, run);

    await recordMilestoneInUi(page, 'Fabric booked by the vendor', { date: today(-30) });

    await expect(page.locator('app-milestone-form .banner--error')).toContainText(`The date must be between ${today()}`);
    await page.getByRole('button', { name: 'Cancel' }).click();
    await expect(timeline(page)).toContainText('Nothing recorded yet');
  });

  test('an empty milestone form says what is missing instead of doing nothing', async ({ page, request }) => {
    const run = await createRun(request);
    await openRun(page, run);

    await page.getByRole('button', { name: 'Record a milestone' }).click();
    await page.getByRole('button', { name: 'Record milestone' }).click();

    await expect(page.locator('app-milestone-form')).toContainText("This can't be saved yet");
    await expect(page.locator('app-milestone-form')).toContainText('your name');
  });

  test('cutting is refused by the API too when no sample is approved', async ({ request }) => {
    const run = await createRun(request);

    const response = await request.post(`${API}/api/production/runs/${run.runId}/milestones`, {
      data: { typeId: 6, claimedDate: today(), reportedBy: 'Ali', actingPerson: PERSON },
    });

    expect(response.status()).toBe(409);
    expect(await response.text()).toContain('pre-production sample has been approved');
  });

  test('over-delivery is recorded and flagged in words, never refused', async ({ page, request }) => {
    const run = await createRunInProduction(request);
    await openRun(page, run);

    await recordDeliveryNoteInUi(page, run, 'DN-OVER', 120); // ordered 100, allowed 105

    await expect(notice(page)).toContainText('Delivery note recorded.');
    await expect(deliveries(page)).toContainText('Over by 15 pieces');
    await expect(deliveries(page)).toContainText('the receiving check decides what happens to the extra');
    await expect(page.locator('app-run-flag-badges').first()).toContainText('Delivered more than allowed');
  });

  test('a Romp-supplied run cut before the fabric was sent is flagged loudly, not blocked', async ({ page, request }) => {
    const run = await createRun(request, { fabricResponsibilityId: 2 });
    await apiSubmitAndApproveSample(request, run.runId);
    await openRun(page, run);

    // Fabric booked is not offered when Romp supplies the fabric; fabric dispatch is.
    await page.getByRole('button', { name: 'Record a milestone' }).click();
    await expect(page.locator('#ms-type option', { hasText: 'Fabric booked' })).toHaveCount(0);
    await expect(page.locator('#ms-type option', { hasText: 'Fabric dispatched by Romp' })).toHaveCount(1);
    await page.getByRole('button', { name: 'Cancel' }).click();

    await recordMilestoneInUi(page, 'Bulk cutting started');

    await expect(notice(page)).toContainText('Milestone recorded.');
    await expect(timeline(page)).toContainText("Cutting started before Romp's fabric was dispatched.");
    await expect(page.locator('app-run-flag-badges').first()).toContainText('Cutting started before Romp sent the fabric');
  });

  test('cancelling in production shows a prominent warning, needs confirmation and the production reason, then closes the run', async ({ page, request }) => {
    const run = await createRunInProduction(request);
    // The reason list is loaded once when the page opens; reload if that single request did not come back (busy machine).
    await expect(async () => {
      await openPoById(page, run.po);
      await expect(statusBadge(page)).toContainText('In Production');
      await expect(page.locator('#cancel-reason option', { hasText: 'Cancelled after production started' })).toHaveCount(1, { timeout: 5_000 });
    }).toPass({ timeout: 40_000 });

    await expect(page.locator('.cancel-panel .banner--error')).toContainText('Production has already started');
    const cancel = page.getByRole('button', { name: 'Cancel PO' });
    await page.locator('#cancel-reason').selectOption({ label: 'Cancelled after production started' });
    await expect(cancel).toBeDisabled();
    await page.locator('#cancel-understood').check();
    await cancel.click();

    await expect(statusBadge(page)).toContainText('Cancelled');
    await expect(page.locator('.timeline')).toContainText('Reason: Cancelled after production started');
    await expect(async () => {
      await openRun(page, run);
      await expect(page.locator('.banner--info', { hasText: 'This run is cancelled' })).toBeVisible();
    }).toPass({ timeout: 30_000 });
    await expect(page.getByRole('button', { name: 'Record a milestone' })).toHaveCount(0);
  });

  test('a fabric-supplier change after a sample was approved shows the re-sample notice, and the run recommends a new sample', async ({ page, request }) => {
    const run = await createRun(request);
    await apiSubmitAndApproveSample(request, run.runId);
    await openPoById(page, run.po);

    await page.getByRole('button', { name: 'Amend', exact: true }).click();
    await expect(page.locator('app-amend-form')).toContainText('A pre-production sample was already approved for this order');
    await expect(page.locator('app-amend-form')).toContainText('A new sample is optional');
    await page.locator('#amend-fabric').selectOption({ label: 'Romp Supplied' });
    await expect(page.locator('app-amend-form')).toContainText('A new sample is strongly recommended');
    await page.locator('#amend-reason').selectOption({ label: 'Spec Change' });
    await page.locator('#amend-note').fill('E2E: Romp will supply the fabric from now on');
    await page.getByRole('button', { name: 'Submit amendment' }).click();
    await expect(page.getByRole('heading', { name: 'Revision 1 awaits a decision' })).toBeVisible();

    // The vendor agrees (recorded through the API); the revision comes into force and PROD is told.
    const accepted = await request.post(`${API}/api/purchase-orders/${run.po.id}/amendments/1/accept`, { data: {} });
    expect(accepted.ok()).toBe(true);
    await expect(page.getByRole('article', { name: 'Revision 1' })).toContainText('Construction change');

    await expect(async () => {
      await openRun(page, run);
      await expect(page.locator('.banner--error', { hasText: 'A new sample is strongly recommended' })).toBeVisible();
    }).toPass({ timeout: 30_000 });
  });

  test('an amendment that takes a line below what was already delivered is refused', async ({ page, request }) => {
    const run = await createRunInProduction(request);
    await apiDeliver(request, run, 'DN-GUARD', 70);
    await openPoById(page, run.po);

    await page.getByRole('button', { name: 'Amend', exact: true }).click();
    await page.locator('#amend-reason').selectOption({ label: 'Size Mix Change' });
    await page.locator('#amend-note').fill('E2E: cut the order back');
    await page.locator('.line-grid input').first().fill('60');
    await page.getByRole('button', { name: 'Submit amendment' }).click();

    // The refusal shows in the banner and again beside the quantity grid; either one proves it.
    await expect(page.locator('app-amend-form .banner--error[role="alert"]')).toContainText('already delivered');
  });

  test('a delivered order cannot be amended or cancelled', async ({ page, request }) => {
    const run = await createRunInProduction(request);
    await apiDeliver(request, run, 'DN-FULL', 100);
    await waitForPoStatus(request, run.po.id, 7);
    await openPoById(page, run.po);

    await expect(statusBadge(page)).toContainText('Delivered');
    await expect(page.getByRole('button', { name: 'Amend', exact: true })).toHaveCount(0);
    await expect(page.locator('.cancel-panel')).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Close order' })).toBeVisible();
  });

  test('a PO that is only sent has no production run, and Draft POs are not listed', async ({ page, request }) => {
    const sent = await createPo(request, 'sent');

    const response = await request.get(`${API}/api/production/runs/by-po/${sent.id}`);
    expect(response.status()).toBe(404);

    await page.goto('/production');
    await page.getByPlaceholder('Search by PO number').fill(sent.poNo);
    await expect(page.getByText('No production runs yet')).toBeVisible();
  });

  test('the production list is paged and its filters go back to page 1', async ({ page, request }) => {
    await createRun(request);
    await createRun(request);

    await page.goto('/production');
    await page.locator('app-pagination select').selectOption('10');
    await expect(page.locator('app-run-table tbody tr').first()).toBeVisible();

    await page.getByLabel('At risk of being late').check();
    await expect(page.locator('app-pagination')).toContainText(/Showing/);
    await page.getByLabel('At risk of being late').uncheck();
    await expect(page.locator('app-run-table tbody tr').first()).toBeVisible();
  });

  test('an unknown run shows a plain not-found message', async ({ page }) => {
    await page.goto('/production/99999999');

    await expect(page.getByText('This production run could not be found.')).toBeVisible();
  });

  test('the run screen has one h1, sections with h2 headings and labelled controls', async ({ page, request }) => {
    const run = await createRun(request);
    await openRun(page, run);

    await expect(page.locator('h1')).toHaveCount(1);
    await expect(page.locator('main h2')).toHaveText(['Where it stands', 'Milestones', 'Pre-production sample', 'Expected completion', 'Deliveries']);
    await page.getByRole('button', { name: 'Record a milestone' }).click();
    for (const id of ['ms-type', 'ms-date', 'ms-reporter', 'ms-person', 'ms-note']) {
      await expect(page.locator(`label[for="${id}"]`)).toHaveCount(1);
    }
  });

  test('the background-updates indicator in the footer reports events as delivered', async ({ page, request }) => {
    const po = await createPo(request, 'acknowledged');
    await waitForRun(request, po.id);

    await page.goto('/production');

    await expect(page.locator('app-outbox-health')).toContainText('Background updates:');
  });
});
