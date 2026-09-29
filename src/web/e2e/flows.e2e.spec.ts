import { expect, test } from '@playwright/test';
import { API, PDF, createPo, fillAmendment, openPo, statusBadge, uniqueSuffix } from './support';

/**
 * Best-case journeys for Sprints 1 and 2, each starting from data set up through the API and then
 * driven through the admin UI the way staff would use it. The worst cases live in
 * edge-cases.e2e.spec.ts and the layout checks in responsive.e2e.spec.ts.
 */

const history = (page: import('@playwright/test').Page) => page.getByRole('region', { name: 'Revision history' });
const facts = (page: import('@playwright/test').Page) => page.locator('.po-detail__facts');

test.describe('Amendments', () => {
  test('an amendment to a SENT PO takes effect immediately, with no decision needed', async ({ page, request }) => {
    const po = await createPo(request, 'sent');
    await openPo(page, po);

    await fillAmendment(page, { unitCost: '450', note: 'E2E: price agreed by phone' });
    await page.getByRole('button', { name: 'Submit amendment' }).click();

    await expect(page.getByRole('heading', { name: /awaits a decision/ })).toHaveCount(0);
    await expect(facts(page)).toContainText('PKR 450');
    await expect(history(page).getByRole('article', { name: 'Revision 1' })).toContainText('In force');
    await expect(history(page).getByRole('article', { name: 'Revision 0' })).toContainText('Superseded');
    await expect(history(page).getByRole('article', { name: 'Revision 1' })).toContainText('E2E: price agreed by phone');
  });

  test('an amendment to an ACKNOWLEDGED PO waits, and rejecting it leaves the terms alone', async ({ page, request }) => {
    const po = await createPo(request, 'acknowledged');
    await openPo(page, po);

    await fillAmendment(page, { unitCost: '500' });
    await page.getByRole('button', { name: 'Submit amendment' }).click();
    await expect(page.getByRole('heading', { name: 'Revision 1 awaits a decision' })).toBeVisible();
    await expect(page.locator('app-pending-revision-actions')).toContainText('Proposed by Romp buyer');
    await expect(page.locator('app-pending-revision-actions')).toContainText("decision is Vendor's");
    await expect(facts(page)).toContainText('PKR 420');

    await page.locator('#pending-note').fill('Too high');
    await page.getByRole('button', { name: 'Reject' }).click();

    await expect(page.getByRole('heading', { name: /awaits a decision/ })).toHaveCount(0);
    await expect(facts(page)).toContainText('PKR 420');
    await expect(history(page).getByRole('article', { name: 'Revision 1' })).toContainText('Rejected');
    await expect(history(page).getByRole('article', { name: 'Revision 0' })).toContainText('In force');
  });

  test('the proposer can withdraw a pending amendment', async ({ page, request }) => {
    const po = await createPo(request, 'acknowledged');
    await openPo(page, po);
    await fillAmendment(page, { unitCost: '480' });
    await page.getByRole('button', { name: 'Submit amendment' }).click();

    await page.getByRole('button', { name: /^Withdraw/ }).click();

    await expect(page.getByRole('heading', { name: /awaits a decision/ })).toHaveCount(0);
    await expect(history(page).getByRole('article', { name: 'Revision 1' })).toContainText('Withdrawn');
    await expect(facts(page)).toContainText('PKR 420');
  });

  test('a second amendment can be raised once the first is decided, and revisions count up', async ({ page, request }) => {
    const po = await createPo(request, 'sent');
    await openPo(page, po);

    await fillAmendment(page, { unitCost: '430' });
    await page.getByRole('button', { name: 'Submit amendment' }).click();
    await expect(facts(page)).toContainText('PKR 430');

    await fillAmendment(page, { unitCost: '440', note: 'E2E: second change' });
    await page.getByRole('button', { name: 'Submit amendment' }).click();

    await expect(facts(page)).toContainText('PKR 440');
    await expect(history(page).getByRole('article')).toHaveCount(3);
    await expect(history(page).getByRole('article', { name: 'Revision 2' })).toContainText('In force');
  });

  test('quantities can be changed in an amendment, and the history shows the before and after', async ({ page, request }) => {
    const po = await createPo(request, 'sent');
    await openPo(page, po);
    await page.getByRole('button', { name: 'Amend', exact: true }).click();
    await page.locator('#amend-reason').selectOption({ label: 'Size Mix Change' });
    await page.locator('#amend-note').fill('E2E: add a size');

    // The style has two sizes; the PO only uses the first. Add 40 of the second.
    const qtyInputs = page.locator('app-amend-form .line-grid input[type="number"]');
    await qtyInputs.nth(1).fill('40');
    await page.getByRole('button', { name: 'Submit amendment' }).click();

    await expect(history(page).getByRole('article', { name: 'Revision 1' })).toContainText('140 pcs');
    await expect(page.locator('.po-detail').getByRole('table').first()).toContainText('40');
  });
});

