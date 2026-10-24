import { APIRequestContext, Page, expect, test } from '@playwright/test';
import { API, uniqueSuffix } from '../support';

/**
 * SCRUM-206 task 22 (ST-1, ST-8): the admin app changes the storefront settings, and the storefront shows the published result.
 * Runs against the docker stack (admin :4201, API :8080, storefront :4000). The two apps are different origins, so the shop is read
 * through `shop`, an absolute-URL request, and the admin through the configured `baseURL`.
 *
 * Set-up goes through the admin API: the seeded pages are drafts and the first publish is refused until they are published (ST-17).
 * Afterwards the first test puts the menu back and the last archives its own page, so the shop returns to the seeded wording.
 */
const SHOP = process.env['STOREFRONT_BASE_URL'] ?? 'http://localhost:4000';
const ADMIN_API = `${API}/api/admin/storefront-settings`;
const PUBLIC_WITHIN_MS = 60_000; // the public copy lives for 60 s (ST-13); a publish also drops it sooner

interface PageRow { id: number; slug: string; title: string; status: string }
interface PageDetail extends PageRow { metaDescription: string; rowVersion: number; blocks: unknown[] }

async function json<T>(response: import('@playwright/test').APIResponse, what: string): Promise<T> {
  if (!response.ok()) throw new Error(`${what} failed: ${response.status()} ${await response.text()}`);
  return (await response.json()) as T;
}

async function getPage(request: APIRequestContext, id: number): Promise<PageDetail> {
  return json<PageDetail>(await request.get(`${ADMIN_API}/pages/${id}`), `get page ${id}`);
}

/** Publishes every page that is not yet published (the two legal placeholders get a description first). */
async function publishSeededPages(request: APIRequestContext): Promise<void> {
  const list = await json<{ items: PageRow[] }>(await request.get(`${ADMIN_API}/pages?pageSize=100`), 'list pages');
  for (const row of list.items.filter((p) => p.status === 'Draft')) {
    let page = await getPage(request, row.id);
    if (page.metaDescription.length === 0) {
      page = await json<PageDetail>(
        await request.put(`${ADMIN_API}/pages/${page.id}`, {
          data: { rowVersion: page.rowVersion, slug: page.slug, title: page.title, metaDescription: `${page.title} information.`, blocks: page.blocks },
        }),
        `describe ${page.slug}`,
      );
    }
    await json(await request.put(`${ADMIN_API}/pages/${page.id}/status`, { data: { rowVersion: page.rowVersion, status: 'Published' } }), `publish ${page.slug}`);
  }
}

interface NavItem { key: string; label: string; children: NavItem[] }

/** Puts the "Sale" entry back as seeded (a run that failed half-way may have left a rename or members in the draft). */
async function resetMenu(request: APIRequestContext): Promise<void> {
  const draft = await json<{ version: number; navigation: NavItem[] }>(await request.get(`${ADMIN_API}/draft`), 'get draft');
  const sale = draft.navigation.find((n) => n.key === 'sale');
  if (!sale || (sale.label === 'Sale' && sale.children.length === 0)) return;
  const items = draft.navigation.map((n) => (n.key === 'sale' ? { ...n, label: 'Sale', children: [] } : n));
  await json(await request.put(`${ADMIN_API}/navigation`, { data: { expectedVersion: draft.version, items } }), 'reset menu');
}

async function publishSettings(request: APIRequestContext, note: string): Promise<void> {
  const draft = await json<{ version: number }>(await request.get(`${ADMIN_API}/draft`), 'get draft');
  await json(await request.post(`${ADMIN_API}/publish`, { data: { expectedVersion: draft.version, note } }), 'publish settings');
}

/** The admin index loads web fonts from Google; do not wait for them (the page is usable once the DOM is ready). */
async function open(page: Page, path: string): Promise<void> {
  await page.goto(path, { waitUntil: 'domcontentloaded' });
}

/** Publishes from the admin Publish screen with a note, and waits for the confirmation. */
async function publishFromAdmin(page: Page, note: string): Promise<void> {
  await open(page, '/storefront/publish');
  await expect(page.getByRole('heading', { level: 1, name: 'Publish' })).toBeVisible();
  const publishNow = page.getByRole('button', { name: 'Publish now' });
  await expect(publishNow).toBeEnabled();
  await page.getByLabel('Note for this version (optional)').fill(note);
  await publishNow.click();
  await expect(page.getByRole('status').filter({ hasText: /Published as version \d+/ })).toBeVisible();
}

async function shopHtml(request: APIRequestContext, path: string): Promise<string> {
  return (await request.get(`${SHOP}${path}`)).text();
}

test.describe.configure({ mode: 'serial' });

