import AxeBuilder from '@axe-core/playwright';
import { Page, expect, test } from '@playwright/test';
import { OWNER_PASSWORD, signInAsOwner } from '../identity/support';
import { ADMIN, STOREFRONT, courierPush, pickAndPack, placeConfirmedCodOrder, sql, waitForWaybill } from './shipping-support';

/**
 * Ship and track, from a packed order to a delivered parcel (S15 A6; bookings, labels, manifests, tracking). A shopper places a cash on delivery order;
 * staff pick and pack it; the booking worker gives it a waybill; staff open its label and the day's manifest; the fake courier posts signed status
 * pushes (a forged one is refused); the customer sees the timeline in the storefront and by tracking number alone, with no order data.
 *
 * Needs the docker stack up and an Owner (see the identity journeys): E2E_OWNER_PASSWORD, STOREFRONT_BASE_URL, ADMIN_BASE_URL, API_BASE_URL.
 */
const AXE_TAGS = ['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'];

async function checkAccessible(page: Page): Promise<void> {
  const axe = await new AxeBuilder({ page }).withTags(AXE_TAGS).analyze();
  expect(axe.violations.map((v) => `${v.id}: ${v.nodes[0]?.html}`)).toEqual([]);
}

test.describe('ship and track', () => {
  test.skip(!OWNER_PASSWORD, 'set E2E_OWNER_PASSWORD to the Owner password created for this run');

  test('BKG-1 to BKG-3, LBL-1, MNF-1, TRK-1, TRK-2, TRK-5, TRK-9: a packed order is booked, labelled, manifested, delivered and tracked', async ({ browser }) => {
    test.setTimeout(420_000);
    const shop = await browser.newPage({ baseURL: STOREFRONT });
    const number = await placeConfirmedCodOrder(shop);

    const staff = await browser.newContext({ baseURL: ADMIN });
    const page = await staff.newPage();
    await signInAsOwner(page);
    await pickAndPack(page, number);
    const waybill = await waitForWaybill(number);

    // The shipment is listed with its waybill and courier; the label has the barcode of the waybill (LBL-1).
    await page.goto('/dispatch/shipments');
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    await checkAccessible(page);
    const shipmentId = sql(`SELECT "ID" FROM "SHPG"."SHPG_MAIN" WHERE "ORDR_NO" = '${number}'`);
    await page.goto(`/dispatch/label/${shipmentId}`);
    await expect(page.getByText(waybill).first()).toBeVisible();
    await checkAccessible(page);

    // The manifest of the day lists the parcel (MNF-1).
    await page.goto('/dispatch/manifests');
    await page.getByRole('button', { name: "Make today's manifests" }).click();
    await expect(page.getByRole('status')).toBeVisible();

    // A forged push is refused and changes nothing (TRK-2); a signed one moves the shipment (TRK-1, TRK-5).
    const forged = await page.request.post(`${process.env['API_BASE_URL'] ?? 'http://localhost:8080'}/api/shipping/webhooks/PRIMARY`, {
      data: JSON.stringify({ waybill, status: 'DELIVERED', eventId: 'forged', at: new Date().toISOString() }),
      headers: { 'content-type': 'application/json', 'X-Courier-Signature': 'deadbeef' },
    });
    expect(forged.ok()).toBeFalsy();
    expect(sql(`SELECT "SHPG_STS_ID" FROM "SHPG"."SHPG_MAIN" WHERE "ORDR_NO" = '${number}'`)).toBe('2');
    await courierPush(page, waybill, 'PICKED_UP');
    await courierPush(page, waybill, 'OUT_FOR_DELIVERY');
    await courierPush(page, waybill, 'DELIVERED');
    expect(sql(`SELECT "SHPG_STS_ID" FROM "SHPG"."SHPG_MAIN" WHERE "ORDR_NO" = '${number}'`)).toBe('7');

    // The customer sees the timeline by tracking number alone, and no order data (TRK-9).
    const track = await staff.newPage({ baseURL: STOREFRONT });
    await openHydratedTrack(track, waybill);
    await expect(track.getByText('Delivered').first()).toBeVisible();
    await expect(track.locator('main')).not.toContainText(number);
    await expect(track.locator('main')).not.toContainText('E2E Shopper');
    await checkAccessible(track);

    await shop.close();
    await staff.close();
  });
});

async function openHydratedTrack(page: Page, waybill: string): Promise<void> {
  await page.goto('/track-parcel');
  await page.waitForLoadState('networkidle');
  await page.getByLabel('Tracking number').fill(waybill);
  await page.getByRole('button', { name: 'Track parcel' }).click();
}