test.describe('Vendor response', () => {
  test('a vendor counter-proposal becomes a pending revision the buyer can accept', async ({ page, request }) => {
    const po = await createPo(request, 'sent');
    await openPo(page, po);

    await page.getByRole('button', { name: 'Record vendor response' }).click();
    await page.locator('#vresp-outcome').selectOption({ label: 'Countered with different terms' });
    await page.locator('#vresp-channel').selectOption({ label: 'Phone Call' });
    await page.locator('#vresp-responder').fill('E2E Contact');
    await expect(page.getByRole('heading', { name: 'Counter-proposal from the vendor' })).toBeVisible();

    await page.locator('#amend-reason').selectOption({ label: 'Capacity Delay' });
    await page.locator('#amend-note').fill('E2E: vendor needs 5 more days');
    await page.locator('#amend-delivery').fill('2026-12-06');
    await page.getByRole('button', { name: 'Record counter-proposal' }).click();

    await expect(page.getByRole('heading', { name: 'Revision 1 awaits a decision' })).toBeVisible();
    await expect(page.locator('app-pending-revision-actions')).toContainText('Proposed by Vendor');
    await expect(page.locator('app-pending-revision-actions')).toContainText("decision is Romp buyer's");
    await expect(history(page).getByRole('article', { name: 'Revision 1' })).toContainText('E2E Contact');

    await page.getByRole('button', { name: /^Accept/ }).click();
    await expect(facts(page)).toContainText('6 Dec 2026');
    await expect(statusBadge(page)).toHaveText('Sent to Vendor');
  });

  test('a declined PO is not cancelled for you, but Cancel opens with the reason filled in', async ({ page, request }) => {
    const po = await createPo(request, 'sent');
    await openPo(page, po);

    await page.getByRole('button', { name: 'Record vendor response' }).click();
    await page.locator('#vresp-outcome').selectOption({ label: 'Declined' });
    await page.locator('#vresp-channel').selectOption({ label: 'WhatsApp' });
    await page.locator('#vresp-responder').fill('E2E Contact');
    await page.getByRole('button', { name: 'Record response' }).click();

    await expect(statusBadge(page)).toHaveText('Sent to Vendor');
    await expect(page.locator('#cancel-reason option:checked')).toHaveText('Vendor Declined');
    await page.getByRole('button', { name: 'Cancel PO' }).click();

    await expect(statusBadge(page)).toHaveText('Cancelled');
    await expect(page.locator('.timeline__status')).toHaveText(['Draft', 'Sent to Vendor', 'Cancelled']);
  });

  test('a cancelled PO offers no further actions', async ({ page, request }) => {
    const po = await createPo(request, 'acknowledged');
    await openPo(page, po);
    await page.locator('#cancel-reason').selectOption({ label: 'Duplicate Entry' });
    await page.getByRole('button', { name: 'Cancel PO' }).click();
    await expect(statusBadge(page)).toHaveText('Cancelled');

    for (const name of ['Send to Vendor', 'Record vendor response', 'Amend', 'Edit']) {
      await expect(page.getByRole('button', { name, exact: true })).toHaveCount(0);
    }
    await expect(page.getByText('cancelled and closed')).toBeVisible();
  });

  test('cancelling a PO with a pending amendment withdraws the amendment', async ({ page, request }) => {
    const po = await createPo(request, 'acknowledged');
    await openPo(page, po);
    await fillAmendment(page, { unitCost: '480' });
    await page.getByRole('button', { name: 'Submit amendment' }).click();
    await expect(page.getByRole('heading', { name: 'Revision 1 awaits a decision' })).toBeVisible();

    await page.locator('#cancel-reason').selectOption({ label: 'Duplicate Entry' });
    await page.getByRole('button', { name: 'Cancel PO' }).click();

    await expect(statusBadge(page)).toHaveText('Cancelled');
    await expect(page.getByRole('heading', { name: /awaits a decision/ })).toHaveCount(0);
    await expect(history(page).getByRole('article', { name: 'Revision 1' })).toContainText('Withdrawn');
  });
});

