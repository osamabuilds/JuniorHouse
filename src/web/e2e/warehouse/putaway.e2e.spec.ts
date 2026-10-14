import { expect, test } from '@playwright/test';
import { API, apiCreateBin, createAcceptedStock, waitForPutawayItem, waitForWorkItem } from '../support';

/**
 * Sprint 6 (Warehouse), putaway: accepted stock is not available until it is shelved. A keyboard-style scanner types the barcode and
 * presses Enter; the bin is scanned or typed; a wrong barcode, an unknown bin and too many units are each refused in plain words; and an
 * item with no tag yet can be chosen from the list instead of scanned.
 */
test.describe.configure({ timeout: 180_000 }); // accepted stock goes through QC, Labelling and the event dispatcher first

test.describe('Warehouse: putaway', () => {
  test('scan the tag, give the bin and the quantity, and the stock becomes available (FR-SC-08)', async ({ page, request }) => {
    const stock = await createAcceptedStock(request, 40);
    const work = await waitForWorkItem(request, stock.run.po.id);
    await waitForPutawayItem(request, stock.run.po.id);
    const bin = await apiCreateBin(request);

    const before = await (await request.get(`${API}/api/warehouse/stock?search=${work.skuCode}`)).json();
    expect(before.items).toHaveLength(0); // accepted but not shelved: nothing is available yet

    await page.goto('/warehouse/putaway');
    await expect(page.getByRole('heading', { level: 1, name: 'Putaway' })).toBeVisible();
    const scanBox = page.getByLabel('Barcode (13 digits)');
    await expect(scanBox).toBeFocused();
    await scanBox.fill(work.gtin!);
    await scanBox.press('Enter');

    await expect(page.getByRole('heading', { name: new RegExp(`Put away ${work.skuCode}`) })).toBeVisible();
    await expect(page.getByText('The barcode was scanned.')).toBeVisible();
    await expect(page.getByLabel('How many')).toHaveValue('40');
    await page.getByLabel('Bin', { exact: true }).fill(bin.binCode.toLowerCase());
    await page.getByLabel('How many').fill('25');
    await page.getByLabel('Your name').fill('E2E Admin');
    await page.getByRole('button', { name: 'Put away', exact: true }).click();

    await expect(page.getByRole('status').filter({ hasText: `25 unit(s) of ${work.skuCode} put away in bin ${bin.binCode}` })).toBeVisible();
    await expect(page.getByRole('status').filter({ hasText: 'put away in bin' })).toContainText('15 still to do');
    await expect(page.getByLabel('Barcode (13 digits)')).toBeFocused(); // ready for the next tag

    const history = page.getByRole('row').filter({ hasText: bin.binCode });
    await expect(history).toContainText('Scanned');
    await expect(history).toContainText('E2E Admin');

    const after = await (await request.get(`${API}/api/warehouse/stock?search=${work.skuCode}`)).json();
    expect(after.items[0].onHand).toBe(25);
    expect(after.items[0].bins[0].binCode).toBe(bin.binCode);
  });

  test('on a slow 3G connection a scan and a putaway still complete: one small request each (PUT-18, NFR-SC-07)', async ({ page, request }) => {
    const stock = await createAcceptedStock(request, 8);
    const work = await waitForWorkItem(request, stock.run.po.id);
    await waitForPutawayItem(request, stock.run.po.id);
    const bin = await apiCreateBin(request);

    await page.goto('/warehouse/putaway');
    await expect(page.getByLabel('Barcode (13 digits)')).toBeFocused();
    const client = await page.context().newCDPSession(page);
    await client.send('Network.enable');
    await client.send('Network.emulateNetworkConditions', { offline: false, latency: 400, downloadThroughput: (400 * 1024) / 8, uploadThroughput: (400 * 1024) / 8 });

    const requests: { url: string; size: number }[] = [];
    page.on('response', async (response) => {
      if (response.url().includes('/api/warehouse/putaway/scan/') || response.url().includes('/confirm')) {
        requests.push({ url: response.url(), size: (await response.body()).length });
      }
    });

    await page.getByLabel('Barcode (13 digits)').fill(work.gtin!);
    await page.getByLabel('Barcode (13 digits)').press('Enter');
    await expect(page.getByRole('heading', { name: new RegExp(`Put away ${work.skuCode}`) })).toBeVisible({ timeout: 15_000 });
    await page.getByLabel('Bin', { exact: true }).fill(bin.binCode);
    await page.getByLabel('Your name').fill('E2E Admin');
    await page.getByRole('button', { name: 'Put away', exact: true }).click();
    await expect(page.getByRole('status').filter({ hasText: 'put away in bin' })).toBeVisible({ timeout: 20_000 });

    const scans = requests.filter((r) => r.url.includes('/scan/'));
    const confirms = requests.filter((r) => r.url.includes('/confirm'));
    expect(scans).toHaveLength(1); // one lookup per scan
    expect(confirms).toHaveLength(1); // one request per confirm
    expect(Math.max(...requests.map((r) => r.size))).toBeLessThan(2_000); // small payloads
  });

  test('a barcode nobody knows, and a wrong bin or too many units, are refused in plain words', async ({ page, request }) => {
    const stock = await createAcceptedStock(request, 10);
    const work = await waitForWorkItem(request, stock.run.po.id);
    await waitForPutawayItem(request, stock.run.po.id);
    const bin = await apiCreateBin(request);

    await page.goto('/warehouse/putaway');
    const scanBox = page.getByLabel('Barcode (13 digits)');
    await scanBox.fill('5999999999990');
    await scanBox.press('Enter');
    await expect(page.getByRole('alert').filter({ hasText: /not a valid GTIN-13|not known to the warehouse/ })).toBeVisible();

    await scanBox.fill(work.gtin!);
    await scanBox.press('Enter');
    await page.getByLabel('Bin', { exact: true }).fill('NO-9-X');
    await page.getByLabel('Your name').fill('E2E Admin');
    await page.getByRole('button', { name: 'Put away', exact: true }).click();
    await expect(page.getByRole('alert').filter({ hasText: 'does not exist' })).toBeVisible();

    await page.getByLabel('Bin', { exact: true }).fill(bin.binCode);
    await page.getByLabel('How many').fill('11');
    await page.getByRole('button', { name: 'Put away', exact: true }).click();
    await expect(page.getByRole('alert').filter({ hasText: "This can't be saved yet" })).toBeVisible();
  });

  test('an item with no tag yet is chosen from the list instead of scanned (PUT-19)', async ({ page, request }) => {
    const stock = await createAcceptedStock(request, 12);
    const work = await waitForWorkItem(request, stock.run.po.id);
    await waitForPutawayItem(request, stock.run.po.id);
    const bin = await apiCreateBin(request);

    await page.goto('/warehouse/putaway');
    await page.getByRole('searchbox', { name: 'Find an item waiting to be put away by SKU or barcode' }).fill(work.skuCode!);
    const row = page.getByRole('row').filter({ hasText: work.skuCode! });
    await row.getByRole('button', { name: 'Choose' }).click();

    await expect(page.getByText('Chosen from the list, because there is no tag to scan yet.')).toBeVisible();
    await page.getByLabel('Bin', { exact: true }).fill(bin.binCode);
    await page.getByLabel('Your name').fill('E2E Admin');
    await page.getByRole('button', { name: 'Put away', exact: true }).click();

    await expect(page.getByRole('status').filter({ hasText: '12 unit(s)' })).toBeVisible();
    await expect(page.getByRole('row').filter({ hasText: bin.binCode })).toContainText('Chosen from the list');
  });
});
