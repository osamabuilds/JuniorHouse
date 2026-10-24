import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';

/**
 * SCRUM-204 design foundation. Runs against the storefront container started by `docker compose up storefront`
 * (built with the development configuration, which keeps the dev-only /_foundations page).
 */
const BASE = process.env['STOREFRONT_BASE_URL'] ?? 'http://localhost:4000';

const VIEWPORTS = [
  { name: 'phone', width: 390, height: 844, margin: 16 },
  { name: 'tablet', width: 768, height: 1024, margin: 32 },
  { name: 'desktop', width: 1280, height: 900, margin: 56 },
];

for (const vp of VIEWPORTS) {
  test.describe(`design foundation at ${vp.name} (${vp.width}px)`, () => {
    test.use({ baseURL: BASE, viewport: { width: vp.width, height: vp.height } });

    test(`DSF-13 DSF-15 has no axe violations, no horizontal scroll and the ${vp.margin}px page margin`, async ({ page }) => {
      await page.goto('/_foundations');
      await expect(page.getByRole('heading', { level: 1 })).toBeVisible();

      const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze();
      expect(results.violations.map((v) => `${v.id}: ${v.help}`)).toEqual([]);

      const metrics = await page.evaluate(() => ({
        scrollWidth: document.documentElement.scrollWidth,
        clientWidth: document.documentElement.clientWidth,
        margin: parseFloat(getComputedStyle(document.querySelector('main .rp-wrap')!).paddingLeft),
      }));
      expect(metrics.scrollWidth).toBeLessThanOrEqual(metrics.clientWidth);
      expect(metrics.margin).toBe(vp.margin);
    });

    test('DSF-5 DSF-8 images are 4:5 and every control is at least 44px tall or has a 44px hit area', async ({ page }) => {
      await page.goto('/_foundations');
      const ratios = await page.$$eval('.rp-media', (els) => els.map((e) => e.clientWidth / e.clientHeight));
      expect(ratios.length).toBeGreaterThan(0);
      for (const r of ratios) {
        expect(r).toBeCloseTo(0.8, 2);
      }
      const small = await page.$$eval('button, input, a', (els) =>
        els
          // controls inside a closed dialog are not rendered, so they have no size to check
          .filter((e) => e.getClientRects().length > 0)
          .filter((e) => e.getBoundingClientRect().height < 44)
          // links get an invisible 44px hit area through ::after (design system)
          .filter((e) => !(e.classList.contains('rp-link') && getComputedStyle(e, '::after').content !== 'none'))
          .map((e) => e.outerHTML.slice(0, 80)),
      );
      expect(small).toEqual([]);
    });
  });
}

test.describe('design foundation behaviour', () => {
  test.use({ baseURL: BASE, viewport: { width: 1280, height: 900 } });

  test('DSF-3 loads fonts from its own origin only', async ({ page }) => {
    const external: string[] = [];
    page.on('request', (r) => {
      if (!r.url().startsWith(BASE) && !r.url().startsWith('data:')) external.push(r.url());
    });
    await page.goto('/_foundations');
    await page.evaluate(() => document.fonts.ready);
    expect(external).toEqual([]);
  });

  test('DSF-9 the server HTML carries the priority preload and the noindex tag', async ({ request }) => {
    const html = await (await request.get(`${BASE}/_foundations`)).text();
    expect(html).toMatch(/<link[^>]+rel="preload"[^>]+fetchpriority="high"/);
    expect(html).toMatch(/<meta name="robots" content="noindex, nofollow"/);
    expect(html).toContain('<html lang="en"');
  });

  test('DSF-11 the second view is requested only after the pointer enters', async ({ page }) => {
    // The foundations page shows a second view on the first image.
    const requests: string[] = [];
    page.on('request', (r) => requests.push(r.url()));
    await page.goto('/_foundations');
    await page.waitForLoadState('networkidle');
    expect(requests.some((u) => u.includes('/placeholders/fabric.svg'))).toBe(false);
    const firstCard = page.locator('rp-image').first();
    await expect(firstCard.locator('.rp-image__second')).toHaveCount(0);
    await firstCard.hover();
    await expect(firstCard.locator('.rp-image__second')).toHaveCount(1);
    await expect.poll(() => requests.some((u) => u.includes('/placeholders/fabric.svg'))).toBe(true);
  });

  test('DSF-12 reduced motion removes the crossfade', async ({ browser }) => {
    const context = await browser.newContext({ baseURL: BASE, reducedMotion: 'reduce' });
    const page = await context.newPage();
    await page.goto('/_foundations');
    const firstCard = page.locator('rp-image').first();
    await firstCard.hover();
    const duration = await firstCard.locator('.rp-image__second').evaluate((e) => getComputedStyle(e).transitionDuration);
    expect(parseFloat(duration)).toBeLessThan(0.001); // the design system sets 0.00001s, effectively none
    await context.close();
  });
});
