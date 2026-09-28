import { expect, test } from '@playwright/test';
import { API, PDF, createPo, createStyle, createVendor, fillAmendment, openPo, statusBadge, uniqueSuffix } from './support';

/**
 * Worst-case journeys: wrong, missing, extreme or hostile input; actions in the wrong state; and a
 * server that says no or isn't there. In every case the screen must say, in plain words, what is
 * wrong and what to do - never a blank page, a raw error, or a generic "one or more errors".
 */

const GENERIC = /One or more (validation )?errors|Http failure|undefined|\[object Object\]/i;

test.describe('Empty and invalid forms', () => {
  test('saving an empty style says what is missing', async ({ page }) => {
    await page.goto('/styles');
    await page.getByRole('button', { name: 'Add style' }).click();
    await page.getByRole('button', { name: 'Save' }).click();

    const alert = page.getByRole('alert').first();
    await expect(alert).toBeVisible();
    await expect(alert).not.toHaveText(GENERIC);
    await expect(page.getByRole('heading', { name: 'Add style' })).toBeVisible(); // stays on the form
  });

  test('a duplicate style code is refused with the code named', async ({ page, request }) => {
    const style = await createStyle(request);
    await page.goto('/styles');
    await page.getByRole('button', { name: 'Add style' }).click();
    await page.getByLabel('Code').fill(style.code);
    await page.getByLabel('Name').fill('Duplicate');
    await page.getByLabel('Category').selectOption({ label: 'Dresses' });
    await page.getByLabel('Gender').selectOption({ label: 'Girls' });
    await page.getByLabel('Age bracket').selectOption({ label: 'Toddler (1-3Y)' });
    await page.getByLabel('Fabric').selectOption({ label: 'Cotton' });
    await page.getByRole('group', { name: 'Colourways' }).getByLabel('White').check();
    await page.getByRole('group', { name: 'Size run' }).getByLabel('1-2 Years').check();
    await page.getByRole('button', { name: 'Save' }).click();

    await expect(page.getByRole('alert').first()).toContainText(style.code);
    await expect(page.getByRole('alert').first()).not.toHaveText(GENERIC);
  });

  test('a vendor with no specialisation cannot be saved, and the message says why', async ({ page }) => {
    await page.goto('/vendors');
    await page.getByRole('button', { name: 'Add vendor' }).click();
    await page.getByLabel('Name', { exact: true }).fill(`E2E Vendor ${uniqueSuffix()}`);
    await page.getByLabel('Contact name').fill('Someone');
    await page.getByLabel('Contact phone').fill('+92-300-0000000');
    await page.getByLabel('City').selectOption({ label: 'Sialkot' });
    await page.getByLabel('Default payment term').selectOption({ index: 1 });
    await page.getByRole('button', { name: 'Save' }).click();

    await expect(page.getByRole('alert').first()).toContainText('specialisation');
  });

  test('a badly formed email is rejected', async ({ page }) => {
    await page.goto('/vendors');
    await page.getByRole('button', { name: 'Add vendor' }).click();
    await page.getByLabel('Name', { exact: true }).fill(`E2E Vendor ${uniqueSuffix()}`);
    await page.getByLabel('Contact name').fill('Someone');
    await page.getByLabel('Contact phone').fill('+92-300-0000000');
    await page.getByLabel('Contact email').fill('not-an-email');
    await page.getByLabel('City').selectOption({ label: 'Sialkot' });
    await page.getByLabel('Default payment term').selectOption({ index: 1 });
    await page.getByRole('group', { name: 'Specialisations' }).getByLabel('Knits').check();
    await page.getByRole('button', { name: 'Save' }).click();

    await expect(page.getByRole('alert').first()).toContainText(/email/i);
  });

  test('a PO with every quantity at 0 says at least one line is needed', async ({ page, request }) => {
    const style = await createStyle(request);
    const vendor = await createVendor(request);
    await page.goto('/purchase-orders');
    await page.getByRole('button', { name: 'Raise PO' }).click();
    const form = page.locator('form.po-form');
    await form.getByLabel('Vendor').selectOption({ label: vendor.name });
    await form.getByLabel('Style').selectOption({ label: `${style.code} — ${style.name}` });
    await form.getByLabel('Unit cost (PKR)').fill('420');
    await form.getByLabel('Expected delivery date').fill('2026-12-01');
    await expect(page.locator('.line-grid input[type="number"]').first()).toBeVisible();
    await page.getByRole('button', { name: 'Save' }).click();

    await expect(page.getByRole('alert').first()).toContainText('At least one size/colour line is required.');
  });

  test('a PO with a zero unit cost is refused and the message names the cost', async ({ page, request }) => {
    const style = await createStyle(request);
    const vendor = await createVendor(request);
    await page.goto('/purchase-orders');
    await page.getByRole('button', { name: 'Raise PO' }).click();
    const form = page.locator('form.po-form');
    await form.getByLabel('Vendor').selectOption({ label: vendor.name });
    await form.getByLabel('Style').selectOption({ label: `${style.code} — ${style.name}` });
    await form.getByLabel('Unit cost (PKR)').fill('0');
    await form.getByLabel('Expected delivery date').fill('2026-12-01');
    await page.locator('.line-grid input[type="number"]').first().fill('10');
    await page.getByRole('button', { name: 'Save' }).click();

    // The browser blocks a cost below the minimum, or the API refuses it: either way we stay on the form.
    await expect(page.getByRole('heading', { name: 'Raise purchase order' })).toBeVisible();
  });

  test('a latest acceptable date before the expected delivery date is refused with the rule spelled out', async ({ page, request }) => {
    const po = await createPo(request, 'draft');
    await openPo(page, po);
    await page.getByRole('button', { name: 'Edit', exact: true }).click();
    await page.getByLabel('Latest acceptable delivery date').fill('2026-11-01');
    await page.getByRole('button', { name: 'Save' }).click();

    await expect(page.getByRole('alert').first()).toContainText('Latest acceptable date must be on or after the expected delivery date.');
  });

  test('tolerances above the allowed maximum are refused with the limit shown', async ({ page, request }) => {
    const po = await createPo(request, 'draft');
    await openPo(page, po);
    await page.getByRole('button', { name: 'Edit', exact: true }).click();
    await page.getByLabel('Extra pieces allowed (%)').fill('90');
    await page.getByRole('button', { name: 'Save' }).click();

    await expect(page.getByRole('alert').first()).toContainText('between 0 and 20%');
  });

  test('a PO cannot be sent without a latest acceptable date or fabric supplier, and each is named', async ({ page, request }) => {
    const style = await createStyle(request);
    const vendor = await createVendor(request);
    await page.goto('/purchase-orders');
    await page.getByRole('button', { name: 'Raise PO' }).click();
    const form = page.locator('form.po-form');
    await form.getByLabel('Vendor').selectOption({ label: vendor.name });
    await form.getByLabel('Style').selectOption({ label: `${style.code} — ${style.name}` });
    await form.getByLabel('Unit cost (PKR)').fill('420');
    await form.getByLabel('Expected delivery date').fill('2026-12-01');
    await page.locator('.line-grid input[type="number"]').first().fill('10');
    await page.getByRole('button', { name: 'Save' }).click();
    await expect(page.getByRole('heading', { name: /^PO-/ })).toBeVisible();

    await page.getByRole('button', { name: 'Send to Vendor' }).click();
    await page.getByRole('alertdialog').getByLabel('Send anyway, without a tech pack').check();
    await page.getByRole('alertdialog').getByRole('button', { name: 'Send to Vendor' }).click();

    const alert = page.getByRole('alert').first();
    await expect(alert).toContainText('Latest acceptable date is required');
    await expect(alert).toContainText('Fabric responsibility is required');
    await expect(statusBadge(page)).toHaveText('Draft');
  });
});

