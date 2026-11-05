import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';

/**
 * Mega menu image tile (docs/specs/storefront/mega-menu-tile, MGT-1..8). Runs against the storefront and API containers.
 * The demo catalogue has one audience (Girls) with one product, so there is one tile.
 */
const BASE = process.env['STOREFRONT_BASE_URL'] ?? 'http://localhost:4000';
const AXE_TAGS = ['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'];

test.use({ baseURL: BASE });

async function open(page: Page, path = '/'): Promise<void> {
  await page.goto(path, { waitUntil: 'networkidle' });
  await page.waitForTimeout(300);
}
const audienceButton = (page: Page) => page.locator('header nav[aria-label="Primary"] button[aria-controls^="mega-"]').first();
const tile = (page: Page) => page.locator('.rp-mega-host:not([hidden]) a.rp-mega__tile');

test('MGT-1 the menu API gives the audience a tile with the fixed text and a New in link', async ({ request }) => {
  const menu = await (await request.get('/api/storefront/menu')).json();
  const audience = menu.audiences[0];
  expect(audience.tile).toMatchObject({ eyebrow: 'New in', line: 'Just arrived', path: `${audience.path}/new-in` });
  expect(audience.tile.image.sources.length).toBeGreaterThan(0);
});

test('MGT-4 the tile link and its picture markup are in the server HTML, with lazy loading and alt text', async ({ request }) => {
  const html = await (await request.get('/')).text();
  const match = /<a[^>]*class="rp-mega__tile"[\s\S]*?<\/a>/.exec(html);
  expect(match).not.toBeNull();
  expect(match![0]).toContain('loading="lazy"');
  expect(match![0]).toMatch(/alt="[^"]+"/);
  expect(match![0]).toMatch(/width="\d+"/);
  expect(match![0]).toMatch(/height="\d+"/);
});

test.describe('on a desktop', () => {
  test.use({ viewport: { width: 1280, height: 900 } });

  test('MGT-1 MGT-2 opening the audience panel shows a 5:4 tile that links to New in', async ({ page }) => {
    await open(page);
    await audienceButton(page).click();
    await expect(tile(page)).toBeVisible();
    const box = (await tile(page).boundingBox())!;
    expect(Math.abs(box.width / box.height - 5 / 4)).toBeLessThan(0.02);
    await expect(tile(page)).toContainText('Just arrived');
    await expect(tile(page)).toHaveAttribute('aria-label', /, new in$/);
    await expect(tile(page)).toHaveAttribute('href', /\/new-in$/);
  });

  test('MGT-5 the tile picture is not requested before the panel opens', async ({ page }) => {
    // A content page has no product pictures of its own, so any picture request is the menu tile's.
    const pictures: string[] = [];
    page.on('request', (r) => {
      if (/\/media\/.*\.(webp|jpg)/.test(r.url())) pictures.push(r.url());
    });
    await open(page, '/delivery');
    expect(pictures).toEqual([]);
    await audienceButton(page).click();
    await expect(tile(page)).toBeVisible();
    await expect.poll(() => pictures.length).toBeGreaterThan(0);
  });

  test('MGT-6 the panel still closes on Escape and a link click navigates', async ({ page }) => {
    await open(page);
    await audienceButton(page).click();
    await expect(tile(page)).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(tile(page)).toBeHidden();
    await audienceButton(page).click();
    await tile(page).click();
    await expect(page).toHaveURL(/\/new-in$/);
  });

  test('MGT-5 MGT-8 no layout shift when the panel opens, no axe violations, the line is readable over the scrim', async ({ page }) => {
    await page.addInitScript(() => {
      (window as unknown as { __cls: number }).__cls = 0;
      new PerformanceObserver((list) => {
        for (const e of list.getEntries() as unknown as { value: number; hadRecentInput: boolean }[]) {
          if (!e.hadRecentInput) (window as unknown as { __cls: number }).__cls += e.value;
        }
      }).observe({ type: 'layout-shift', buffered: true });
    });
    await open(page);
    const before = await page.evaluate(() => (window as unknown as { __cls: number }).__cls);
    await audienceButton(page).click();
    await expect(tile(page)).toBeVisible();
    await page.waitForTimeout(500);
    const after = await page.evaluate(() => (window as unknown as { __cls: number }).__cls);
    expect(after - before).toBeLessThanOrEqual(0.01);
    const results = await new AxeBuilder({ page }).withTags(AXE_TAGS).analyze();
    expect(results.violations.map((v) => v.id + ': ' + v.nodes.map((n) => JSON.stringify(n.target)).join(', '))).toEqual([]);
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
    expect(overflow).toBeLessThanOrEqual(0);
  });
});

test.describe('on a phone', () => {
  test.use({ viewport: { width: 390, height: 844 } });

  test('MGT-7 the mobile menu shows no tile', async ({ page }) => {
    await open(page);
    await page.getByRole('button', { name: 'Open menu' }).click();
    const dialog = page.getByRole('dialog', { name: 'Menu' });
    await expect(dialog).toBeVisible();
    await expect(dialog.locator('img')).toHaveCount(0);
    await expect(dialog.locator('.rp-mega__tile')).toHaveCount(0);
  });
});
