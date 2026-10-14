import { expect, test } from '@playwright/test';
import { uniqueSuffix } from '../support';

/**
 * Sprint 6 (Warehouse), bins: a bin is a zone, an aisle and a shelf; its code is what is printed on the shelf. Duplicates are refused,
 * bad parts are caught on the form, and a bin can be switched off and on again while it is empty.
 */
test.describe('Warehouse: bins', () => {
  test('adding a bin shows its code in the list, and a second bin with the same code is refused', async ({ page }) => {
    const zone = `B${uniqueSuffix().slice(-2)}`;

    await page.goto('/warehouse/bins');
    await expect(page.getByRole('heading', { level: 1, name: 'Bins' })).toBeVisible();

    await page.getByRole('button', { name: 'Add a bin' }).click();
    await page.getByLabel('Zone', { exact: true }).fill(zone.toLowerCase());
    await page.getByLabel('Aisle').fill('03');
    await page.getByLabel('Shelf').fill('b');
    await page.getByLabel('Your name').fill('E2E Admin');
    await page.getByRole('button', { name: 'Add bin', exact: true }).click();

    await expect(page.getByRole('status').filter({ hasText: `Bin ${zone}-03-B was added.` })).toBeVisible();
    await page.getByRole('searchbox', { name: 'Filter by zone' }).fill(zone);
    const row = page.getByRole('row').filter({ hasText: `${zone}-03-B` });
    await expect(row).toContainText('Never');
    await expect(row).toContainText('Active');

    await page.getByRole('button', { name: 'Add a bin' }).click();
    await page.getByLabel('Zone', { exact: true }).fill(zone);
    await page.getByLabel('Aisle').fill('03');
    await page.getByLabel('Shelf').fill('B');
    await page.getByLabel('Your name').fill('E2E Admin');
    await page.getByRole('button', { name: 'Add bin', exact: true }).click();
    await expect(page.getByRole('alert').filter({ hasText: 'already exists' })).toBeVisible();
  });

  test('a zone, aisle or shelf outside the pattern is caught on the form before anything is sent', async ({ page }) => {
    await page.goto('/warehouse/bins');
    await page.getByRole('button', { name: 'Add a bin' }).click();

    await page.getByLabel('Zone', { exact: true }).fill('TOOL');
    await page.getByLabel('Aisle').fill('xx');
    await page.getByLabel('Shelf').fill('');
    await page.getByLabel('Your name').fill('E2E Admin');
    await page.getByRole('button', { name: 'Add bin', exact: true }).click();

    await expect(page.getByRole('alert').filter({ hasText: "This can't be saved yet" })).toBeVisible();
  });

  test('an empty bin can be deactivated and activated again', async ({ page }) => {
    const zone = `D${uniqueSuffix().slice(-2)}`;
    await page.goto('/warehouse/bins');
    await page.getByRole('searchbox', { name: 'Filter by zone' }).fill(zone);
    await page.getByRole('button', { name: 'Add a bin' }).click();
    await page.getByLabel('Zone', { exact: true }).fill(zone);
    await page.getByLabel('Aisle').fill('01');
    await page.getByLabel('Shelf').fill('A');
    await page.getByLabel('Your name').fill('E2E Admin');
    await page.getByRole('button', { name: 'Add bin', exact: true }).click();

    const row = page.getByRole('row').filter({ hasText: `${zone}-01-A` });
    await row.getByRole('button', { name: 'Deactivate' }).click();
    await expect(page.getByRole('status').filter({ hasText: 'was deactivated' })).toBeVisible();
    await expect(row).toContainText('Inactive');

    await row.getByRole('button', { name: 'Activate' }).click();
    await expect(row).toContainText('Active');
  });
});
