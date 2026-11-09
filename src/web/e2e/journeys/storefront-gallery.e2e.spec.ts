import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';

/**
 * Product gallery and lightbox (docs/specs/storefront/product-gallery-lightbox, GAL-1..14). Runs against the storefront container.
 * The demo product has one picture, so the tests that need several (counter, moving) skip themselves until a product with more exists.
 */
const BASE = process.env['STOREFRONT_BASE_URL'] ?? 'http://localhost:4000';
const PRODUCT = '/girls/dresses/floral-summer-dress';
const AXE_TAGS = ['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'];

test.use({ baseURL: BASE });

async function open(page: Page): Promise<number> {
  await page.goto(PRODUCT, { waitUntil: 'networkidle' });
  return page.locator('.rp-gimg').count();
}
const scale = async (page: Page) => page.locator('.rp-lb__img').evaluate((el) => Number(/scale\(([\d.]+)\)/.exec((el as HTMLElement).style.transform)?.[1] ?? 1));

test('GAL-1 GAL-14 the server HTML has every picture as an img inside a button, without any scripting', async ({ request }) => {
  const html = await (await request.get(PRODUCT)).text();
  const buttons = html.match(/<button[^>]*class="rp-gimg"[\s\S]*?<\/button>/g) ?? [];
  expect(buttons.length).toBeGreaterThan(0);
  for (const b of buttons) {
    expect(b).toMatch(/<img[^>]*alt="[^"]+"/);
    expect(b).toMatch(/width="\d+"/);
    expect(b).toMatch(/height="\d+"/);
  }
  expect(buttons[0]).toContain('fetchpriority="high"');
});

for (const vp of [
  { name: 'phone', width: 390, height: 844 },
  { name: 'tablet', width: 768, height: 1024 },
  { name: 'desktop', width: 1280, height: 900 },
]) {
  test.describe(`gallery at ${vp.name}`, () => {
    test.use({ viewport: { width: vp.width, height: vp.height } });

    test('GAL-13 no horizontal scroll, no axe violations, picture buttons are at least 44px', async ({ page }) => {
      await open(page);
      const results = await new AxeBuilder({ page }).withTags(AXE_TAGS).analyze();
      expect(results.violations.map((v) => v.id + ': ' + v.nodes.map((n) => JSON.stringify(n.target)).join(', '))).toEqual([]);
      const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
      expect(overflow).toBeLessThanOrEqual(0);
      const small = await page.$$eval('.rp-gimg', (els) => els.filter((e) => e.getBoundingClientRect().height < 44).length);
      expect(small).toBe(0);
    });

    test('GAL-3 GAL-8 pressing a picture opens the viewer; Escape closes it and focus returns to the picture', async ({ page }) => {
      expect(await open(page)).toBeGreaterThan(0);
      const first = page.locator('.rp-gimg').first();
      await first.click();
      const dialog = page.getByRole('dialog', { name: 'Product pictures' });
      await expect(dialog).toBeVisible();
      await expect(dialog.locator('.rp-lb__top')).toContainText(/1 \/ \d+/);
      await expect(dialog.getByText('Pinch or double-tap to zoom')).toBeVisible();
      await page.keyboard.press('Escape');
      await expect(dialog).toBeHidden();
      await expect(first).toBeFocused();
    });

    test('GAL-3 the viewer picture is fitted at 4:5 inside the stage, never stretched or cropped', async ({ page }) => {
      expect(await open(page)).toBeGreaterThan(0);
      await page.locator('.rp-gimg').first().click();
      await expect(page.locator('.rp-lb__img img')).toHaveCount(1);
      const { pic, stage } = await page.evaluate(() => {
        const r = (s: string) => document.querySelector(s)!.getBoundingClientRect();
        return { pic: r('.rp-lb__img'), stage: r('.rp-lb__stage') };
      });
      expect(Math.abs(pic.width / pic.height - 0.8)).toBeLessThan(0.01);
      expect(pic.width).toBeLessThanOrEqual(stage.width + 0.5);
      expect(pic.height).toBeLessThanOrEqual(stage.height + 0.5);
    });

    test('GAL-9 the viewer picture is requested only when it opens', async ({ page }) => {
      await open(page);
      expect(await page.locator('.rp-lb__img img').count()).toBe(0);
      await page.locator('.rp-gimg').first().click();
      await expect(page.locator('.rp-lb__img img')).toHaveCount(1);
    });

    test('GAL-13 opening the viewer causes no layout shift', async ({ page }) => {
      await page.addInitScript(() => {
        (window as unknown as { __cls: number }).__cls = 0;
        new PerformanceObserver((list) => {
          for (const e of list.getEntries() as unknown as { value: number; hadRecentInput: boolean }[]) {
            if (!e.hadRecentInput) (window as unknown as { __cls: number }).__cls += e.value;
          }
        }).observe({ type: 'layout-shift', buffered: true });
      });
      await open(page);
      await page.waitForTimeout(500);
      const before = await page.evaluate(() => (window as unknown as { __cls: number }).__cls);
      await page.locator('.rp-gimg').first().click();
      await page.getByRole('dialog', { name: 'Product pictures' }).waitFor();
      await page.keyboard.press('Escape');
      await page.waitForTimeout(400);
      const after = await page.evaluate(() => (window as unknown as { __cls: number }).__cls);
      expect(after - before).toBeLessThanOrEqual(0.01);
    });
  });
}

