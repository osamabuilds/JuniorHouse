import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';

/**
 * Listing filter bar, sheet and swatches (docs/specs/storefront/listing-filter-bar, FLT-1..13). Runs against the storefront container.
 */
const BASE = process.env['STOREFRONT_BASE_URL'] ?? 'http://localhost:4000';
const AXE_TAGS = ['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'];
const PAGES = ['/girls', '/search?q=dress'];

test.use({ baseURL: BASE });

async function open(page: Page, path: string): Promise<void> {
  await page.goto(path, { waitUntil: 'networkidle' });
  await page.waitForTimeout(300);
}

for (const path of PAGES) {
  test.describe(`filter bar on ${path} at desktop`, () => {
    test.use({ viewport: { width: 1280, height: 900 } });

    test('FLT-1 the bar is sticky under the header with chips, the count and a native sort select', async ({ page }) => {
      await open(page, path);
      const bar = page.locator('.rp-filterbar');
      await expect(bar).toBeVisible();
      expect(await bar.evaluate((el) => getComputedStyle(el).position)).toBe('sticky');
      const desktop = page.locator('.rp-filterbar__desktop');
      await expect(desktop.locator('summary.rp-chip').first()).toBeVisible();
      await expect(desktop.locator('.rp-count-note')).toContainText(/piece/);
      await expect(desktop.locator('select[name="sort"]')).toBeVisible();
      await expect(page.locator('.rp-filterbar__phone')).toBeHidden();
    });

    test('FLT-2 a chip opens a small panel; Escape closes it and focus returns to the chip', async ({ page }) => {
      await open(page, path);
      const chip = page.locator('.rp-filterbar__desktop summary.rp-chip', { hasText: 'Size' });
      test.skip((await chip.count()) === 0, 'this listing has no sizes to filter by');
      await chip.click();
      const panel = page.locator('.rp-filterbar__desktop .rp-chip-pop[open] .rp-chip-pop__panel');
      await expect(panel).toBeVisible();
      await expect(panel.locator('input[name="size"]').first()).toBeVisible();
      await page.keyboard.press('Escape');
      await expect(panel).toBeHidden();
      await expect(chip).toBeFocused();
    });

    test('FLT-5 choosing a size applies it without a full reload and the chip shows the selection', async ({ page }) => {
      await open(page, path);
      const chip = page.locator('.rp-filterbar__desktop summary.rp-chip', { hasText: 'Size' });
      test.skip((await chip.count()) === 0, 'this listing has no sizes to filter by');
      await page.evaluate(() => ((window as unknown as { __alive: boolean }).__alive = true));
      await chip.click();
      await page.locator('.rp-filterbar__desktop .rp-chip-pop[open] input[name="size"]').first().check();
      await page.locator('.rp-filterbar__desktop .rp-chip-pop[open] button[type="submit"]').click();
      await expect(page).toHaveURL(/size=/);
      expect(await page.evaluate(() => (window as unknown as { __alive?: boolean }).__alive)).toBe(true);
      await expect(page.locator('.rp-filterbar__desktop summary.rp-chip.is-on', { hasText: 'Size' })).toBeVisible();
      await expect(page.locator('.rp-filterbar__desktop a.rp-chip__x')).toHaveAttribute('aria-label', 'Remove Size filter');
    });
  });
}

