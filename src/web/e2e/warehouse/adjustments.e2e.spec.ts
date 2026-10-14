import { expect, test } from '@playwright/test';
import { apiAdjust, apiCreateBin, createVariant } from '../support';

/**
 * Sprint 6 (Warehouse), adjustments: every correction needs a reason and a name, shows the balance before and after, and can never be
 * edited. A change above the limit asks for an explicit confirmation before it is applied.
 */
test.describe.configure({ timeout: 180_000 });

test.describe('Warehouse: adjustments', () => {
  test('removing damaged units records the reason, the person and the new balance', async ({ page, request }) => {
    const variant = await createVariant(request);
    const bin = await apiCreateBin(request);
    await apiAdjust(request, { gtin: variant.gtin, binCode: bin.binCode, change: 10 });

    await page.goto('/warehouse/adjustments');
    await expect(page.getByRole('heading', { level: 1, name: 'Adjustments' })).toBeVisible();
    await page.getByRole('button', { name: 'New adjustment' }).click();
    await page.getByLabel('Barcode (13 digits)').fill(variant.gtin);
    await page.getByLabel('Bin', { exact: true }).fill(bin.binCode);
    await page.getByLabel('Reason', { exact: true }).selectOption({ label: 'Damage (removes units)' });
    await page.getByLabel('Change (use a minus to remove)').fill('-3');
    await page.getByLabel('Note (optional)').fill('crushed in the aisle');
    await page.getByLabel('Your name').fill('E2E Admin');
    await page.getByRole('button', { name: 'Record adjustment' }).click();

    await expect(page.getByRole('status').filter({ hasText: '-3 unit(s)' })).toBeVisible();
    await expect(page.getByRole('status').filter({ hasText: '-3 unit(s)' })).toContainText('The bin now holds 7');
    const row = page.getByRole('row').filter({ hasText: variant.skuCode }).first();
    await expect(row).toContainText('10 → 7');
    await expect(row).toContainText('Damage');
    await expect(row).toContainText('crushed in the aisle');
    await expect(row).toContainText('E2E Admin');
    await expect(row.getByRole('button')).toHaveCount(0); // a record: no edit, no delete
  });

  test('a form without a reason or with a zero change is stopped before it is sent', async ({ page }) => {
    await page.goto('/warehouse/adjustments');
    await page.getByRole('button', { name: 'New adjustment' }).click();
    await page.getByLabel('Barcode (13 digits)').fill('2000000000015');
    await page.getByLabel('Bin', { exact: true }).fill('A-01-A');
    await page.getByLabel('Your name').fill('E2E Admin');
    await page.getByRole('button', { name: 'Record adjustment' }).click();

    await expect(page.getByRole('alert').filter({ hasText: "This can't be saved yet" })).toBeVisible();
  });

  test('a large change asks for a confirmation, and applies only when it is given', async ({ page, request }) => {
    const variant = await createVariant(request);
    const bin = await apiCreateBin(request);

    await page.goto('/warehouse/adjustments');
    await page.getByRole('button', { name: 'New adjustment' }).click();
    await page.getByLabel('Barcode (13 digits)').fill(variant.gtin);
    await page.getByLabel('Bin', { exact: true }).fill(bin.binCode);
    await page.getByLabel('Reason', { exact: true }).selectOption({ label: 'Found stock (adds units)' });
    await page.getByLabel('Change (use a minus to remove)').fill('45');
    await page.getByLabel('Your name').fill('E2E Admin');
    await page.getByRole('button', { name: 'Record adjustment' }).click();

    const dialog = page.getByRole('alertdialog');
    await expect(dialog).toContainText('Confirmation required');
    await expect(dialog).toContainText('45 units');
    await dialog.getByRole('button', { name: 'Go back and change it' }).click();
    await expect(page.getByRole('alertdialog')).toHaveCount(0);
    await expect(page.getByLabel('Change (use a minus to remove)')).toHaveValue('45'); // nothing typed was lost

    await page.getByRole('button', { name: 'Record adjustment' }).click();
    await page.getByRole('alertdialog').getByRole('button', { name: 'Yes, apply this change' }).click();

    await expect(page.getByRole('status').filter({ hasText: '+45 unit(s)' })).toBeVisible();
    await expect(page.getByRole('row').filter({ hasText: variant.skuCode }).first()).toContainText('Large change, confirmed');
  });

  test('removing more than the bin holds is refused with the server\'s words', async ({ page, request }) => {
    const variant = await createVariant(request);
    const bin = await apiCreateBin(request);
    await apiAdjust(request, { gtin: variant.gtin, binCode: bin.binCode, change: 2 });

    await page.goto('/warehouse/adjustments');
    await page.getByRole('button', { name: 'New adjustment' }).click();
    await page.getByLabel('Barcode (13 digits)').fill(variant.gtin);
    await page.getByLabel('Bin', { exact: true }).fill(bin.binCode);
    await page.getByLabel('Reason', { exact: true }).selectOption({ label: 'Miscount (adds or removes units)' });
    await page.getByLabel('Change (use a minus to remove)').fill('-5');
    await page.getByLabel('Your name').fill('E2E Admin');
    await page.getByRole('button', { name: 'Record adjustment' }).click();

    await expect(page.getByRole('alert').filter({ hasText: 'Not enough stock in the bin' })).toBeVisible();
  });
});