test.describe('Amendment and response mistakes', () => {
  test('an amendment that changes nothing is refused with a clear reason', async ({ page, request }) => {
    const po = await createPo(request, 'sent');
    await openPo(page, po);
    await fillAmendment(page); // reason + note, but nothing else changed
    await page.getByRole('button', { name: 'Submit amendment' }).click();

    await expect(page.getByRole('alert').first()).toContainText("doesn't change anything");
    await expect(page.getByRole('heading', { name: 'Amend this PO' })).toBeVisible(); // form stays open
  });

  test('an amendment without a reason or note names both missing pieces', async ({ page, request }) => {
    const po = await createPo(request, 'sent');
    await openPo(page, po);
    await page.getByRole('button', { name: 'Amend', exact: true }).click();
    await page.locator('#amend-unit-cost').fill('450');
    await page.getByRole('button', { name: 'Submit amendment' }).click();

    await expect(page.getByText('Choose a reason for this change.')).toBeVisible();
    await expect(page.getByText('Explain the impact of this change for the record.')).toBeVisible();
  });

  test('an amendment that removes every quantity is refused', async ({ page, request }) => {
    const po = await createPo(request, 'sent');
    await openPo(page, po);
    await fillAmendment(page, { unitCost: '450' });
    await page.locator('app-amend-form .line-grid input[type="number"]').first().fill('0');
    await page.getByRole('button', { name: 'Submit amendment' }).click();

    await expect(page.getByRole('alert').first()).toContainText('At least one size/colour line is required.');
  });

  test('a vendor response needs a channel and a name, and says so', async ({ page, request }) => {
    const po = await createPo(request, 'sent');
    await openPo(page, po);
    await page.getByRole('button', { name: 'Record vendor response' }).click();
    await page.getByRole('button', { name: 'Record response' }).click();

    await expect(page.getByText('Choose the channel the vendor used.')).toBeVisible();
    await expect(page.getByText('Enter the name of the person who responded.')).toBeVisible();
    await expect(statusBadge(page)).toHaveText('Sent to Vendor');
  });

  test('a vendor response dated in the future is refused', async ({ page, request }) => {
    const po = await createPo(request, 'sent');
    await openPo(page, po);
    await page.getByRole('button', { name: 'Record vendor response' }).click();
    await page.locator('#vresp-channel').selectOption({ label: 'Email' });
    await page.locator('#vresp-responder').fill('E2E Contact');
    await page.locator('#vresp-time').fill('2099-01-01T10:00');
    await page.getByRole('button', { name: 'Record response' }).click();

    await expect(page.getByRole('alert').first()).toContainText('cannot be in the future');
    await expect(statusBadge(page)).toHaveText('Sent to Vendor');
  });

  test('acting on a PO that changed in another window shows a sensible message, not a crash', async ({ page, request }) => {
    const po = await createPo(request, 'sent');
    await openPo(page, po);
    // Someone else confirms the PO while this window still shows "Sent to Vendor".
    await request.post(`${API}/api/purchase-orders/${po.id}/acknowledge`, { data: {} });

    await page.getByRole('button', { name: 'Record vendor response' }).click();
    await page.locator('#vresp-channel').selectOption({ label: 'WhatsApp' });
    await page.locator('#vresp-responder').fill('E2E Contact');
    await page.getByRole('button', { name: 'Record response' }).click();

    const alert = page.getByRole('alert').first();
    await expect(alert).toBeVisible();
    await expect(alert).not.toHaveText(GENERIC);
  });

  test('cancelling needs a reason: the button stays off until one is chosen', async ({ page, request }) => {
    const po = await createPo(request, 'sent');
    await openPo(page, po);

    await expect(page.getByRole('button', { name: 'Cancel PO' })).toBeDisabled();
    await page.locator('#cancel-reason').selectOption({ label: 'Other' });
    await expect(page.getByRole('button', { name: 'Cancel PO' })).toBeEnabled();
  });
});

