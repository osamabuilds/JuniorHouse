import { execSync } from 'node:child_process';
import { expect, test } from '@playwright/test';

/**
 * SCRUM-175 + SCRUM-93: the full procurement flow through the admin UI - create a style, create a
 * vendor, raise a PO against them, send it (confirming there is no tech pack), record the vendor's
 * confirmation, then amend it (Sprint 2), accept the amendment and see the new terms in force,
 * including on the vendor-facing view. Run against `docker compose up` (README "Try Sprint 1/2");
 * not run as part of `ng test` - a separate `npm run e2e` step, since it needs the real API +
 * Postgres, not a mocked HttpClient.
 *
 * Optional outbox check: with E2E_CHECK_OUTBOX=1 the test also asks Postgres (through
 * `docker compose exec`) whether the background dispatcher has moved the revision's event to
 * Processed - there is no HTTP surface for that (tasks.md task 15).
 */
test('Staff creates a PO, sends and confirms it, then amends it and the new terms take effect', async ({ page }) => {
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
  await page.getByRole('group', { name: 'Size run' }).getByLabel('1-2 Years').check();

  await page.getByLabel('1-2 Years × White target quantity').fill('20');
  await page.getByRole('button', { name: 'Save' }).click();

  // The list is paged, so find the new style through the search box.
  await page.getByPlaceholder('Search by code or name').fill(styleCode);
  await expect(page.getByRole('cell', { name: styleCode })).toBeVisible();

  // --- Vendor ---
  await page.goto('/vendors');
  await page.getByRole('button', { name: 'Add vendor' }).click();

  await page.getByLabel('Name', { exact: true }).fill(vendorName);
  await page.getByLabel('Contact name').fill('E2E Contact');
  await page.getByLabel('Contact phone').fill('+92-300-0000000');
  await page.getByLabel('City').selectOption({ label: 'Sialkot' });
  await page.getByLabel('Default payment term').selectOption({ index: 1 });
  await page.getByRole('group', { name: 'Specialisations' }).getByLabel('Knits').check();
  await page.getByRole('button', { name: 'Save' }).click();

  await page.getByPlaceholder('Search by name').fill(vendorName);
  await expect(page.getByRole('cell', { name: vendorName })).toBeVisible();

  // --- Purchase order ---
  await page.goto('/purchase-orders');
  await page.getByRole('button', { name: 'Raise PO' }).click();

  const poForm = page.locator('form.po-form');
  await poForm.getByLabel('Vendor').selectOption({ label: vendorName });
  await poForm.getByLabel('Style').selectOption({ label: `${styleCode} — E2E Test Dress` });
  await poForm.getByLabel('Unit cost (PKR)').fill('420');
  await poForm.getByLabel('Expected delivery date').fill('2026-12-01');
  // Required before a PO can be sent (SCRUM-93 AC-6).
  await poForm.getByLabel('Latest acceptable delivery date').fill('2026-12-15');
  await poForm.getByLabel('Who supplies the fabric').selectOption({ label: 'Vendor Supplied' });

  // The size x colour grid only appears once the style's size run/colourways have loaded.
  const qtyInputs = page.locator('.line-grid input[type="number"]');
  await expect(qtyInputs.first()).toBeVisible();
  await qtyInputs.first().fill('15');

  await page.getByRole('button', { name: 'Save' }).click();

  // Now on the detail view (AC-7: created in Draft with a generated PO number).
  const statusBadge = page.locator('.po-detail__header .badge');
  await expect(page.getByRole('heading', { name: /^PO-\d{4}-\d{5}$/ })).toBeVisible();
  await expect(statusBadge).toHaveText('Draft');
  await expect(page.locator('.po-detail__facts')).toContainText('Latest acceptable delivery');

  // --- Send: no tech pack attached, so an explicit "send anyway" is needed (AC-39) ---
  await page.getByRole('button', { name: 'Send to Vendor' }).click();
  const sendDialog = page.getByRole('alertdialog');
  await expect(sendDialog).toContainText('No Tech Pack Spec is attached');
  await expect(sendDialog.getByRole('button', { name: 'Send to Vendor' })).toBeDisabled();
  await sendDialog.getByLabel('Send anyway, without a tech pack').check();
  await sendDialog.getByRole('button', { name: 'Send to Vendor' }).click();
  await expect(statusBadge).toHaveText('Sent to Vendor');

  // --- Vendor confirms over WhatsApp (replaces the bare Acknowledge, AC-4/AC-25) ---
  await page.getByRole('button', { name: 'Record vendor response' }).click();
  await page.locator('#vresp-channel').selectOption({ label: 'WhatsApp' });
  await page.locator('#vresp-responder').fill('E2E Contact');
  await page.locator('#vresp-evidence').setInputFiles({
    name: 'whatsapp-confirmation.png',
    mimeType: 'image/png',
    buffer: Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0x00]),
  });
  await expect(page.getByText('whatsapp-confirmation.png')).toBeVisible();
  await page.getByRole('button', { name: 'Record response' }).click();
  await expect(statusBadge).toHaveText('Acknowledged');

  // AC-14: the timeline shows all three transitions, oldest first.
  const timelineEntries = page.locator('.timeline li .timeline__status');
  await expect(timelineEntries).toHaveText(['Draft', 'Sent to Vendor', 'Acknowledged']);

  // --- Amend the acknowledged PO: it waits as Pending until decided (AC-10) ---
  await page.getByRole('button', { name: 'Amend', exact: true }).click();
  await page.locator('#amend-reason').selectOption({ label: 'Vendor Cost Increase' });
  await page.locator('#amend-note').fill('E2E: fabric price rose');
  await page.locator('#amend-unit-cost').fill('480');
  await page.getByRole('button', { name: 'Submit amendment' }).click();

  await expect(page.getByRole('heading', { name: 'Revision 1 awaits a decision' })).toBeVisible();
  const history = page.getByRole('region', { name: 'Revision history' });
  await expect(history.getByRole('article', { name: 'Revision 1' })).toContainText('Pending');
  await expect(page.locator('.po-detail__facts')).toContainText('PKR 420'); // unchanged until accepted

  // --- Accept: Rev 1 goes In force and the PO's terms update (AC-11) ---
  await page.getByRole('button', { name: /^Accept/ }).click();
  await expect(page.getByRole('heading', { name: 'Revision 1 awaits a decision' })).toBeHidden();
  await expect(history.getByRole('article', { name: 'Revision 1' })).toContainText('In force');
  await expect(history.getByRole('article', { name: 'Revision 0' })).toContainText('Superseded');
  await expect(history.getByRole('article', { name: 'Revision 0' })).toContainText('Evidence: whatsapp-confirmation.png');
  await expect(page.locator('.po-detail__facts')).toContainText('PKR 480');

  // --- The vendor-facing view shows the new terms with no admin navigation (AC-35) ---
  const vendorViewPath = await page.getByRole('link', { name: 'Vendor view' }).getAttribute('href');
  await page.goto(vendorViewPath!);
  await expect(page.getByRole('heading', { level: 1 })).toContainText('Purchase order PO-');
  await expect(page.getByText('PKR 480')).toBeVisible();
  await expect(page.locator('nav')).toHaveCount(0);

  // --- Optional: the dispatcher has processed the revision's event ---
  if (process.env['E2E_CHECK_OUTBOX'] === '1') {
    await expect
      .poll(
        () =>
          execSync(
            `docker compose exec -T postgres psql -U ${process.env['POSTGRES_USER'] ?? 'romp'} -d ${process.env['POSTGRES_DB'] ?? 'romp'} -tA -c "SELECT count(*) FROM \\"VNDR\\".\\"OUTB_MSG\\" WHERE \\"EVNT_TYP\\" = 'PoRevisionPutInForceEvent' AND \\"PROC_DTE\\" IS NOT NULL"`,
          )
            .toString()
            .trim(),
        { timeout: 30_000 },
      )
      .not.toBe('0');
  }
});
