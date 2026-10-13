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
export async function createPo(
  request: APIRequestContext,
  stage: PoStage = 'draft',
  options: { fabricResponsibilityId?: number } = {},
): Promise<TestPo> {
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
        fabricResponsibilityId: options.fabricResponsibilityId ?? 1,
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
  // The list is paged, so narrow it to the PO's vendor first.
  await page.locator('.toolbar select').first().selectOption({ label: po.vendorName });
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

// ---------------------------------------------------------------------------------------------
// Sprint 3 (production tracking)
// ---------------------------------------------------------------------------------------------

/** Today in Pakistan time (UTC+5): the day the API treats as "today" for milestone and delivery dates. */
export const today = (offsetDays = 0): string => new Date(Date.now() + 5 * 3600 * 1000 + offsetDays * 86400 * 1000).toISOString().slice(0, 10);

/** Repeats `check` until it returns a value or the time runs out. The outbox dispatcher delivers events every few seconds. */
export async function poll<T>(check: () => Promise<T | null>, what: string, timeoutMs = 45_000): Promise<T> {
  const started = Date.now();
  for (;;) {
    const value = await check();
    if (value !== null) {
      return value;
    }
    if (Date.now() - started > timeoutMs) {
      throw new Error(`Timed out waiting for ${what}`);
    }
    await new Promise((resolve) => setTimeout(resolve, 500));
  }
}

/** The production run opens by itself once the acknowledge event reaches PROD; wait for it. */
export async function waitForRun(request: APIRequestContext, poId: number): Promise<number> {
  return poll(async () => {
    const response = await request.get(`${API}/api/production/runs/by-po/${poId}`);
    return response.ok() ? ((await response.json()) as { id: number }).id : null;
  }, `the production run of PO ${poId}`);
}

/** Waits until the PO reaches a status (PROD's events change it a moment after the action). */
export async function waitForPoStatus(request: APIRequestContext, poId: number, statusId: number): Promise<void> {
  await poll(async () => {
    const po = (await (await request.get(`${API}/api/purchase-orders/${poId}`)).json()) as { statusId: number };
    return po.statusId === statusId ? true : null;
  }, `PO ${poId} to reach status ${statusId}`);
}

export interface TestRun {
  po: TestPo;
  runId: number;
}

/** An acknowledged PO with its production run open. */
export async function createRun(request: APIRequestContext, options: { fabricResponsibilityId?: number } = {}): Promise<TestRun> {
  const po = await createPo(request, 'acknowledged', options);
  return { po, runId: await waitForRun(request, po.id) };
}

export const PERSON = 'E2E Buyer';

export async function apiSubmitAndApproveSample(request: APIRequestContext, runId: number): Promise<void> {
  await ok(await request.post(`${API}/api/production/runs/${runId}/pp-rounds`, { data: { reportedBy: 'E2E Vendor', actingPerson: PERSON } }), 'submit sample');
  const rounds = await ok<{ roundNo: number }[]>(await request.get(`${API}/api/production/runs/${runId}/pp-rounds`), 'list rounds');
  await ok(
    await request.post(`${API}/api/production/runs/${runId}/pp-rounds/${rounds.at(-1)!.roundNo}/decision`, { data: { approve: true, actingPerson: PERSON } }),
    'approve sample',
  );
}

export async function apiStartCutting(request: APIRequestContext, runId: number): Promise<void> {
  await ok(
    await request.post(`${API}/api/production/runs/${runId}/milestones`, {
      data: { typeId: 6, claimedDate: today(), reportedBy: 'E2E Vendor', actingPerson: PERSON },
    }),
    'start bulk cutting',
  );
}

/** A run whose sample is approved and whose bulk cutting has started (the PO is In Production once the event is delivered). */
export async function createRunInProduction(request: APIRequestContext, options: { fabricResponsibilityId?: number } = {}): Promise<TestRun> {
  const run = await createRun(request, options);
  await apiSubmitAndApproveSample(request, run.runId);
  await apiStartCutting(request, run.runId);
  await waitForPoStatus(request, run.po.id, 5);
  return run;
}

export async function apiDeliver(
  request: APIRequestContext,
  run: TestRun,
  dnNo: string,
  qty: number,
  isFinalShipment = false,
): Promise<void> {
  await ok(
    await request.post(`${API}/api/production/runs/${run.runId}/delivery-notes`, {
      data: {
        vendorDnNo: dnNo,
        dispatchDate: today(),
        isFinalShipment,
        lines: [{ sizeId: run.po.sizeId, colourId: run.po.colourId, qty }],
        actingPerson: PERSON,
      },
    }),
    `deliver ${dnNo}`,
  );
}

/** Opens a run's screen. */
export async function openRun(page: Page, run: TestRun): Promise<void> {
  await page.goto(`/production/${run.runId}`);
  await expect(page.getByRole('heading', { level: 1, name: `Production: ${run.po.poNo}` })).toBeVisible();
}

/** Opens a PO's screen straight from a link, the way the Production screen does. */
export async function openPoById(page: Page, po: TestPo): Promise<void> {
  await page.goto(`/purchase-orders?po=${po.id}`);
  await expect(page.getByRole('heading', { name: po.poNo })).toBeVisible();
}

// ---- Sprint 4: receiving and QC ----

export interface Receivable {
  run: TestRun;
  dnNo: string;
  /** QC's inbound delivery, created from the delivery note event. */
  deliveryId: number;
}

/** A run in production with one final delivery note for 100 pieces, once QC has its copy of the delivery note. */
export async function createReceivable(request: APIRequestContext, dnNo = `DN-QC-${uniqueSuffix()}`): Promise<Receivable> {
  const run = await createRunInProduction(request);
  await apiDeliver(request, run, dnNo, 100, true);
  const deliveryId = await poll(async () => {
    const page = (await (await request.get(`${API}/api/quality/inbound-deliveries?search=${run.po.poNo}&pageSize=100`)).json()) as {
      items: { id: number; vendorDnNo: string }[];
    };
    return page.items.find((d) => d.vendorDnNo === dnNo)?.id ?? null;
  }, `the inbound delivery of ${dnNo}`);
  return { run, dnNo, deliveryId };
}

export interface ReceivedBatch extends Receivable {
  grnNo: string;
  batchId: number;
}

/** Records the goods receipt through the API (counted as claimed unless `counted` says otherwise) and returns the QC batch it opened. */
export async function apiReceive(request: APIRequestContext, receivable: Receivable, counted = 100): Promise<ReceivedBatch> {
  const grn = await ok<{ grnNo: string }>(
    await request.post(`${API}/api/quality/grns`, {
      data: {
        inboundDeliveryId: receivable.deliveryId,
        receivedDate: today(),
        actingPerson: PERSON,
        note: null,
        lines: [{ sizeId: receivable.run.po.sizeId, colourId: receivable.run.po.colourId, qty: counted }],
      },
    }),
    'record goods receipt',
  );
  const batches = await ok<{ items: { id: number }[] }>(await request.get(`${API}/api/quality/batches?search=${grn.grnNo}`), 'find the batch');
  return { ...receivable, grnNo: grn.grnNo, batchId: batches.items[0].id };
}

const ALL_PASS = [1, 2, 3, 4, 5, 6].map((checkTypeId) => ({ checkTypeId, resultId: 1 }));

/** Records an inspection through the API and returns its id. */
export async function apiInspect(
  request: APIRequestContext,
  batch: ReceivedBatch,
  options: { subBatchId?: number | null; defects?: object[]; failStitching?: boolean } = {},
): Promise<number> {
  const inspection = await ok<{ id: number }>(
    await request.post(`${API}/api/quality/batches/${batch.batchId}/inspections`, {
      data: {
        inspectionDate: today(),
        actingPerson: PERSON,
        subBatchId: options.subBatchId ?? null,
        checks: ALL_PASS.map((c) => (options.failStitching && c.checkTypeId === 1 ? { ...c, resultId: 2 } : c)),
        defects: options.defects ?? [],
      },
    }),
    'record inspection',
  );
  return inspection.id;
}

/** Records both child-safety checks through the API: both Pass, or the choking-hazard check Fail for the whole batch. */
export async function apiSafety(request: APIRequestContext, inspectionId: number, chokingHazardFails = false): Promise<void> {
  await ok(
    await request.put(`${API}/api/quality/inspections/${inspectionId}/safety-checks`, {
      data: {
        actingPerson: PERSON,
        checks: [
          { safetyTypeId: 1, resultId: chokingHazardFails ? 2 : 1, reason: null, affectedLines: [] },
          { safetyTypeId: 2, resultId: 1, reason: null, affectedLines: [] },
        ],
      },
    }),
    'record safety checks',
  );
}

/** Splits a batch through the API: the first part is accepted, the second is rejected or sent for rework. */
export async function apiSplit(request: APIRequestContext, batch: ReceivedBatch, acceptedQty: number, restOutcomeId: 3 | 4): Promise<{ id: number; outcomeId: number }[]> {
  const { sizeId, colourId } = batch.run.po;
  const decision = await ok<{ subBatches: { id: number; outcomeId: number }[] }>(
    await request.post(`${API}/api/quality/batches/${batch.batchId}/decision`, {
      data: {
        actingPerson: PERSON,
        parts: [
          { outcomeId: 1, reason: null, lines: [{ sizeId, colourId, qty: acceptedQty }] },
          { outcomeId: restOutcomeId, reason: 'E2E: not good enough', lines: [{ sizeId, colourId, qty: 100 - acceptedQty }] },
        ],
      },
    }),
    'decide the batch',
  );
  return decision.subBatches;
}

export const openBatch = async (page: Page, batch: ReceivedBatch): Promise<void> => {
  await page.goto(`/quality/batches/${batch.batchId}`);
  await expect(page.getByRole('heading', { level: 1, name: `QC batch: ${batch.grnNo}` })).toBeVisible();
};

// ---- Sprint 5: labelling ----

export interface WorkItem {
  id: number;
  poId: number;
  variantId: number | null;
  skuCode: string | null;
  gtin: string | null;
  qty: number;
  statusCode: string;
  printedQty: number;
  remainingQty: number;
}

/** A batch of the given quantity, accepted by QC through the API (the rest rejected), so its tags are ready to be made. */
export async function createAcceptedStock(request: APIRequestContext, acceptedQty = 60): Promise<ReceivedBatch> {
  const batch = await apiReceive(request, await createReceivable(request));
  const inspectionId = await apiInspect(request, batch);
  await apiSafety(request, inspectionId);
  await apiSplit(request, batch, acceptedQty, 3);
  return batch;
}

/** Waits for the QC event to reach Labelling and returns the work item it created for a PO (the dispatcher runs on its own timer). */
export async function waitForWorkItem(request: APIRequestContext, poId: number): Promise<WorkItem> {
  return poll(async () => {
    const page = (await (await request.get(`${API}/api/labelling/work-items?pageSize=100`)).json()) as { items: WorkItem[] };
    return page.items.find((i) => i.poId === poId) ?? null;
  }, `the label work item for PO ${poId}`, 60_000);
}

/** Queues tags for a work item through the API on a configured printer (by code) and returns the job. */
export async function apiQueueTags(
  request: APIRequestContext,
  itemId: number,
  qty: number,
  printerCode: 'FAKE-PDF' | 'FAKE-JAM' = 'FAKE-PDF',
): Promise<{ id: number; jobNo: string; gtin: string; skuCode: string }> {
  return ok(
    await request.post(`${API}/api/labelling/work-items/${itemId}/print-jobs`, { data: { qty, printerCode, actingPerson: PERSON } }),
    'queue tags',
  );
}