test.describe('storefront settings: admin to shop', () => {
  let suffix: string;

  test.beforeAll(async ({ playwright }) => {
    suffix = uniqueSuffix();
    const request = await playwright.request.newContext();
    await publishSeededPages(request);
    await resetMenu(request);
    await publishSettings(request, `e2e set-up ${suffix}`);
    await request.dispose();
  });

  test('ST-1 renaming an entry and adding a member shows on the shop within 60 s, and putting them back removes them', async ({ page, request }) => {
    const renamed = `Clearance ${suffix}`;
    const member = `Outlet ${suffix}`;

    await open(page, '/storefront/navigation');
    await expect(page.getByRole('heading', { level: 1, name: 'Navigation' })).toBeVisible();
    const entry = (key: string) => page.locator('fieldset.nav-node__box').filter({ hasText: `Key: ${key} ` });

    // Rename "Sale" (key "sale"), add a member under it and name the member.
    await entry('sale').getByLabel('Label').fill(renamed);
    await page.getByRole('button', { name: `Add a member to ${renamed}` }).click();
    await entry('new-entry').getByLabel('Label').fill(member);
    await page.getByRole('button', { name: 'Save navigation' }).click();
    await expect(page.getByText('Saved. It goes live when you publish.')).toBeVisible();

    await publishFromAdmin(page, `e2e rename ${suffix}`);
    await expect
      .poll(async () => {
        const html = await shopHtml(request, '/delivery');
        return html.includes(renamed) && html.includes(member);
      }, { timeout: PUBLIC_WITHIN_MS, intervals: [1_000] })
      .toBe(true);

    // Put the menu back so other suites see the seeded one: rename, remove the member, publish.
    await open(page, '/storefront/navigation');
    await expect(page.getByRole('heading', { level: 1, name: 'Navigation' })).toBeVisible();
    await page.getByRole('button', { name: `Remove ${member} and its members` }).click();
    await entry('sale').getByLabel('Label').fill('Sale');
    await page.getByRole('button', { name: 'Save navigation' }).click();
    await expect(page.getByText('Saved. It goes live when you publish.')).toBeVisible();
    await publishFromAdmin(page, `e2e restore ${suffix}`);
    await expect
      .poll(async () => {
        const html = await shopHtml(request, '/delivery');
        return !html.includes(renamed) && !html.includes(member);
      }, { timeout: PUBLIC_WITHIN_MS, intervals: [1_000] })
      .toBe(true);
  });

  test('ST-8 editing a page shows the new text on the shop, and archiving it makes its address answer 410', async ({ page, request }) => {
    const slug = `e2e-${suffix}`;
    const original = `Original wording ${suffix}.`;
    const edited = `Edited wording ${suffix}.`;

    // A page of its own, published in the draft (set-up through the API).
    const created = await json<PageDetail>(
      await request.post(`${ADMIN_API}/pages`, {
        data: {
          slug,
          title: `E2E page ${suffix}`,
          metaDescription: 'A page created by the end-to-end test.',
          blocks: [{ type: 'Paragraph', text: original, level: 0, factKey: null, label: null, ordered: false, items: [] }],
        },
      }),
      'create page',
    );
    await json(await request.put(`${ADMIN_API}/pages/${created.id}/status`, { data: { rowVersion: created.rowVersion, status: 'Published' } }), 'publish page');
    await publishSettings(request, `e2e page ${suffix}`);
    await expect
      .poll(async () => (await request.get(`${SHOP}/${slug}`)).status(), { timeout: PUBLIC_WITHIN_MS, intervals: [1_000] })
      .toBe(200);
    expect(await shopHtml(request, `/${slug}`)).toContain(original);

    // Edit the paragraph in the admin app, save and publish.
    await open(page, `/storefront/pages/${created.id}`);
    await expect(page.getByRole('heading', { level: 1, name: 'Edit page' })).toBeVisible();
    await page.getByRole('textbox', { name: 'Paragraph' }).fill(edited);
    await page.getByRole('button', { name: 'Save the page' }).click();
    await expect(page.getByRole('status').filter({ hasText: /Saved\./ })).toBeVisible();
    await publishFromAdmin(page, `e2e edit ${suffix}`);
    await expect
      .poll(async () => (await shopHtml(request, `/${slug}`)).includes(edited), { timeout: PUBLIC_WITHIN_MS, intervals: [1_000] })
      .toBe(true);

    // Archive it: the address is gone for good (410), not a 404 and not a 200 "not found" page.
    await open(page, `/storefront/pages/${created.id}`);
    await page.getByRole('button', { name: 'Archive' }).click();
    await expect(page.getByRole('heading', { level: 2, name: 'Status: Archived' })).toBeVisible();
    await publishFromAdmin(page, `e2e archive ${suffix}`);
    await expect
      .poll(async () => (await request.get(`${SHOP}/${slug}`)).status(), { timeout: PUBLIC_WITHIN_MS, intervals: [1_000] })
      .toBe(410);
  });
});