test.describe('Files and the tech pack', () => {
  test('a Draft PO takes files in any category and lets you remove them', async ({ page, request }) => {
    const po = await createPo(request, 'draft');
    await openPo(page, po);

    await page.locator('#file-category').selectOption({ label: 'Tech Pack Spec' });
    await page.locator('#file-input').setInputFiles(PDF);
    await expect(page.getByRole('region', { name: 'Files' }).getByRole('link', { name: 'tech-pack.pdf' })).toBeVisible();

    await page.getByRole('region', { name: 'Files' }).getByRole('button', { name: 'Remove' }).click();
    await expect(page.getByRole('link', { name: 'tech-pack.pdf' })).toHaveCount(0);
  });

  test('with a tech pack attached the PO sends with no "send anyway"; afterwards vendor-visible files are locked', async ({ page, request }) => {
    const po = await createPo(request, 'draft');
    await openPo(page, po);
    await page.locator('#file-category').selectOption({ label: 'Tech Pack Spec' });
    await page.locator('#file-input').setInputFiles(PDF);
    await expect(page.getByRole('link', { name: 'tech-pack.pdf' })).toBeVisible();

    await page.getByRole('button', { name: 'Send to Vendor' }).click();
    const dialog = page.getByRole('alertdialog');
    await expect(dialog.getByLabel('Send anyway, without a tech pack')).toHaveCount(0);
    await dialog.getByRole('button', { name: 'Send to Vendor' }).click();
    await expect(statusBadge(page)).toHaveText('Sent to Vendor');

    const remove = page.getByRole('region', { name: 'Files' }).getByRole('button', { name: 'Remove' });
    await expect(remove).toBeDisabled();
    await expect(page.getByRole('region', { name: 'Files' })).toContainText('Use Amend to retire a vendor-visible file');
    // The send was fully specified, so nothing was recorded as "sent without a tech pack".
    await expect(page.locator('.timeline')).not.toContainText('without a Tech Pack');
  });

  test('a downloaded file is forced to download and cannot be sniffed as another type', async ({ page, request }) => {
    const po = await createPo(request, 'draft');
    await openPo(page, po);
    await page.locator('#file-input').setInputFiles(PDF);
    const href = await page.getByRole('link', { name: 'tech-pack.pdf' }).getAttribute('href');

    const response = await request.get(href!);

    expect(response.status()).toBe(200);
    expect(response.headers()['content-disposition']).toContain('attachment');
    expect(response.headers()['x-content-type-options']).toBe('nosniff');
  });

  test('vendor-visible files can only be added to a sent PO through an amendment, and are then listed with their revision', async ({ page, request }) => {
    const po = await createPo(request, 'sent');
    await openPo(page, po);
    await expect(page.locator('#file-category option', { hasText: 'Tech Pack Spec' })).toHaveCount(0);

    await page.getByRole('button', { name: 'Amend', exact: true }).click();
    await page.locator('#amend-reason').selectOption({ label: 'Spec Change' });
    await page.locator('#amend-note').fill('E2E: attach the tech pack');
    await page.locator('#amend-file-category').selectOption({ label: 'Tech Pack Spec' });
    await page.locator('#amend-file').setInputFiles(PDF);
    await expect(page.getByText('Adding tech-pack.pdf')).toBeVisible();
    await page.getByRole('button', { name: 'Submit amendment' }).click();

    await expect(page.getByRole('region', { name: 'Files' })).toContainText('tech-pack.pdf');
    await expect(page.getByRole('region', { name: 'Files' })).toContainText('added in Rev 1');
  });

  test('evidence attached to a vendor response is kept as an internal file and linked from the history', async ({ page, request }) => {
    const po = await createPo(request, 'sent');
    await openPo(page, po);
    await page.getByRole('button', { name: 'Record vendor response' }).click();
    await page.locator('#vresp-channel').selectOption({ label: 'WhatsApp' });
    await page.locator('#vresp-responder').fill('E2E Contact');
    await page.locator('#vresp-evidence').setInputFiles({ name: 'chat.png', mimeType: 'image/png', buffer: Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0]) });
    await page.getByRole('button', { name: 'Record response' }).click();

    await expect(history(page)).toContainText('Evidence: chat.png');
    await expect(page.getByRole('region', { name: 'Files' })).toContainText('Internal only');
    await expect(page.getByRole('region', { name: 'Files' }).getByRole('link', { name: 'chat.png' })).toBeVisible();
  });
});

