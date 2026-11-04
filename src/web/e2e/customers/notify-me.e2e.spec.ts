import { execSync } from 'node:child_process';
import AxeBuilder from '@axe-core/playwright';
import { APIRequestContext, expect, test } from '@playwright/test';
import { openHydrated } from './customer-session';
import { uniqueSuffix } from '../support';

/**
 * Notify Me in the storefront container (NTM-1 to NTM-10, TC-PDP-04..06): a shopper chooses the sold-out size of the demo product, asks to be
 * told, asks again (still one request), gets exactly one email when Warehouse reports stock for it, and the form works without JavaScript.
 * The restock is simulated by writing the Warehouse stock event into its outbox, exactly what a putaway would do.
 * Run: `docker compose -f docker-compose.yml -f docker-compose.secure.yml up -d --build`, then
 * `STOREFRONT_BASE_URL=http://localhost:4000 npx playwright test e2e/customers/notify-me`.
 */
const BASE = process.env['STOREFRONT_BASE_URL'] ?? 'http://localhost:4000';
const API = process.env['API_BASE_URL'] ?? 'http://localhost:8080';
const MAILPIT = process.env['MAILPIT_URL'] ?? 'http://localhost:8025';
const AXE_TAGS = ['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'];
const DEMO = '/girls/dresses/floral-summer-dress';

test.use({ baseURL: BASE });

interface SizeRow { code: string; name: string; availability: string; selectable: boolean; variantId: number }
interface PageJson { page: { selectedColour: string; sizes: SizeRow[] } | null }

function sql(statement: string): string {
  const user = process.env['POSTGRES_USER'] ?? 'romp';
  const db = process.env['POSTGRES_DB'] ?? 'romp';
  return execSync(`docker compose exec -T postgres psql -U ${user} -d ${db} -tA -c "${statement.replace(/"/g, '\\"')}"`, { stdio: 'pipe' }).toString().trim();
}

/** The first sold-out size the demo product offers, or null when the data has none. */
async function soldOutSize(request: APIRequestContext): Promise<{ colour: string; size: SizeRow } | null> {
  for (const colour of ['wht', 'blk']) {
    const json = (await (await request.get(`${API}/api/storefront/products/by-path?path=${DEMO}&colour=${colour}`)).json()) as PageJson;
    const size = json.page?.sizes.find((s) => s.availability === 'SoldOut' && s.selectable);
    if (size) return { colour: json.page!.selectedColour, size };
  }
  return null;
}

/** Writes the Warehouse stock event a putaway would, so the Customers consumer sees the variant come back. */
function restock(variantId: number): void {
  const payload = JSON.stringify({
    VariantId: variantId, StyleId: 1, ColourId: 1, SizeId: 1, SkuCode: 'E2E-NOTIFY', Gtin: '0000000000000',
    OnHand: 5, Available: 5, OnHandAccepted: 5, OnHandConcession: 0, ReasonCode: 'PUTAWAY', Version: 0,
  });
  sql(`INSERT INTO "WHSE"."OUTB_MSG" ("EVNT_TYP","PYLD","INSR_DTE","AGGR_TYP","AGGR_ID") VALUES ('StockLevelChangedEvent', '${payload}'::jsonb, now(), 'Variant', ${variantId})`);
}

interface MailSummary { ID: string; Subject: string }

async function restockMails(request: APIRequestContext, to: string): Promise<MailSummary[]> {
  const found = (await (await request.get(`${MAILPIT}/api/v1/search?query=${encodeURIComponent(`to:${to}`)}`)).json()) as { messages?: MailSummary[] };
  return (found.messages ?? []).filter((m) => m.Subject.includes('back in stock'));
}

async function waitForRestockMail(request: APIRequestContext, to: string): Promise<{ Text: string }> {
  for (let attempt = 0; attempt < 60; attempt++) {
    const [summary] = await restockMails(request, to);
    if (summary) return (await (await request.get(`${MAILPIT}/api/v1/message/${summary.ID}`)).json()) as { Text: string };
    await new Promise((resolve) => setTimeout(resolve, 500));
  }
  throw new Error(`No restock email to ${to} arrived.`);
}

