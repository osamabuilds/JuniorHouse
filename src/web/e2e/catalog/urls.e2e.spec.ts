import { expect, test } from '@playwright/test';
import { API, createProduct, fillProduct, openProduct, publishProduct, uniqueSuffix } from '../support';

/** Sprint 7 (Catalog), URLs: a clean address from the name, a rename keeps the old address working, reserved names are refused (URL-1..11). */
test.describe('Catalog: product URLs', () => {
  test('renaming the address keeps the old one in the history and resolving it points to the new one', async ({ page, request }) => {
    const product = await createProduct(request);
    await fillProduct(request, product);
    await publishProduct(request, product); // addresses are kept only once a product has been on sale
    await openProduct(page, product, 'URL');
    const slug = `e2e-${uniqueSuffix()}`;

    await page.getByLabel('URL name').fill(slug);
    await page.getByRole('button', { name: 'Change address' }).click();

    await expect(page.getByRole('status').filter({ hasText: `The address is now /girls/dresses/${slug}.` })).toBeVisible();
    await expect(page.getByRole('region', { name: 'Every address this product has had' })).toContainText(product.path);

    const resolved = (await (await request.get(`${API}/api/catalog/paths/resolve?path=${encodeURIComponent(product.path)}`)).json()) as { currentPath: string };
    expect(resolved.currentPath, 'the old address points to the new one').toBe(`/girls/dresses/${slug}`);
  });

  test('a reserved name such as checkout is refused', async ({ page, request }) => {
    const product = await createProduct(request);
    await openProduct(page, product, 'URL');

    await page.getByLabel('URL name').fill('checkout');
    await page.getByRole('button', { name: 'Change address' }).click();

    await expect(page.getByRole('alert').first()).toBeVisible();
  });

  test('the suggestion button proposes a name from the product name', async ({ page, request }) => {
    const product = await createProduct(request, `Floral Summer Dress ${uniqueSuffix()}`);
    await openProduct(page, product, 'URL');

    await page.getByRole('button', { name: 'Suggest from the name' }).click();

    await expect(page.getByRole('status').filter({ hasText: 'Suggested:' })).toContainText('floral-summer-dress');
  });
});
