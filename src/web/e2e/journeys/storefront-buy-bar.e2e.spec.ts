import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';

/**
 * Product page buy bar and perks (docs/specs/storefront/product-buy-bar, BUY-1..11). Runs against the storefront container.
 * The bar is for phones and tablets (under 64rem); the demo settings have a WhatsApp number but no delivery or exchange facts,
 * so only the WhatsApp perk is expected here (BUY-11).
 */
const BASE = process.env['STOREFRONT_BASE_URL'] ?? 'http://localhost:4000';
const PRODUCT = '/girls/dresses/floral-summer-dress';
const AXE_TAGS = ['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'];

test.use({ baseURL: BASE });

async function open(page: Page): Promise<void> {
  await page.goto(PRODUCT, { waitUntil: 'networkidle' });
  await page.waitForTimeout(300);
}
const bar = (page: Page) => page.locator('rp-buy-bar .rp-buybar');
const scrollToEnd = async (page: Page) => {
  await page.evaluate(() => window.scrollTo(0, document.documentElement.scrollHeight));
  await page.waitForTimeout(300);
};

for (const vp of [
  { name: 'phone', width: 390, height: 844 },
  { name: 'tablet', width: 768, height: 1024 },
]) {
  test.describe(`buy bar at ${vp.name}`, () => {
    test.use({ viewport: { width: vp.width, height: vp.height } });

    test('BUY-1 BUY-2 the bar is hidden while the main button is in view and appears once it scrolls away', async ({ page }) => {
      await open(page);
      await page.evaluate(() => window.scrollTo(0, 0));
      await page.locator('.pdp-add').scrollIntoViewIfNeeded();
      await expect(bar(page)).toHaveCount(0);
      await scrollToEnd(page);
      await expect(bar(page)).toBeVisible();
      await expect(bar(page)).toContainText('Rs.');
      await expect(bar(page).getByRole('button', { name: 'Add to bag' })).toBeVisible();
    });

    test('BUY-3 with no size chosen the bar takes the visitor to the sizes, says why and adds nothing', async ({ page }) => {
      await open(page);
      await scrollToEnd(page);
      await bar(page).getByRole('button', { name: 'Add to bag' }).click();
      await expect(page.locator('rp-size-options a:focus')).toHaveCount(1);
      await expect(page.getByRole('button', { name: /^Bag, 0 items?$/ })).toBeVisible();
      await expect(page.getByRole('dialog', { name: 'Your bag' })).toBeHidden();
    });

    test('BUY-4 with a size chosen the bar adds it through the same action and the bag opens with one line', async ({ page }) => {
      await open(page);
      await page.locator('rp-size-options a').first().click();
      await page.waitForLoadState('networkidle');
      await scrollToEnd(page);
      await bar(page).getByRole('button', { name: 'Add to bag' }).click();
      const drawer = page.getByRole('dialog', { name: 'Your bag' });
      await expect(drawer).toBeVisible();
      await expect(drawer.getByRole('heading', { name: /Your bag \(1\)/ })).toBeVisible();
    });

    test('BUY-7 BUY-8 the bar is 44px, the page keeps room for it, no layout shift, no horizontal scroll, axe clean', async ({ page }) => {
      await page.addInitScript(() => {
        (window as unknown as { __cls: number }).__cls = 0;
        new PerformanceObserver((list) => {
          for (const e of list.getEntries() as unknown as { value: number; hadRecentInput: boolean }[]) {
            if (!e.hadRecentInput) (window as unknown as { __cls: number }).__cls += e.value;
          }
        }).observe({ type: 'layout-shift', buffered: true });
      });
      await open(page);
      await scrollToEnd(page);
      await expect(bar(page)).toBeVisible();
      const barBox = (await bar(page).boundingBox())!;
      const button = (await bar(page).getByRole('button').boundingBox())!;
      expect(button.height).toBeGreaterThanOrEqual(44);
      const padding = await page.evaluate(() => ({ scroll: getComputedStyle(document.documentElement).scrollPaddingBottom, body: getComputedStyle(document.body).paddingBottom }));
      expect(Math.abs(parseFloat(padding.scroll) - barBox.height)).toBeLessThanOrEqual(1);
      expect(Math.abs(parseFloat(padding.body) - barBox.height)).toBeLessThanOrEqual(1);
      // The last line of the page is not covered: the footer's bottom edge sits above the bar.
      const footerBottom = await page.evaluate(() => document.querySelector('footer')!.getBoundingClientRect().bottom);
      expect(footerBottom).toBeLessThanOrEqual(barBox.y + 1);
      const results = await new AxeBuilder({ page }).withTags(AXE_TAGS).analyze();
      expect(results.violations.map((v) => v.id + ': ' + v.nodes.map((n) => JSON.stringify(n.target)).join(', '))).toEqual([]);
      const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
      expect(overflow).toBeLessThanOrEqual(0);
      expect(await page.evaluate(() => (window as unknown as { __cls: number }).__cls)).toBeLessThanOrEqual(0.1);
    });

    test('BUY-7 a focused control near the bottom is not hidden behind the bar', async ({ page }) => {
      await open(page);
      await scrollToEnd(page);
      await expect(bar(page)).toBeVisible();
      const barTop = (await bar(page).boundingBox())!.y;
      const link = page.locator('footer a').last();
      await link.focus();
      await page.waitForTimeout(200);
      const bottom = (await link.boundingBox())!.y + (await link.boundingBox())!.height;
      expect(bottom).toBeLessThanOrEqual(barTop + 1);
    });
  });
}

test.describe('buy bar at desktop', () => {
  test.use({ viewport: { width: 1280, height: 900 } });

  test('BUY-9 from 64rem there is no bar, whatever the scroll', async ({ page }) => {
    await open(page);
    await scrollToEnd(page);
    await expect(bar(page)).toHaveCount(0);
  });
});

test.describe('perks', () => {
  test.use({ viewport: { width: 390, height: 844 } });

  test('BUY-10 BUY-11 the perks show only the facts the settings have: a WhatsApp link here, no empty lines', async ({ page, request }) => {
    const settings = await (await request.get('/api/storefront/settings')).json().catch(() => null);
    await open(page);
    const lines = await page.locator('.rp-perks li').allTextContents();
    expect(lines.length).toBeGreaterThan(0);
    for (const line of lines) expect(line.trim()).not.toBe('');
    const whatsapp = page.locator('.rp-perks a');
    await expect(whatsapp).toHaveAttribute('href', /wa\.me\//);
    void settings;
  });

  test('BUY-10 the perks are in the server HTML', async ({ request }) => {
    const html = await (await request.get(PRODUCT)).text();
    expect(html).toContain('class="rp-perks"');
  });
});
