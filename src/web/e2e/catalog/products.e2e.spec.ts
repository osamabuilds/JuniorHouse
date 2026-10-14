import { expect, test } from '@playwright/test';
import { API, createLabelledStyle, createProduct, fillProduct, openProduct, uniqueSuffix } from '../support';

/**
 * Sprint 7 (Catalog), products: create a product from a style, see why it cannot be published yet, then publish, take it off sale
 * and archive it. The product is a draft until nothing is missing (PRD-1, PRD-4, PRD-6).
 */
test.describe('Catalog: products', () => {
  test('creating a product from a labelled style adds a draft to the list and opens its editor', async ({ page, request }) => {
    const { styleCode } = await createLabelledStyle(request);
    const name = `E2E Created ${uniqueSuffix()}`;

    await page.goto('/catalog/products');
    await expect(page.getByRole('heading', { level: 1, name: 'Products' })).toBeVisible();
    await page.getByRole('button', { name: 'Create a product' }).click();
    const option = page.locator('#pc-style option', { hasText: styleCode });
    await page.locator('#pc-style').selectOption((await option.getAttribute('value'))!);
    await page.getByLabel('Product name').fill(name);
    await page.getByLabel('For', { exact: true }).selectOption({ label: 'Girls' });
    await page.getByLabel('Category').selectOption({ label: 'Dresses' });
    await page.getByLabel('Age group').selectOption({ label: 'Toddler (1-3Y)' });
    await page.getByLabel('Your name').fill('E2E Admin');
    await page.getByRole('button', { name: 'Create product' }).click();

    await page.getByRole('searchbox', { name: 'Search products by name or style code' }).fill(name);
    const row = page.getByRole('row').filter({ hasText: name });
    await expect(row).toContainText('Draft');
    await expect(row).toContainText(styleCode);
    await row.getByRole('link', { name }).click();
    await expect(page.getByRole('heading', { level: 1, name })).toBeVisible();
  });

  test('a product with nothing filled in is refused with every missing item, each a link to the tab that fixes it', async ({ page, request }) => {
    const product = await createProduct(request);
    await openProduct(page, product, 'Publish');

    await page.getByRole('button', { name: 'Publish', exact: true }).click();

    const alert = page.getByRole('alert').filter({ hasText: 'Not ready to publish' });
    await expect(alert).toContainText('Add at least one image.');
    await expect(alert).toContainText('Choose at least one care instruction.');
    await alert.getByRole('link', { name: 'Go to Images' }).click();
    await expect(page.getByRole('tab', { name: 'Images', exact: true })).toHaveAttribute('aria-selected', 'true');
  });

  test('a complete product is published, taken off sale and archived, and an archived one is read-only', async ({ page, request }) => {
    const product = await createProduct(request);
    await fillProduct(request, product);
    await openProduct(page, product, 'Publish');

    await expect(page.getByRole('status').filter({ hasText: 'Everything needed is in place' })).toBeVisible();
    await page.getByRole('button', { name: 'Publish', exact: true }).click();
    await expect(page.locator('.badge').first()).toHaveText('Published');
    await expect(page.getByText('Live at')).toBeVisible();

    await page.getByRole('button', { name: 'Take off sale' }).click();
    await expect(page.getByRole('status').filter({ hasText: 'The product is off sale. Nothing was deleted.' })).toBeVisible();
    await expect(page.locator('.badge').first()).toHaveText('Unpublished');

    await page.getByRole('button', { name: 'Archive', exact: true }).click();
    await page.getByRole('button', { name: 'Yes, archive it for good' }).click();
    await expect(page.getByRole('status').filter({ hasText: 'The product was archived.' })).toBeVisible();
    await expect(page.getByRole('status').filter({ hasText: 'This product is archived, so it is read-only.' })).toBeVisible();
    await page.getByRole('tab', { name: 'Content', exact: true }).click();
    await expect(page.getByRole('button', { name: 'Save content' })).toBeDisabled();
  });

  test('saving content from a stale copy is refused and the editor offers to reload', async ({ page, request }) => {
    const product = await createProduct(request);
    await openProduct(page, product, 'Content');

    // Someone else saves first (through the API), so this page's version token is out of date.
    const current = (await (await request.get(`${API}/api/catalog/products/${product.id}`)).json()) as Record<string, unknown>;
    await request.put(`${API}/api/catalog/products/${product.id}`, {
      data: {
        name: `${product.name} (other)`,
        description: 'Changed elsewhere',
        genderId: current['genderId'],
        categoryId: current['categoryId'],
        ageBracketId: current['ageBracketId'],
        careNote: null,
        version: current['version'],
        actingPerson: 'Someone Else',
      },
    });

    await page.getByLabel('Product name').fill(`${product.name} (mine)`);
    await page.getByRole('button', { name: 'Save content' }).click();

    const alert = page.getByRole('alert').filter({ has: page.getByRole('button', { name: 'Reload the product' }) });
    await expect(alert).toBeVisible();
    await alert.getByRole('button', { name: 'Reload the product' }).click();
    await expect(page.getByRole('heading', { level: 1, name: `${product.name} (other)` })).toBeVisible();
  });
});
