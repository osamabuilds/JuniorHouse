import { Page, expect, test } from '@playwright/test';
import {
  API, PERSON, apiCreateBin, apiDeliver, apiInspect, apiQueueTags, apiReceive, apiSafety, apiSplit, createPo,
  apiStartCutting, apiSubmitAndApproveSample, fillProduct, openBatch, openPoById, poll, uniqueSuffix, waitForPoStatus,
  waitForPutawayItem, waitForRun, waitForWorkItem, CatalogProduct, TestRun,
} from './support';

/** Live demo: one product from purchase order to a buyer's screen. Run headed: `npx playwright test e2e/zz-live-demo --headed`. */
const STORE = 'http://localhost:4000';
const PAUSE = Number(process.env['DEMO_PAUSE'] ?? 3500);

test.use({ viewport: { width: 1366, height: 820 }, launchOptions: { slowMo: 350 } });
test.describe.configure({ timeout: 900_000 });

/** A caption pinned to the top of the page so a viewer knows which stage this is. */
async function say(page: Page, step: string, text: string): Promise<void> {
  console.log(`\n[${step}] ${text}`);
  await page.evaluate(
    ([s, t]) => {
      document.getElementById('demo-caption')?.remove();
      const el = document.createElement('div');
      el.id = 'demo-caption';
      el.setAttribute('style', 'position:fixed;z-index:99999;left:0;right:0;top:0;padding:10px 16px;background:#1f2a24;color:#fff;font:600 15px system-ui;box-shadow:0 2px 8px #0006');
      el.textContent = `${s}  ·  ${t}`;
      document.body.appendChild(el);
      document.body.style.paddingTop = '44px';
    },
    [step, text],
  );
  await page.waitForTimeout(PAUSE);
}