for (const path of PAGES) {
  test.describe(`filter sheet on ${path} at phone`, () => {
    test.use({ viewport: { width: 390, height: 844 } });

    test('FLT-3 FLT-10 one "Filter and sort" chip opens an 88% sheet with Show N pieces; Escape closes it and focus returns', async ({ page }) => {
      await open(page, path);
      const sheet = page.locator('.rp-filterbar__sheet');
      await expect(page.locator('.rp-filterbar__desktop')).toBeHidden();
      await sheet.locator('summary').click();
      const form = sheet.locator('form');
      await expect(form).toBeVisible();
      const box = (await form.boundingBox())!;
      expect(Math.abs(box.height - 844 * 0.88)).toBeLessThanOrEqual(2);
      await expect(form.getByRole('button', { name: /^Show \d+ pieces?$/ })).toBeVisible();
      await expect(form.locator('legend', { hasText: 'Sort' })).toBeVisible();
      expect(await page.evaluate(() => document.body.classList.contains('rp-no-scroll'))).toBe(true);
      await page.keyboard.press('Escape');
      await expect(form).toBeHidden();
      await expect(sheet.locator('summary')).toBeFocused();
      expect(await page.evaluate(() => document.body.classList.contains('rp-no-scroll'))).toBe(false);
    });

    test('FLT-10 the Close button and a tap on the scrim also close the sheet', async ({ page }) => {
      await open(page, path);
      const sheet = page.locator('.rp-filterbar__sheet');
      await sheet.locator('summary').click();
      await sheet.getByRole('button', { name: 'Close filters' }).click();
      await expect(sheet.locator('form')).toBeHidden();
      await sheet.locator('summary').click();
      await page.mouse.click(195, 30);
      await expect(sheet.locator('form')).toBeHidden();
    });

    test('FLT-4 FLT-13 colour swatches name each colour in words, and the button count follows what is ticked', async ({ page }) => {
      await open(page, path);
      const sheet = page.locator('.rp-filterbar__sheet');
      await sheet.locator('summary').click();
      const swatches = sheet.locator('.rp-swatchbox');
      test.skip((await swatches.count()) === 0, 'this listing has no colours');
      await expect(swatches.first().locator('.rp-swatchbox__name')).not.toBeEmpty();
      const request = page.waitForRequest((r) => r.url().includes('pageSize=1'));
      await swatches.first().click();
      expect((await request).url()).toContain('colour=');
    });
  });
}

for (const vp of [
  { name: 'phone', width: 390, height: 844 },
  { name: 'tablet', width: 768, height: 1024 },
  { name: 'desktop', width: 1280, height: 900 },
]) {
  test.describe(`quality at ${vp.name}`, () => {
    test.use({ viewport: { width: vp.width, height: vp.height } });

    for (const path of PAGES) {
      test(`FLT-9 ${path}: no horizontal scroll, no axe violations, no layout shift, controls at least 44px`, async ({ page }) => {
        await page.addInitScript(() => {
          (window as unknown as { __cls: number }).__cls = 0;
          new PerformanceObserver((list) => {
            for (const e of list.getEntries() as unknown as { value: number; hadRecentInput: boolean }[]) {
              if (!e.hadRecentInput) (window as unknown as { __cls: number }).__cls += e.value;
            }
          }).observe({ type: 'layout-shift', buffered: true });
        });
        await open(page, path);
        const results = await new AxeBuilder({ page }).withTags(AXE_TAGS).analyze();
        expect(results.violations.map((v) => v.id + ': ' + v.nodes.map((n) => JSON.stringify(n.target)).join(', '))).toEqual([]);
        const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
        expect(overflow).toBeLessThanOrEqual(0);
        const small = await page.$$eval('.rp-filterbar summary, .rp-filterbar button, .rp-filterbar select', (els) =>
          els.filter((e) => (e as HTMLElement).offsetParent !== null).filter((e) => e.getBoundingClientRect().height < 44).map((e) => e.outerHTML.slice(0, 80)),
        );
        expect(small).toEqual([]);
        expect(await page.evaluate(() => (window as unknown as { __cls: number }).__cls)).toBeLessThanOrEqual(0.1);
      });
    }
  });
}

test.describe('without scripting', () => {
  test.use({ viewport: { width: 1280, height: 900 }, javaScriptEnabled: false });

  test('FLT-6 the filters work as a GET form and the server returns the filtered page', async ({ page }) => {
    await page.goto('/girls');
    const chip = page.locator('.rp-filterbar__desktop summary.rp-chip', { hasText: 'Size' });
    test.skip((await chip.count()) === 0, 'this listing has no sizes to filter by');
    await chip.click();
    await page.locator('.rp-filterbar__desktop .rp-chip-pop[open] input[name="size"]').first().check();
    await page.locator('.rp-filterbar__desktop .rp-chip-pop[open] button[type="submit"]').click();
    await expect(page).toHaveURL(/size=/);
    await expect(page.locator('h1')).toBeVisible();
    await expect(page.locator('.rp-filterbar__desktop summary.rp-chip.is-on', { hasText: 'Size' })).toBeVisible();
  });
});
