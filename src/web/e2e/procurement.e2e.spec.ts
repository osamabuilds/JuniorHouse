import { expect, test } from '@playwright/test';

/**
 * SCRUM-175: the full Sprint 1 procurement flow through the admin UI - create a style, create a
 * vendor, raise a PO against them, send it, acknowledge it, and confirm the status timeline shows
 * all three transitions. Run against `docker compose up` (README "Try Sprint 1"); not run as part
 * of `ng test` - a separate `npm run e2e` step, since it needs the real API + Postgres, not a
 * mocked HttpClient.
 */
test('Staff creates a style, a vendor, and a PO, and walks it through to Acknowledged', async ({ page }) => {
  const unique = Date.now().toString().slice(-8);
  const styleCode = `E2E-STY-${unique}`;
  const vendorName = `E2E Vendor ${unique}`;

  // --- Style ---
  await page.goto('/styles');
  await page.getByRole('button', { name: 'Add style' }).click();

  await page.getByLabel('Code').fill(styleCode);
  await page.getByLabel('Name').fill('E2E Test Dress');
  await page.getByLabel('Category').selectOption({ label: 'Dresses' });
  await page.getByLabel('Gender').selectOption({ label: 'Girls' });
  await page.getByLabel('Age bracket').selectOption({ label: 'Toddler (1-3Y)' });
  await page.getByLabel('Fabric').selectOption({ label: 'Cotton' });
  await page.getByLabel('Target unit cost (PKR)').fill('400');
  await page.getByLabel('Target retail price (PKR)').fill('1000');

  await page.getByRole('group', { name: 'Colourways' }).getByLabel('White').check();
  await page.getByRole('group', { name: 'Size run' }).getByLabel('1-2Y').check();

  await page.getByLabel('1-2Y × White target quantity').fill('20');
  await page.getByRole('button', { name: 'Save' }).click();

  await expect(page.getByRole('cell', { name: styleCode })).toBeVisible();

  // --- Vendor ---
  await page.goto('/vendors');
  await page.getByRole('button', { name: 'Add vendor' }).click();

  await page.getByLabel('Name').fill(vendorName);
  await page.getByLabel('Contact name').fill('E2E Contact');
  await page.getByLabel('Contact phone').fill('+92-300-0000000');
  await page.getByLabel('City').selectOption({ label: 'Sialkot' });
  await page.getByLabel('Default payment term').selectOption({ index: 1 });
  await page.getByRole('group', { name: 'Specialisations' }).getByLabel('Knits').check();
  await page.getByRole('button', { name: 'Save' }).click();

  await expect(page.getByRole('cell', { name: vendorName })).toBeVisible();

  // --- Purchase order ---
  await page.goto('/purchase-orders');
  await page.getByRole('button', { name: 'Raise PO' }).click();

  await page.getByLabel('Vendor').selectOption({ label: vendorName });
  await page.getByLabel('Style').selectOption({ label: new RegExp(styleCode) });
  await page.getByLabel('Unit cost (PKR)').fill('420');
  await page.getByLabel('Expected delivery date').fill('2026-12-01');

  // The size x colour grid only appears once the style's size run/colourways have loaded.
  const qtyInputs = page.locator('.line-grid input[type="number"]');
  await expect(qtyInputs.first()).toBeVisible();
  await qtyInputs.first().fill('15');

  await page.getByRole('button', { name: 'Save' }).click();

  // Now on the detail view (AC-7: created in Draft with a generated PO number).
  await expect(page.getByRole('heading', { name: /^PO-\d{4}-\d{5}$/ })).toBeVisible();
  await expect(page.getByText('Draft', { exact: true })).toBeVisible();

  await page.getByRole('button', { name: 'Send to Vendor' }).click();
  await expect(page.getByText('Sent to Vendor', { exact: true })).toBeVisible();

  await page.getByRole('button', { name: 'Acknowledge' }).click();
  await expect(page.getByText('Acknowledged', { exact: true })).toBeVisible();

  // AC-14: the timeline shows all three transitions, oldest first.
  const timelineEntries = page.locator('.timeline li .timeline__status');
  await expect(timelineEntries).toHaveText(['Draft', 'Sent to Vendor', 'Acknowledged']);
});