test.describe('viewer controls', () => {
  test.use({ viewport: { width: 1280, height: 900 } });

  test('GAL-4 GAL-7 the + key and the buttons zoom, the - key zooms out, reset returns to 1x', async ({ page }) => {
    expect(await open(page)).toBeGreaterThan(0);
    await page.locator('.rp-gimg').first().click();
    const dialog = page.getByRole('dialog', { name: 'Product pictures' });
    await expect(dialog).toBeVisible();
    await page.keyboard.press('+');
    await expect.poll(() => scale(page)).toBe(2);
    await dialog.getByRole('button', { name: 'Zoom in' }).click();
    await expect.poll(() => scale(page)).toBe(2.5);
    await page.keyboard.press('-');
    await expect.poll(() => scale(page)).toBe(2);
    await dialog.getByRole('button', { name: 'Reset zoom' }).click();
    await expect.poll(() => scale(page)).toBe(1);
    await dialog.getByRole('button', { name: 'Close' }).click();
    await expect(dialog).toBeHidden();
  });

  test('GAL-4 a click on the picture zooms in about the pointer and a second click zooms out', async ({ page }) => {
    expect(await open(page)).toBeGreaterThan(0);
    await page.locator('.rp-gimg').first().click();
    const img = page.locator('.rp-lb__img');
    await expect(img).toBeVisible();
    await img.click();
    await expect.poll(() => scale(page)).toBe(2);
    await img.click();
    await expect.poll(() => scale(page)).toBe(1);
  });

  test('GAL-6 previous and next buttons and the arrow keys move, with no wrap (needs several pictures)', async ({ page }) => {
    const count = await open(page);
    test.skip(count < 2, 'the demo product has one picture');
    await page.locator('.rp-gimg').first().click();
    const dialog = page.getByRole('dialog', { name: 'Product pictures' });
    await expect(dialog.getByRole('button', { name: 'Previous picture' })).toBeDisabled();
    await page.keyboard.press('ArrowRight');
    await expect(dialog.locator('.rp-lb__top')).toContainText(`2 / ${count}`);
    for (let i = 0; i < count + 2; i++) await page.keyboard.press('ArrowRight');
    await expect(dialog.locator('.rp-lb__top')).toContainText(`${count} / ${count}`);
    await expect(dialog.getByRole('button', { name: 'Next picture' })).toBeDisabled();
  });
});

test.describe('phone strip', () => {
  test.use({ viewport: { width: 390, height: 844 } });

  test('GAL-2 the counter follows the strip as it scrolls (needs several pictures)', async ({ page }) => {
    const count = await open(page);
    test.skip(count < 2, 'the demo product has one picture');
    const counter = page.locator('.rp-gallery__count');
    await expect(counter).toHaveText(`1 / ${count}`);
    await page.locator('.rp-gallery__list').evaluate((el) => el.scrollTo({ left: el.clientWidth }));
    await expect(counter).toHaveText(`2 / ${count}`);
  });

  test('GAL-2 a single picture shows no counter', async ({ page }) => {
    const count = await open(page);
    test.skip(count > 1, 'this check is for a one-picture product');
    await expect(page.locator('.rp-gallery__count')).toHaveCount(0);
  });
});
