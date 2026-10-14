import { expect, test } from '@playwright/test';
import { createProduct, openProduct } from '../support';

/** Sprint 7 (Catalog), composition and care: fibre shares must add up to 100% (CMP-1..9). */
test.describe('Catalog: composition and care', () => {
  test('a partial total is saved as a draft and reported as missing, the full total is saved, and care instructions are saved', async ({ page, request }) => {
    const product = await createProduct(request);
    await openProduct(page, product, 'Composition and care');

    await page.getByRole('button', { name: 'Add a fibre' }).click();
    await page.getByRole('combobox', { name: 'Fibre 1' }).selectOption({ index: 1 });
    await page.getByLabel('Share of fibre 1 in percent').fill('90');
    await page.getByRole('button', { name: 'Save composition' }).click();
    await expect(page.getByRole('status').filter({ hasText: 'Total 90%: 10% missing' })).toBeVisible();
    await expect(page.getByRole('status').filter({ hasText: 'Composition saved.' })).toBeVisible();

    await page.getByRole('button', { name: 'Add a fibre' }).click();
    await page.getByRole('combobox', { name: 'Fibre 2' }).selectOption({ index: 2 });
    await page.getByLabel('Share of fibre 2 in percent').fill('10');
    await expect(page.getByRole('status').filter({ hasText: 'Total 100%' })).toBeVisible();
    await page.getByRole('button', { name: 'Save composition' }).click();
    await expect(page.getByRole('status').filter({ hasText: 'Composition saved.' })).toBeVisible();

    await page.getByRole('group', { name: 'Care instructions' }).getByRole('checkbox').first().check();
    await page.getByRole('button', { name: 'Save care' }).click();
    await expect(page.getByRole('status').filter({ hasText: 'Care instructions saved.' })).toBeVisible();

    await page.reload();
    await page.getByRole('tab', { name: 'Composition and care', exact: true }).click();
    await expect(page.getByLabel('Share of fibre 1 in percent')).toHaveValue('90');
    await expect(page.getByLabel('Share of fibre 2 in percent')).toHaveValue('10');
    await expect(page.getByRole('group', { name: 'Care instructions' }).getByRole('checkbox').first()).toBeChecked();
  });
});