test.describe('Vendor view', () => {
  test('shows the agreed terms and a pending change, and never the internal note', async ({ page, request }) => {
    const po = await createPo(request, 'acknowledged');
    await openPo(page, po);
    await fillAmendment(page, { unitCost: '480', note: 'INTERNAL-ONLY-NOTE-XYZ' });
    await page.locator('#amend-message').fill('Please confirm the new price');
    await page.getByRole('button', { name: 'Submit amendment' }).click();
    await expect(page.getByRole('heading', { name: 'Revision 1 awaits a decision' })).toBeVisible();

    await page.goto(`/purchase-orders/${po.id}/vendor-view`);

    await expect(page.getByRole('heading', { level: 1 })).toContainText(po.poNo);
    await expect(page.getByText('PKR 420')).toBeVisible(); // still the agreed price
    await expect(page.getByText('A change is proposed and not yet agreed')).toBeVisible();
    await expect(page.getByText('Please confirm the new price')).toBeVisible();
    await expect(page.locator('body')).not.toContainText('INTERNAL-ONLY-NOTE-XYZ');
    await expect(page.locator('nav')).toHaveCount(0);
  });

  test('a Draft PO is not available to a vendor', async ({ page, request }) => {
    const po = await createPo(request, 'draft');

    await page.goto(`/purchase-orders/${po.id}/vendor-view`);

    await expect(page.getByText('This purchase order is not available.')).toBeVisible();
  });
});

test.describe('Style guard and reference data', () => {
  test('a size that a live PO uses cannot be removed from its style, and the message names the PO', async ({ page, request }) => {
    const po = await createPo(request, 'sent');
    await page.goto('/styles');
    await page.getByPlaceholder('Search by code or name').fill(po.styleCode);
    await page.getByRole('row').filter({ hasText: po.styleCode }).getByRole('button', { name: 'Edit' }).click();

    await page.getByRole('group', { name: 'Size run' }).getByLabel('1-2 Years').uncheck();
    await page.getByRole('button', { name: 'Save' }).click();

    await expect(page.getByRole('alert').first()).toContainText(po.poNo);
    await expect(page.getByRole('alert').first()).toContainText('cannot be removed');
  });

  test('a new amendment reason added in Reference Data is offered in the amendment form', async ({ page, request }) => {
    const reason = `E2E reason ${uniqueSuffix()}`;
    await page.goto('/reference-data');
    await page.getByRole('tab', { name: 'Amendment Reasons' }).click();
    await page.getByRole('button', { name: 'Add value' }).click();
    await page.getByLabel('Code').fill(`E2E_${uniqueSuffix()}`);
    await page.getByLabel('Name').fill(reason);
    await page.getByRole('button', { name: 'Save' }).click();
    await expect(page.getByRole('cell', { name: reason })).toBeVisible();

    const po = await createPo(request, 'sent');
    await openPo(page, po);
    await page.getByRole('button', { name: 'Amend', exact: true }).click();

    await expect(page.locator('#amend-reason option', { hasText: reason })).toHaveCount(1);
  });

  test('the system-owned Sprint 2 lookups are not editable from the screen', async ({ page }) => {
    await page.goto('/reference-data');

    for (const name of ['Revision Statuses', 'File Categories', 'Vendor Communication Types']) {
      await expect(page.getByRole('tab', { name })).toHaveCount(0);
    }
    const response = await page.request.post(`${API}/api/ref/revision-statuses`, { data: { code: 'X', name: 'X', sortSeq: 1 } });
    expect([404, 405]).toContain(response.status());
  });
});
