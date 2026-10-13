import { expect, test } from '@playwright/test';
import { apiQueueTags, createAcceptedStock, waitForWorkItem } from '../support';

/**
 * Sprint 5 (Labelling), the print queue: a jammed printer retries and then fails without stopping other work, staff can retry or
 * cancel, and a lost tag is reprinted with the same barcode. The "Jammed printer" is a development printer that always fails.
 */
test.describe.configure({ timeout: 180_000 }); // setting stock up goes through QC and the event dispatcher, which takes several seconds

test.describe('Labelling: the print queue', () => {
  test('a jammed printer retries, ends Failed with the reason, and the same job can be retried and cancelled', async ({ page, request }) => {
    const stock = await createAcceptedStock(request, 20);
    const item = await waitForWorkItem(request, stock.run.po.id);
    const job = await apiQueueTags(request, item.id, 5, 'FAKE-JAM');

    await page.goto('/labelling/queue');
    await expect(page.getByRole('heading', { level: 1, name: 'Print queue' })).toBeVisible();
    const row = page.getByRole('row').filter({ hasText: job.jobNo });

    // Development retries after 3 s and 6 s; the screen refreshes itself every few seconds.
    await expect(row).toContainText('Failed', { timeout: 45_000 });
    await expect(row).toContainText('Paper jam (simulated).');

    await row.getByRole('button', { name: 'Retry' }).click();
    await expect(page.getByRole('status').filter({ hasText: `Job ${job.jobNo} is back in the queue` })).toBeVisible();
    await expect(row).toContainText('Failed', { timeout: 45_000 }); // the printer is still jammed

    await row.getByRole('button', { name: 'Cancel' }).click();
    await expect(row).toContainText('Cancelled');
    await expect(row.getByRole('button')).toHaveCount(0);
  });

  test('a jammed printer does not stop a working one', async ({ page, request }) => {
    const stock = await createAcceptedStock(request, 20);
    const item = await waitForWorkItem(request, stock.run.po.id);
    const stuck = await apiQueueTags(request, item.id, 3, 'FAKE-JAM');
    const fine = await apiQueueTags(request, item.id, 3, 'FAKE-PDF');

    await page.goto('/labelling/queue');
    await expect(page.getByRole('row').filter({ hasText: fine.jobNo })).toContainText('Printed', { timeout: 40_000 });
    await expect(page.getByRole('row').filter({ hasText: stuck.jobNo })).not.toContainText('Printed');
  });

  test('a lost tag is reprinted with the same barcode, with a reason and the person asking', async ({ page, request }) => {
    const stock = await createAcceptedStock(request, 20);
    const item = await waitForWorkItem(request, stock.run.po.id);
    await apiQueueTags(request, item.id, 5);

    await page.goto(`/labelling/queue?variantId=${item.variantId}&reprint=1&sku=${encodeURIComponent(item.skuCode!)}`);
    await expect(page.getByRole('heading', { name: `Reprint tags for ${item.skuCode}` })).toBeVisible();
    await page.getByLabel('How many tags').fill('2');
    await page.getByLabel('Why').selectOption({ label: 'Tag damaged' });
    await page.getByLabel('Note (optional)').fill('ripped in the bin');
    await page.getByLabel('Your name').fill('E2E Warehouse');
    await page.getByRole('button', { name: 'Queue reprint' }).click();

    await expect(page.getByRole('status').filter({ hasText: 'Reprint queued as job PJ-' })).toBeVisible();
    const reprintRow = page.getByRole('row').filter({ hasText: 'Reason: Tag damaged'.replace('Tag damaged', 'Damaged') });
    await expect(reprintRow).toContainText('ripped in the bin');
    await expect(reprintRow).toContainText(item.gtin!); // the SAME barcode: no new number was made
    await expect(page.getByRole('heading', { level: 1, name: 'Print history' })).toBeVisible();
  });

  test('a reprint without a name is refused before anything is sent', async ({ page, request }) => {
    const stock = await createAcceptedStock(request, 20);
    const item = await waitForWorkItem(request, stock.run.po.id);

    await page.goto(`/labelling/queue?variantId=${item.variantId}&reprint=1&sku=${encodeURIComponent(item.skuCode!)}`);
    await page.getByLabel('Your name').fill('');
    await page.getByRole('button', { name: 'Queue reprint' }).click();

    await expect(page.getByRole('alert').filter({ hasText: "can't be queued yet" })).toBeVisible();
  });
});