test.describe('notify me', () => {
  test('NTM-1 NTM-2 NTM-3 NTM-4 NTM-5 a shopper asks twice, gets one email when the size is back, and the page is accessible', async ({ page, request }) => {
    const sold = await soldOutSize(request);
    test.skip(sold === null, 'The demo product has no sold-out size in this data.');
    const { colour, size } = sold!;
    const email = `e2e-ntm-${uniqueSuffix()}@example.com`;

    await openHydrated(page, `${DEMO}?colour=${colour}&size=${size.code}`);
    const form = page.locator('rp-notify-me-form');
    await expect(form.getByRole('heading', { name: new RegExp(`Size ${size.name} is sold out`) })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Add to bag' })).toBeDisabled();

    const axe = await new AxeBuilder({ page }).withTags(AXE_TAGS).analyze();
    expect(axe.violations).toEqual([]);

    await form.getByLabel('Email me when it is back').fill(email);
    await form.getByRole('button', { name: 'Notify me' }).click();
    await expect(form.getByRole('status').filter({ hasText: 'We will email you once' })).toBeVisible();

    // The same address again (any case) gets the same answer and does not make a second request.
    await form.getByLabel('Email me when it is back').fill(email.toUpperCase());
    await form.getByRole('button', { name: 'Notify me' }).click();
    await expect(form.getByRole('status').filter({ hasText: 'We will email you once' })).toBeVisible();
    expect(sql(`SELECT count(*) FROM "CUST"."NTFM_REQ" WHERE "EML" = '${email}' AND "STS_ID" = 1`)).toBe('1');

    // A bad address is named in words and records nothing.
    await form.getByLabel('Email me when it is back').fill('not-an-email@x');
    await form.getByRole('button', { name: 'Notify me' }).click();
    await expect(form.getByRole('status')).toContainText(/email address|check/i);

    restock(size.variantId);
    const mail = await waitForRestockMail(request, email);
    const text = mail.Text;
    expect(text).toContain(DEMO);
    expect(text).not.toContain(email);
    await new Promise((resolve) => setTimeout(resolve, 2000));
    expect(await restockMails(request, email)).toHaveLength(1);
    expect(sql(`SELECT "STS_ID" || ',' || ("SENT_DTE" IS NOT NULL) FROM "CUST"."NTFM_REQ" WHERE "EML" = '${email}'`)).toBe('2,true');
  });

  test('NTM-1 an in-stock size does not offer Notify me', async ({ page, request }) => {
    const json = (await (await request.get(`${API}/api/storefront/products/by-path?path=${DEMO}&colour=wht`)).json()) as PageJson;
    const inStock = json.page?.sizes.find((s) => s.availability === 'InStock');
    test.skip(!inStock, 'The demo product has no in-stock size in this data.');

    await openHydrated(page, `${DEMO}?colour=wht&size=${inStock!.code}`);

    await expect(page.locator('rp-notify-me-form')).toHaveCount(0);
  });

  test.describe('without JavaScript', () => {
    test.use({ javaScriptEnabled: false });

    test('NTM-10 the form posts to the server and lands on a noindex result page', async ({ page, request }) => {
      const sold = await soldOutSize(request);
      test.skip(sold === null, 'The demo product has no sold-out size in this data.');
      const email = `e2e-ntm-nojs-${uniqueSuffix()}@example.com`;

      await page.goto(`${DEMO}?colour=${sold!.colour}&size=${sold!.size.code}`);
      await page.getByLabel('Email me when it is back').fill(email);
      await page.getByRole('button', { name: 'Notify me' }).click();

      await expect(page).toHaveURL(/\/notify-me\/requested\?status=ok/);
      await expect(page.getByRole('heading', { level: 1, name: 'We will let you know' })).toBeVisible();
      expect(await page.locator('meta[name=robots]').getAttribute('content')).toContain('noindex');
      expect(sql(`SELECT count(*) FROM "CUST"."NTFM_REQ" WHERE "EML" = '${email}'`)).toBe('1');
    });
  });
});
