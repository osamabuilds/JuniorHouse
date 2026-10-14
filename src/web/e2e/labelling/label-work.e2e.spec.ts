import { expect, test } from '@playwright/test';
import { API, apiQueueTags, createAcceptedStock, waitForWorkItem } from '../support';

/**
 * Sprint 5 (Labelling): the journey of accepted stock from QC to a printed tag, driven through the admin app. QC accepts 60 pieces
 * (the rest is rejected); the event reaches Labelling on its own; staff queue tags; the background worker prints them.
 * Needs the stack from `docker compose up` (the API runs the Development settings: the preview "printer" and fast retries).
 */
test.describe.configure({ timeout: 180_000 }); // setting stock up goes through QC and the event dispatcher, which takes several seconds

test.describe('Labelling: from accepted stock to a printed tag', () => {
  test('accepted stock appears on the worklist with its SKU and barcode, and its tags are queued and printed', async ({ page, request }) => {
    const stock = await createAcceptedStock(request, 60);
    const item = await waitForWorkItem(request, stock.run.po.id);
    expect(item.skuCode).toMatch(/^ROMP-/);
    expect(item.gtin).toMatch(/^\d{13}$/);

    await page.goto('/labelling/worklist');
    await expect(page.getByRole('heading', { level: 1, name: 'Label worklist' })).toBeVisible();
    const row = page.getByRole('row').filter({ hasText: item.skuCode! });
    await expect(row).toContainText('Ready to print');
    await expect(row).toContainText(item.gtin!);
    await expect(row).toContainText('60');
    await expect(row).toContainText('0 printed');

    // Queue 25 of the 60 tags.
    await row.getByRole('button', { name: 'Queue tags' }).click();
    await expect(page.getByRole('heading', { name: `Queue tags for ${item.skuCode}` })).toBeVisible();
    await page.getByLabel('How many tags').fill('25');
    await page.getByLabel('Your name').fill('E2E Warehouse');
    await page.getByRole('form').getByRole('button', { name: 'Queue tags' }).click();
    await expect(page.getByRole('status').filter({ hasText: 'Queued 25 tags as job PJ-' })).toBeVisible();

    // The background worker prints them; the worklist counts them as printed.
    await expect(async () => {
      await page.getByRole('button', { name: 'Refresh' }).click();
      await expect(row).toContainText('25 printed', { timeout: 2_000 });
    }).toPass({ timeout: 40_000 });
    await expect(row).toContainText('35 still allowed');
  });

  test('more tags than the accepted quantity are refused with the reason, and nothing is queued', async ({ page, request }) => {
    const stock = await createAcceptedStock(request, 10);
    const item = await waitForWorkItem(request, stock.run.po.id);
    await apiQueueTags(request, item.id, 4);

    await page.goto('/labelling/worklist');
    const row = page.getByRole('row').filter({ hasText: item.skuCode! });
    await row.getByRole('button', { name: 'Queue tags' }).click();
    await page.getByLabel('How many tags').fill('7');
    await page.getByLabel('Your name').fill('E2E Warehouse');
    // The form itself limits the count to what is allowed (6 more), so submitting is blocked before the server is asked.
    await page.getByRole('form').getByRole('button', { name: 'Queue tags' }).click();
    await expect(page.getByRole('alert').filter({ hasText: "can't be queued yet" })).toBeVisible();

    // The server enforces the same limit for anyone who skips the form (PJ-1).
    const refused = await request.post(`${API}/api/labelling/work-items/${item.id}/print-jobs`, {
      data: { qty: 7, printerCode: 'FAKE-PDF', actingPerson: 'E2E API' },
    });
    expect(refused.status()).toBe(400);
    expect(JSON.stringify(await refused.json())).toContain('Only 6 more tags are allowed');
  });

  test('the tag preview is a real PDF', async ({ page, request }) => {
    const stock = await createAcceptedStock(request, 12);
    const item = await waitForWorkItem(request, stock.run.po.id);

    await page.goto('/labelling/worklist');
    const row = page.getByRole('row').filter({ hasText: item.skuCode! });
    const href = await row.getByRole('link', { name: 'Preview tag' }).getAttribute('href');
    expect(href).toContain(`/api/labelling/work-items/${item.id}/tag-preview`);

    const pdf = await request.get(href!);
    expect(pdf.status()).toBe(200);
    expect(pdf.headers()['content-type']).toContain('application/pdf');
    expect(pdf.headers()['x-content-type-options']).toBe('nosniff');
    expect((await pdf.body()).subarray(0, 8).toString('ascii')).toBe('%PDF-1.4');
  });

  test('every Labelling screen is reachable from the main menu and the section menu, by keyboard-friendly links', async ({ page }) => {
    await page.goto('/');
    await page.getByRole('navigation', { name: 'Primary' }).getByRole('link', { name: 'Labelling' }).click();
    await expect(page).toHaveURL(/\/labelling\/worklist$/);

    const section = page.getByRole('navigation', { name: 'Labelling' });
    await section.getByRole('link', { name: 'Print queue' }).click();
    await expect(page.getByRole('heading', { level: 1, name: 'Print queue' })).toBeVisible();
    await section.getByRole('link', { name: 'SKU register' }).click();
    await expect(page.getByRole('heading', { level: 1, name: 'SKU register' })).toBeVisible();
    await expect(section.getByRole('link', { name: 'SKU register' })).toHaveAttribute('aria-current', 'page');
  });
});
