import AxeBuilder from '@axe-core/playwright';
import { Page, expect, test } from '@playwright/test';
import { API } from '../support';

/**
 * SCRUM-206 task 23 (ST-15): the admin storefront-settings screens at phone width (390 px). Every screen has no axe violations (WCAG 2
 * A and AA up to 2.2), no horizontal scroll, form controls and buttons at least 44 px high, and every reorder works with buttons and
 * the keyboard alone (no drag, WCAG 2.5.7). Runs against the docker admin (:4201) and API (:8080).
 */
const AXE_TAGS = ['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'];
const MIN_TARGET = 44;

const SCREENS = [
  { name: 'Navigation', path: '/storefront/navigation', h1: 'Navigation' },
  { name: 'Ages', path: '/storefront/ages', h1: 'Ages' },
  { name: 'Footer', path: '/storefront/footer', h1: 'Footer' },
  { name: 'Settings', path: '/storefront/contact', h1: /Settings/ },
  { name: 'Delivery and exchange', path: '/storefront/delivery', h1: /Delivery/ },
  { name: 'Pages list', path: '/storefront/pages', h1: 'Pages' },
  { name: 'New page', path: '/storefront/pages/new', h1: 'New page' },
  { name: 'Publish', path: '/storefront/publish', h1: 'Publish' },
];

async function open(page: Page, path: string, h1: string | RegExp): Promise<void> {
  // The admin index requests Google Fonts; the page is usable once the DOM is ready.
  await page.goto(path, { waitUntil: 'domcontentloaded' });
  await expect(page.getByRole('heading', { level: 1, name: h1 })).toBeVisible({ timeout: 20_000 });
  await expect(page.getByText(/^Loading/)).toHaveCount(0, { timeout: 20_000 });
}

const summary = (r: Awaited<ReturnType<AxeBuilder['analyze']>>) =>
  r.violations.map((v) => `${v.id}: ${v.nodes.map((n) => JSON.stringify(n.target)).join(', ')}`);

/** Visible controls in the screen's main area that are shorter than 44 px (a checkbox is measured by the label around it). */
async function smallTargets(page: Page): Promise<string[]> {
  return page.$$eval(
    'main :is(button, a, select, textarea, input:not([type=checkbox]):not([type=radio]):not([type=hidden]), label.check)',
    (els, min) =>
      els
        .filter((e) => (e as HTMLElement).offsetParent !== null)
        .filter((e) => e.getBoundingClientRect().height < min)
        // a link inside running text is exempt (WCAG 2.5.8 inline exception)
        .filter((e) => !(e.tagName === 'A' && e.closest('p, li, small') && !e.classList.contains('button')))
        .map((e) => e.outerHTML.replace(/\s+/g, ' ').slice(0, 110)),
    MIN_TARGET,
  );
}

test.describe('storefront settings screens at 390 px', () => {
  test.use({ viewport: { width: 390, height: 844 } });

  for (const screen of SCREENS) {
    test(`ST-15 ${screen.name}: no axe violations, no horizontal scroll, targets at least 44 px`, async ({ page }) => {
      await open(page, screen.path, screen.h1);
      expect(summary(await new AxeBuilder({ page }).withTags(AXE_TAGS).analyze())).toEqual([]);
      expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(0);
      expect(await smallTargets(page)).toEqual([]);
    });
  }

  test('ST-15 the page editor passes too', async ({ page, request }) => {
    const list = (await (await request.get(`${API}/api/admin/storefront-settings/pages?pageSize=100`)).json()) as { items: { id: number; slug: string }[] };
    const delivery = list.items.find((p) => p.slug === 'delivery');
    expect(delivery).toBeTruthy();
    await open(page, `/storefront/pages/${delivery!.id}`, 'Edit page');
    expect(summary(await new AxeBuilder({ page }).withTags(AXE_TAGS).analyze())).toEqual([]);
    expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(0);
    expect(await smallTargets(page)).toEqual([]);
  });

  test('ST-15 the primary admin navigation fits at 390 px', async ({ page }) => {
    await open(page, '/storefront/navigation', 'Navigation');
    const links = page.getByRole('navigation', { name: 'Primary' }).getByRole('link');
    const box = await page.getByRole('navigation', { name: 'Primary' }).boundingBox();
    expect(box!.x + box!.width).toBeLessThanOrEqual(390);
    for (const link of await links.all()) {
      expect((await link.boundingBox())!.height).toBeGreaterThanOrEqual(MIN_TARGET);
    }
  });

  test('ST-15 navigation entries reorder with the keyboard and buttons only', async ({ page }) => {
    await open(page, '/storefront/navigation', 'Navigation');
    const labels = () => page.locator('ul.nav-tree > li > fieldset input[id$="-label"]').evaluateAll((els) => els.map((e) => (e as HTMLInputElement).value));
    const before = await labels();
    expect(before.length).toBeGreaterThan(2);

    // Move the second entry down with the keyboard: focus its button and press Enter.
    const moveDown = page.getByRole('button', { name: `Move ${before[1]} down` });
    await moveDown.focus();
    await page.keyboard.press('Enter');
    await expect.poll(labels).toEqual([before[0], before[2], before[1], ...before.slice(3)]);

    // And back up again, so nothing is left unsaved in the draft (the change was never saved either way).
    await page.getByRole('button', { name: `Move ${before[1]} up` }).focus();
    await page.keyboard.press('Enter');
    await expect.poll(labels).toEqual(before);

    // Indent and outdent are buttons too.
    await expect(page.getByRole('button', { name: new RegExp(`^Make ${before[1]} a member of the entry above`) })).toBeVisible();
  });
});
