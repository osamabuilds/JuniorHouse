import { APIRequestContext, Page, expect } from '@playwright/test';

/** The API the admin app talks to (the docker stack's default). Tests set data up through it, then drive the UI. */
export const API = process.env['API_BASE_URL'] ?? 'http://localhost:8080';

export const uniqueSuffix = (): string => `${Date.now().toString().slice(-7)}${Math.floor(Math.random() * 90 + 10)}`;

/** A file that passes the API's content-signature check (a PDF header). */
export const PDF = { name: 'tech-pack.pdf', mimeType: 'application/pdf', buffer: Buffer.from('%PDF-1.4 e2e sample') };

/** Looks a REF lookup value up by its display name (e.g. sizes -> "1-2 Years"). */
async function lookupId(request: APIRequestContext, lookup: string, name: string): Promise<number> {
  const response = await request.get(`${API}/api/ref/${lookup}`);
  const items = (await response.json()) as { id: number; name: string }[];
  const match = items.find((item) => item.name === name);
  if (!match) {
    throw new Error(`No ${lookup} value named "${name}" - is the reference data seeded?`);
  }
  return match.id;
}

async function ok<T>(response: import('@playwright/test').APIResponse, what: string): Promise<T> {
  if (!response.ok()) {
    throw new Error(`${what} failed: ${response.status()} ${await response.text()}`);
  }
  return (await response.json()) as T;
}

export interface TestStyle {
  id: number;
  code: string;
  name: string;
  sizeId: number;
  colourId: number;
}

/** A style with two sizes and one colour, so an amendment has a second size to add. */
export async function createStyle(request: APIRequestContext, code = `E2E-${uniqueSuffix()}`): Promise<TestStyle> {
  const sizeId = await lookupId(request, 'sizes', '1-2 Years');
  const secondSizeId = await lookupId(request, 'sizes', '2-3 Years');
  const colourId = await lookupId(request, 'colours', 'White');
  const style = await ok<{ id: number }>(
    await request.post(`${API}/api/catalog/styles`, {
      data: {
        code,
        name: `E2E Dress ${code}`,
        collectionName: 'E2E',
        categoryId: await lookupId(request, 'categories', 'Dresses'),
        genderId: await lookupId(request, 'genders', 'Girls'),
        ageBracketId: await lookupId(request, 'age-brackets', 'Toddler (1-3Y)'),
        fabricId: await lookupId(request, 'fabrics', 'Cotton'),
        targetUnitCost: 400,
        targetRetailPrice: 1000,
        colourIds: [colourId],
        sizeIds: [sizeId, secondSizeId],
        targetLines: [
          { sizeId, colourId, targetQty: 50 },
          { sizeId: secondSizeId, colourId, targetQty: 50 },
        ],
      },
    }),
    'create style',
  );
  return { id: style.id, code, name: `E2E Dress ${code}`, sizeId, colourId };
}

export async function createVendor(request: APIRequestContext, name = `E2E Vendor ${uniqueSuffix()}`): Promise<{ id: number; name: string }> {
  const vendor = await ok<{ id: number }>(
    await request.post(`${API}/api/vendors`, {
      data: {
        name,
        contactName: 'E2E Contact',
        contactPhone: '+92-300-0000000',
        contactEmail: null,
        cityId: await lookupId(request, 'cities', 'Sialkot'),
        paymentTermId: (await (await request.get(`${API}/api/ref/payment-terms`)).json())[0].id,
        specialisationIds: [await lookupId(request, 'vendor-specialisations', 'Knits')],
      },
    }),
    'create vendor',
  );
  return { id: vendor.id, name };
}

export interface TestPo {
  id: number;
  poNo: string;
  styleCode: string;
  vendorName: string;
  sizeId: number;
  colourId: number;
}

export type PoStage = 'draft' | 'sent' | 'acknowledged';

/** Raises a PO through the API and walks it to `stage`. A Draft has valid commercial terms so it can be sent. */
export async function createPo(request: APIRequestContext, stage: PoStage = 'draft'): Promise<TestPo> {
  const style = await createStyle(request);
  const vendor = await createVendor(request);
  const created = await ok<{ id: number; poNo: string; paymentTermId: number; advancePercent: number }>(
    await request.post(`${API}/api/purchase-orders`, {
      data: {
        vendorId: vendor.id,
        styleId: style.id,
        unitCost: 420,
        expectedDeliveryDate: '2026-12-01',
        lines: [{ sizeId: style.sizeId, colourId: style.colourId, qty: 100 }],
      },
    }),
    'create PO',
  );
  await ok(
    await request.put(`${API}/api/purchase-orders/${created.id}`, {
      data: {
        unitCost: 420,
        expectedDeliveryDate: '2026-12-01',
        paymentTermId: created.paymentTermId,
        advancePercent: created.advancePercent,
        lines: [{ sizeId: style.sizeId, colourId: style.colourId, qty: 100 }],
        latestAcceptableDate: '2026-12-15',
        overTolerancePercent: 5,
        underTolerancePercent: 5,
        fabricResponsibilityId: 1,
      },
    }),
    'set PO terms',
  );

  if (stage !== 'draft') {
    await ok(await request.post(`${API}/api/purchase-orders/${created.id}/send?sendWithoutTechPack=true`, { data: {} }), 'send PO');
  }
  if (stage === 'acknowledged') {
    await ok(await request.post(`${API}/api/purchase-orders/${created.id}/acknowledge`, { data: {} }), 'acknowledge PO');
  }

  return { id: created.id, poNo: created.poNo, styleCode: style.code, vendorName: vendor.name, sizeId: style.sizeId, colourId: style.colourId };
}

/** Opens a PO's detail view from the Purchase Orders list. */
export async function openPo(page: Page, po: TestPo): Promise<void> {
  await page.goto('/purchase-orders');
  await page.getByRole('row').filter({ hasText: po.poNo }).getByRole('button', { name: 'View' }).click();
  await expect(page.getByRole('heading', { name: po.poNo })).toBeVisible();
}

/** The status badge next to the PO number. */
export const statusBadge = (page: Page) => page.locator('.po-detail__title .badge');

/** Opens the amendment form and fills the mandatory parts. */
export async function fillAmendment(page: Page, options: { unitCost?: string; note?: string; reason?: string } = {}): Promise<void> {
  await page.getByRole('button', { name: 'Amend', exact: true }).click();
  await page.locator('#amend-reason').selectOption({ label: options.reason ?? 'Vendor Cost Increase' });
  await page.locator('#amend-note').fill(options.note ?? 'E2E: agreed change');
  if (options.unitCost) {
    await page.locator('#amend-unit-cost').fill(options.unitCost);
  }
}