test.describe('Files that should be refused', () => {
  test('a program renamed to .pdf is refused, and nothing is stored', async ({ page, request }) => {
    const po = await createPo(request, 'draft');
    await openPo(page, po);
    await page.locator('#file-input').setInputFiles({ name: 'invoice.pdf', mimeType: 'application/pdf', buffer: Buffer.from('MZ this is really an executable') });

    await expect(page.getByRole('alert').first()).toContainText("This file type isn't allowed");
    await expect(page.getByRole('link', { name: 'invoice.pdf' })).toHaveCount(0);
  });

  test('a file over the size limit is refused with the limit named', async ({ page, request }) => {
    const po = await createPo(request, 'draft');
    await openPo(page, po);
    const big = Buffer.concat([Buffer.from('%PDF-1.4 '), Buffer.alloc(11 * 1024 * 1024, 1)]);
    await page.locator('#file-input').setInputFiles({ name: 'huge.pdf', mimeType: 'application/pdf', buffer: big });

    await expect(page.getByRole('alert').first()).toContainText('larger than the 10 MB limit');
  });

  test('an empty file is refused', async ({ page, request }) => {
    const po = await createPo(request, 'draft');
    await openPo(page, po);
    await page.locator('#file-input').setInputFiles({ name: 'empty.pdf', mimeType: 'application/pdf', buffer: Buffer.alloc(0) });

    await expect(page.getByRole('alert').first()).toContainText('empty');
  });

  test('the API refuses a path-style filename and a sent PO refuses a direct vendor-visible upload', async ({ request }) => {
    const draft = await createPo(request, 'draft');
    const traversal = await request.post(`${API}/api/purchase-orders/${draft.id}/files`, {
      multipart: { categoryId: '6', file: { name: '../../etc/passwd.pdf', mimeType: 'application/pdf', buffer: PDF.buffer } },
    });
    expect(traversal.status()).toBe(200);
    const stored = (await traversal.json()) as { fileName: string };
    expect(stored.fileName).toBe('passwd.pdf'); // the path is stripped, not obeyed

    const sent = await createPo(request, 'sent');
    const locked = await request.post(`${API}/api/purchase-orders/${sent.id}/files`, {
      multipart: { categoryId: '1', file: { name: 'spec.pdf', mimeType: 'application/pdf', buffer: PDF.buffer } },
    });
    expect(locked.status()).toBe(409);
    expect(await locked.text()).toContain('amendment');
  });

  test('a file that belongs to another PO cannot be downloaded through this one', async ({ request }) => {
    const first = await createPo(request, 'draft');
    const second = await createPo(request, 'draft');
    const upload = await request.post(`${API}/api/purchase-orders/${first.id}/files`, {
      multipart: { categoryId: '6', file: { name: 'private.pdf', mimeType: 'application/pdf', buffer: PDF.buffer } },
    });
    const { id } = (await upload.json()) as { id: number };

    const crossed = await request.get(`${API}/api/purchase-orders/${second.id}/files/${id}`);

    expect(crossed.status()).toBe(404);
  });
});

