import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';

/**
 * Product card quick add (docs/specs/storefront/product-card-quick-add, CRD-1..15). Runs against the storefront and API containers.
 * The demo product has two sizes (one sold out), one colour for the card and one picture, so the second-picture checks are unit-tested.
 */
const BASE = process.env['STOREFRONT_BASE_URL'] ?? 'http://localhost:4000';
const AXE_TAGS = ['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'];

test.use({ baseURL: BASE });

async function open(page: Page, path = '/girls'): Promise<void> {
  await page.goto(path, { waitUntil: 'networkidle' });
  await page.waitForTimeout(400);
}
const card = (page: Page) => page.locator('article.rp-card').first();
const bar = (page: Page) => card(page).locator('.rp-qa__host');
const chip = (page: Page, name: string) => card(page).locator('.rp-qa__sizes button', { hasText: name });

test('CRD-13 the server HTML is the plain card: picture, name link, price and sizes, with no quick-add bar', async ({ request }) => {
  const html = await (await request.get('/girls')).text();
  expect(html).toContain('class="rp-tile-img"');
  expect(html).toMatch(/<h3[^>]*><a[^>]*href="\/girls\/dresses\//);
  expect(html).toContain('class="rp-avail"');
  expect(html).not.toContain('rp-quick-add-bar');
  expect(html).not.toContain('rp-qa__plus');
});

test.describe('quick add on a desktop', () => {
  test.use({ viewport: { width: 1280, height: 900 } });

  test('CRD-4 hovering the card slides the bar up with 44px size chips; a sold-out size is aria-disabled and says so', async ({ page }) => {
    await open(page);
    await expect(page.locator('.rp-qa__plus')).toBeHidden();
    await card(page).locator('h3 a').hover();
    await expect(bar(page)).toBeVisible();
    await expect(bar(page)).toContainText('Choose a size');
    const media = (await card(page).locator('.rp-media').boundingBox())!;
    await page.waitForTimeout(600);
    const host = (await bar(page).boundingBox())!;
    expect(host.y + host.height).toBeLessThanOrEqual(media.y + media.height + 1);
    expect(host.y).toBeGreaterThanOrEqual(media.y);
    const available = chip(page, '1-2 Years');
    expect((await available.boundingBox())!.height).toBeGreaterThanOrEqual(44);
    const sold = chip(page, '2-3 Years');
    await expect(sold).toHaveAttribute('aria-disabled', 'true');
    await expect(sold).toContainText('sold out');
  });

  test('CRD-5 CRD-12 pressing a size adds it in one press: the bar confirms, the bag count changes, the drawer stays shut, View bag opens it', async ({ page }) => {
    await open(page);
    await card(page).locator('h3 a').hover();
    await chip(page, '1-2 Years').click();
    await expect(bar(page).getByRole('status')).toContainText('Added, size 1-2 Years');
    await expect(page.getByRole('button', { name: /^Bag, 1 item$/ })).toBeVisible();
    await expect(page.getByRole('dialog', { name: 'Your bag' })).toBeHidden();
    // The bar uses ink buttons, never the marigold action colour.
    const colour = await bar(page).getByRole('button', { name: 'View bag' }).evaluate((el) => getComputedStyle(el).backgroundColor);
    expect(colour).not.toBe('rgb(217, 166, 46)');
    await bar(page).getByRole('button', { name: 'View bag' }).click();
    const drawer = page.getByRole('dialog', { name: 'Your bag' });
    await expect(drawer).toBeVisible();
    await expect(drawer.getByRole('heading', { name: /Your bag \(1\)/ })).toBeVisible();
  });

  test('CRD-11 focusing a size with the keyboard shows the bar, Enter adds, and Escape puts it away and returns focus to the card link', async ({ page }) => {
    await open(page);
    await chip(page, '1-2 Years').focus();
    await expect(bar(page)).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(card(page).locator('h3 a')).toBeFocused();
    await expect.poll(async () => (await bar(page).boundingBox())!.y).toBeGreaterThanOrEqual((await card(page).locator('.rp-media').boundingBox())!.y + (await card(page).locator('.rp-media').boundingBox())!.height - 1);
    await chip(page, '1-2 Years').focus();
    await page.keyboard.press('Enter');
    await expect(page.getByRole('button', { name: /^Bag, 1 item$/ })).toBeVisible();
  });
});

test.describe('quick add on a phone', () => {
  test.use({ viewport: { width: 390, height: 844 }, hasTouch: true });

  test('CRD-6 a 44px round + opens the bar; the sizes add; Close puts it away', async ({ page }) => {
    await open(page);
    const plus = card(page).locator('.rp-qa__plus');
    await expect(plus).toBeVisible();
    const box = (await plus.boundingBox())!;
    expect(box.width).toBeGreaterThanOrEqual(44);
    expect(box.height).toBeGreaterThanOrEqual(44);
    await expect(plus).toHaveAttribute('aria-expanded', 'false');
    await expect(bar(page)).toBeHidden();
    await plus.click();
    await expect(plus).toHaveAttribute('aria-expanded', 'true');
    await expect(bar(page)).toBeVisible();
    await bar(page).getByRole('button', { name: 'Close quick add' }).click();
    await expect(bar(page)).toBeHidden();
    await plus.click();
    await chip(page, '1-2 Years').click();
    await expect(bar(page).getByRole('status')).toContainText('Added, size 1-2 Years');
    await expect(page.getByRole('dialog', { name: 'Your bag' })).toBeHidden();
  });
});

for (const vp of [
  { name: 'phone', width: 390, height: 844 },
  { name: 'tablet', width: 768, height: 1024 },
  { name: 'desktop', width: 1280, height: 900 },
]) {
  test.describe(`quality at ${vp.name}`, () => {
    test.use({ viewport: { width: vp.width, height: vp.height } });

    test('CRD-14 no horizontal scroll, no axe violations with the bar open, no layout shift when it opens', async ({ page }) => {
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
      if (vp.width < 1024) await card(page).locator('.rp-qa__plus').click();
      else await card(page).locator('h3 a').hover();
      await expect(bar(page)).toBeVisible();
      await page.waitForTimeout(400);
      const after = await page.evaluate(() => (window as unknown as { __cls: number }).__cls);
      expect(after - before).toBeLessThanOrEqual(0.01);
      const results = await new AxeBuilder({ page }).withTags(AXE_TAGS).analyze();
      expect(results.violations.map((v) => v.id + ': ' + v.nodes.map((n) => JSON.stringify(n.target)).join(', '))).toEqual([]);
      const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
      expect(overflow).toBeLessThanOrEqual(0);
    });
  });
}
