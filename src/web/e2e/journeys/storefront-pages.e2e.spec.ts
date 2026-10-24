import AxeBuilder from '@axe-core/playwright';
import { APIRequestContext, expect, test } from '@playwright/test';

/** SCRUM-205 content pages: real status codes, SEO tags in the server HTML, links that resolve, axe. */
const BASE = process.env['STOREFRONT_BASE_URL'] ?? 'http://localhost:4000';
const AXE_TAGS = ['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'];
const PUBLISHED = ['delivery', 'exchanges', 'size-guide', 'our-story', 'made-in-pakistan', 'contact'];

/**
 * What the shop is serving: the published settings when something has been published (the API document through the storefront's own
 * forwarder), otherwise the built-in launch set (404 from the API). Tests that depend on which pages are live read it, so they hold on a
 * fresh stack and on one where the settings have been published.
 */
async function liveSettings(request: APIRequestContext): Promise<{ published: Set<string>; delivery: { city: string }[] }> {
  const res = await request.get('/api/storefront/settings');
  if (res.status() !== 200) return { published: new Set(PUBLISHED), delivery: [] };
  const doc = (await res.json()) as { pages: { slug: string; status: string }[]; delivery: { city: string }[] };
  return { published: new Set(doc.pages.filter((p) => p.status === 'published').map((p) => p.slug)), delivery: doc.delivery };
}

test.describe('content pages in the server HTML', () => {
  test.use({ baseURL: BASE });

  for (const slug of PUBLISHED) {
    test(`SC-13 /${slug} returns 200 with one h1, a unique title, description, canonical, Open Graph, Twitter tags and BreadcrumbList`, async ({ request }) => {
      const res = await request.get(`/${slug}`);
      expect(res.status()).toBe(200);
      const html = await res.text();
      expect(html.match(/<h1\b/g)?.length).toBe(1);
      expect(html).toMatch(/<title>[^<]+ \| Romp<\/title>/);
      expect(html).toMatch(/<meta name="description" content="[^"]+"/);
      expect(html).toContain(`<link rel="canonical" href="http://localhost:4000/${slug}"`);
      expect(html).toContain('property="og:title"');
      expect(html).toContain('name="twitter:card"');
      expect(html).toContain('"@type":"BreadcrumbList"');
      expect(html).toContain('aria-label="Breadcrumb"');
      expect(html).not.toContain('name="robots"');
    });
  }

  test('SC-13 every page has its own title', async ({ request }) => {
    const titles = await Promise.all(PUBLISHED.map(async (s) => /<title>([^<]+)<\/title>/.exec(await (await request.get(`/${s}`)).text())![1]));
    expect(new Set(titles).size).toBe(PUBLISHED.length);
  });

  test('ST-8 an unknown address returns a real 404 with noindex, not a soft 404', async ({ request }) => {
    for (const path of ['/no-such-page', '/a/b/c']) {
      const res = await request.get(path);
      expect(res.status()).toBe(404);
      expect(await res.text()).toContain('content="noindex, nofollow"');
    }
  });

  test('ST-8 privacy and terms are served exactly when they are published, and are a real 404 while drafts', async ({ request }) => {
    const { published } = await liveSettings(request);
    for (const slug of ['privacy', 'terms']) {
      expect((await request.get(`/${slug}`)).status(), slug).toBe(published.has(slug) ? 200 : 404);
    }
  });

  test('SC-12 every internal footer link resolves to a page that returns 200', async ({ request }) => {
    const html = await (await request.get('/delivery')).text();
    const footer = html.slice(html.indexOf('<footer'), html.indexOf('</footer>'));
    const hrefs = [...footer.matchAll(/data-footer-link[^>]*href="(\/[^"]*)"/g)].map((m) => m[1]);
    expect(hrefs.length).toBeGreaterThan(5);
    const dead: string[] = [];
    for (const href of hrefs) {
      // Listing routes (/girls, /boys, /new-in, ...) arrive with stories 3 to 5; pages must resolve now.
      if (PUBLISHED.includes(href.slice(1))) {
        if ((await request.get(href)).status() !== 200) dead.push(href);
      }
    }
    expect(dead).toEqual([]);
    for (const slug of PUBLISHED.filter((s) => s !== 'delivery' && s !== 'exchanges' && s !== 'size-guide' && s !== 'contact')) expect(hrefs).toContain(`/${slug}`);
    const { published } = await liveSettings(request);
    for (const slug of ['privacy', 'terms']) {
      if (published.has(slug)) expect(hrefs).toContain(`/${slug}`);
      else expect(hrefs).not.toContain(`/${slug}`);
    }
  });

  test('SC-14 the contact page shows the WhatsApp number from the settings and the delivery page shows only facts held in the settings', async ({ request }) => {
    expect(await (await request.get('/contact')).text()).toContain('href="https://wa.me/923315613217"');
    const delivery = await (await request.get('/delivery')).text();
    const { delivery: rules } = await liveSettings(request);
    if (rules.length === 0) {
      // Nothing entered yet: no invented prices or times.
      expect(delivery).not.toMatch(/Rs. ?d/);
      expect(delivery).not.toMatch(/d+ to d+ days/);
    } else {
      for (const rule of rules) expect(delivery).toContain(rule.city);
    }
  });
});

for (const vp of [{ name: 'phone', width: 390, height: 844 }, { name: 'tablet', width: 768, height: 1024 }, { name: 'desktop', width: 1280, height: 900 }]) {
  test.describe(`content pages at ${vp.name}`, () => {
    test.use({ baseURL: BASE, viewport: { width: vp.width, height: vp.height } });

    for (const slug of ['size-guide', 'contact', 'no-such-page']) {
      test(`SC-19 /${slug} has no axe violations and no horizontal scroll`, async ({ page }) => {
        await page.goto(`/${slug}`);
        await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
        const results = await new AxeBuilder({ page }).withTags(AXE_TAGS).analyze();
        expect(results.violations.map((v) => `${v.id}: ${v.nodes.map((n) => JSON.stringify(n.target)).join(', ')}`)).toEqual([]);
        expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(0);
      });
    }
  });
}