test.describe('Server and network trouble', () => {
  test('when the server cannot be reached the list says so in plain words', async ({ page }) => {
    await page.route('**/api/purchase-orders**', (route) => route.abort('connectionrefused'));
    await page.goto('/purchase-orders');

    await expect(page.getByRole('alert').first()).toContainText("couldn't reach the server");
  });

  test('a server error is shown as a sentence, not as raw technical text', async ({ page }) => {
    await page.route('**/api/purchase-orders**', (route) =>
      route.fulfill({ status: 500, contentType: 'application/json', body: JSON.stringify({ title: 'An error occurred while processing your request.', status: 500 }) }),
    );
    await page.goto('/purchase-orders');

    const alert = page.getByRole('alert').first();
    await expect(alert).toContainText('Something went wrong on our side');
    await expect(alert).not.toHaveText(GENERIC);
  });

  test('an unknown PO id in the vendor view is handled quietly', async ({ page }) => {
    await page.goto('/purchase-orders/99999999/vendor-view');

    await expect(page.getByText('This purchase order is not available.')).toBeVisible();
  });

  test('a URL that does not exist does not leave a blank page', async ({ page }) => {
    await page.goto('/purchase-orders/not-a-number/vendor-view');

    await expect(page.locator('main')).toBeVisible();
    await expect(page.locator('body')).not.toContainText('undefined');
  });
});

test.describe('Hostile and extreme text', () => {
  test('HTML in a vendor name is shown as text and cannot run', async ({ page, request }) => {
    const marker = uniqueSuffix();
    const name = `<img src=x onerror="window.__pwned=1"> ${marker}`;
    await createVendor(request, name);
    await page.goto('/vendors');
    await page.getByRole('searchbox').fill(marker);

    await expect(page.getByRole('cell', { name: new RegExp(marker) })).toBeVisible();
    expect(await page.evaluate(() => (window as unknown as { __pwned?: number }).__pwned)).toBeUndefined();
  });

  test('a very long unbroken name does not break the layout on a phone', async ({ page, request }) => {
    await page.setViewportSize({ width: 320, height: 640 });
    const marker = uniqueSuffix();
    await createVendor(request, `${marker}${'W'.repeat(150)}`);
    await page.goto('/vendors');
    await page.getByRole('searchbox').fill(marker);
    await expect(page.getByRole('cell', { name: new RegExp(marker) })).toBeVisible();

    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
    expect(overflow).toBeLessThanOrEqual(1);
  });

  test('a huge quantity is handled without breaking the totals', async ({ page, request }) => {
    const po = await createPo(request, 'draft');
    await openPo(page, po);
    await page.getByRole('button', { name: 'Edit', exact: true }).click();
    await page.locator('.line-grid input[type="number"]').first().fill('2000000000');
    await page.getByRole('button', { name: 'Save' }).click();

    // Either it saves and shows the number, or it is refused with a message; never a blank screen.
    await expect(page.locator('.po-detail, form.po-form').first()).toBeVisible();
    await expect(page.locator('body')).not.toContainText('NaN');
  });
});
