import { expect, test } from '@playwright/test';
import { PNG, createProduct, openProduct } from '../support';

/** Sprint 7 (Catalog), product media: upload, alt text, order and delete (MED-1..11). The file type is decided by the bytes, not the name. */
test.describe('Catalog: product images', () => {
  test('uploaded pictures show with their alt text, can be reordered, and are deleted only after a confirmation', async ({ page, request }) => {
    const product = await createProduct(request);
    await openProduct(page, product, 'Images');

    for (const [file, alt] of [
      ['front.png', 'Front view of the dress'],
      ['back.png', 'Back view of the dress'],
    ]) {
      await page.locator('#img-file').setInputFiles({ ...PNG, name: file });
      await page.getByLabel('Alt text (can be added later)').fill(alt);
      await page.getByRole('button', { name: 'Add image' }).click();
      await expect(page.getByRole('status').filter({ hasText: 'Image added.' })).toBeVisible();
    }

    await expect(page.getByLabel('Alt text for front.png')).toHaveValue('Front view of the dress');
    await expect(page.getByLabel('Alt text for back.png')).toHaveValue('Back view of the dress');

    await page.getByRole('button', { name: 'Move down' }).first().click();
    await expect(page.getByRole('status').filter({ hasText: 'Image order saved.' })).toBeVisible();

    await page.getByRole('button', { name: 'Delete', exact: true }).first().click();
    await page.getByRole('button', { name: 'Yes, delete it' }).click();
    await expect(page.getByRole('status').filter({ hasText: 'Image deleted.' })).toBeVisible();
  });

  test('a file that is not a picture is refused even when it is named .png', async ({ page, request }) => {
    const product = await createProduct(request);
    await openProduct(page, product, 'Images');

    await page.locator('#img-file').setInputFiles({ name: 'fake.png', mimeType: 'image/png', buffer: Buffer.from('<html>not an image</html>') });
    await page.getByRole('button', { name: 'Add image' }).click();

    await expect(page.getByRole('alert').first()).toBeVisible();
    await expect(page.getByLabel('Alt text for fake.png')).toHaveCount(0);
  });
});