test('from purchase order to the storefront', async ({ page, request }) => {
  // 1. Vendor and purchase order
  const po = await createPo(request, 'draft');
  await openPoById(page, po);
  await say(page, '1 / 8  Purchasing', `Draft purchase order ${po.poNo} to vendor "${po.vendorName}": 100 pieces at PKR 420, terms and tolerances set.`);
  await request.post(`${API}/api/purchase-orders/${po.id}/send?sendWithoutTechPack=true`, { data: {} });
  await request.post(`${API}/api/purchase-orders/${po.id}/acknowledge`, { data: {} });
  await page.reload();
  await expect(page.getByRole('heading', { name: po.poNo })).toBeVisible();
  await say(page, '1 / 8  Purchasing', 'Sent to the vendor and acknowledged. The PO status changed; this also opened a production run (an event, not a shared table).');

  // 2. Production tracking
  const run: TestRun = { po, runId: await waitForRun(request, po.id) };
  await apiSubmitAndApproveSample(request, run.runId);
  await apiStartCutting(request, run.runId);
  await waitForPoStatus(request, po.id, 5);
  await apiDeliver(request, run, `DN-DEMO-${uniqueSuffix()}`, 100, true);
  await page.goto(`/production/${run.runId}`);
  await expect(page.getByRole('heading', { level: 1, name: `Production: ${po.poNo}` })).toBeVisible();
  await say(page, '2 / 8  Production', 'Pre-production sample approved, bulk cutting started (PO moved to In Production), and the vendor dispatched one final delivery note.');

  // 3. Quality control
  const receivable = await createReceivableFor(request, run);
  const batch = await apiReceive(request, receivable, 100);
  await page.goto('/quality');
  await say(page, '3 / 8  Quality control', 'QC received the delivery note automatically. The warehouse team now records the goods receipt, then inspects.');
  const inspectionId = await apiInspect(request, batch);
  await apiSafety(request, inspectionId);
  await apiSplit(request, batch, 60, 3);
  await openBatch(page, batch);
  await say(page, '3 / 8  Quality control', 'Six quality checks and both child-safety checks pass. The batch is split: 60 accepted, 40 rejected. Only accepted pieces move on.');

  // 4. Labelling and barcodes
  const work = await waitForWorkItem(request, po.id);
  await apiQueueTags(request, work.id, 60);
  await page.goto('/labelling/worklist');
  await say(page, '4 / 8  Labelling', `Accepted stock reached Labelling by event. Each variant got a SKU (${work.skuCode}) and a 13-digit GTIN (${work.gtin}). 60 tags queued to print.`);

  // 5. Warehouse putaway
  await waitForPutawayItem(request, po.id);
  const bin = await apiCreateBin(request);
  await page.goto('/warehouse/putaway');
  await expect(page.getByRole('heading', { level: 1, name: 'Putaway' })).toBeVisible();
  const scan = page.getByLabel('Barcode (13 digits)');
  await scan.fill(work.gtin!);
  await scan.press('Enter');
  await page.getByLabel('Bin', { exact: true }).fill(bin.binCode.toLowerCase());
  await page.getByLabel('How many').fill('60');
  await page.getByLabel('Your name').fill('Demo Picker');
  await say(page, '5 / 8  Warehouse', `Scanning the tag and the shelf bin ${bin.binCode}. Until it is shelved the stock is not available to sell.`);
  await page.getByRole('button', { name: 'Put away', exact: true }).click();
  await expect(page.getByRole('status').filter({ hasText: 'put away in bin' })).toBeVisible();
  await say(page, '5 / 8  Warehouse', '60 units shelved, recorded in an append-only stock ledger. Now they are sellable.');

  // 6. Catalog: product from the style
  const styleCode = po.styleCode;
  const styles = (await (await request.get(`${API}/api/catalog/styles?search=${encodeURIComponent(styleCode)}&pageSize=100`)).json()) as { items: { id: number; code: string }[] };
  const styleId = styles.items.find((s) => s.code === styleCode)!.id;
  const lookup = async (kind: string, name: string) => ((await (await request.get(`${API}/api/ref/${kind}`)).json()) as { id: number; name: string }[]).find((x) => x.name === name)!.id;
  const name = `Demo Floral Dress ${uniqueSuffix()}`;
  const created = (await (await request.post(`${API}/api/catalog/products`, {
    data: {
      styleId, name, description: 'A soft cotton dress with a floral print, made in Sialkot.', actingPerson: PERSON,
      genderId: await lookup('genders', 'Girls'), categoryId: await lookup('categories', 'Dresses'), ageBracketId: await lookup('age-brackets', 'Toddler (1-3Y)'),
    },
  })).json()) as { id: number; path: string; version: number };
  const product: CatalogProduct = { id: created.id, name, styleCode, path: created.path, version: created.version };
  await page.goto(`/catalog/products/${product.id}`);
  await page.getByRole('textbox', { name: 'Your name' }).fill('Demo Merchandiser');
  await say(page, '6 / 8  Catalogue', `A draft product "${name}" created from the labelled style. Try publishing it now: it will be refused.`);
  await page.getByRole('tab', { name: 'Publish', exact: true }).click();
  await page.getByRole('button', { name: 'Publish', exact: true }).click();
  await say(page, '6 / 8  Catalogue', 'Refused with every missing item listed (no price, picture, composition or care), each a link to the tab that fixes it.');
  await fillProduct(request, product);
  await page.reload();
  await page.getByRole('textbox', { name: 'Your name' }).fill('Demo Merchandiser');
  await page.getByRole('tab', { name: 'Publish', exact: true }).click();
  await say(page, '6 / 8  Catalogue', 'Prices, picture with alt text, 100% composition and care added. Everything needed is now in place.');

  // 7. Publish
  await page.getByRole('button', { name: 'Publish', exact: true }).click();
  await expect(page.getByText('Live at')).toBeVisible();
  await say(page, '7 / 8  Publish', `Published. The product is live at ${product.path}. Publishing wrote an event that updates the storefront read model.`);

  // 8. Storefront
  await poll(async () => ((await request.get(`${STORE}${product.path}`)).status() === 200 ? true : null), 'the product page to go live', 90_000);
  await page.goto(`${STORE}/`);
  await say(page, '8 / 8  Storefront', 'The public shop (server-rendered Angular). Same data, now through a separate read model.');
  await page.goto(`${STORE}${product.path.replace(/\/[^/]+$/, '')}`);
  await say(page, '8 / 8  Storefront', 'The category listing shows the new product: filters, sort and pagination are real links.');
  await page.goto(`${STORE}${product.path}`);
  await expect(page.locator('h1')).toHaveText(name);
  await say(page, '8 / 8  Storefront', 'The product page: gallery, price from the server, colour and size as links, stock-aware availability, size chart, composition and care.');
  const size = page.locator('rp-size-options a').first();
  if (await size.count()) {
    await size.click();
    await say(page, '8 / 8  Storefront', 'Picking a size updates the URL and the availability line. Sold-out sizes are text, not just a colour.');
  }
  await say(page, 'Done', 'Purchase order → production → QC → barcodes → shelf → catalogue → live product page, all through events between modules.');
});

/** The QC inbound delivery for a run that already has its delivery note. */
async function createReceivableFor(request: import('@playwright/test').APIRequestContext, run: TestRun) {
  const deliveryId = await poll(async () => {
    const page = (await (await request.get(`${API}/api/quality/inbound-deliveries?search=${run.po.poNo}&pageSize=100`)).json()) as { items: { id: number; vendorDnNo: string }[] };
    return page.items[0]?.id ?? null;
  }, 'the inbound delivery', 60_000);
  const dn = (await (await request.get(`${API}/api/quality/inbound-deliveries?search=${run.po.poNo}&pageSize=100`)).json()) as { items: { vendorDnNo: string }[] };
  return { run, dnNo: dn.items[0].vendorDnNo, deliveryId };
}

