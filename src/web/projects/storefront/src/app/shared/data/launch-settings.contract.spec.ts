import { launchPages } from './launch-pages';
import { launchSettings } from './launch-settings';
import apiDocument from './contract/api-launch-document.json';
import { ContentPage, SiteSettings } from '../models/site-settings.model';

/**
 * SCRUM-206 task 21, ST-1: the document the settings API publishes for the seeded launch set equals the storefront's built-in
 * launch set. The JSON fixture is written by the API test `Seed_PublishedDocument_MatchesTheCommittedContractFixture`
 * (set ROMP_UPDATE_FIXTURES=1 to regenerate it), so a change on either side fails one of the two suites.
 *
 * Known differences, by design (tasks.md "Seed vs the web fallback"):
 * - page status: the seeded pages are drafts, the fallback shows six as published (the fixture is the preview, which shows drafts as live);
 * - business facts (hours, email, delivery, exchange): placeholders in the seed, absent from the built-in set;
 * - `version`: 0 for the built-in set.
 */
const document = apiDocument as unknown as { settings: SiteSettings; pages: Record<string, ContentPage> };
const api = document.settings;

describe('launch set contract (ST-1)', () => {
  it('has the same brand name, tagline and WhatsApp number (digits only, no "+")', () => {
    expect(api.brand.name).toBe(launchSettings.brand.name);
    expect(api.brand.tagline).toBe(launchSettings.brand.tagline);
    expect(api.brand.whatsapp).toBe(launchSettings.brand.whatsapp);
    expect(api.brand.whatsapp).toMatch(/^\d+$/);
  });

  it('has the same navigation tree (a Link with members is the web kind "group")', () => {
    expect(api.navigation).toEqual(launchSettings.navigation);
  });

  it('has the same ages, footer, announcement and popular searches', () => {
    expect(api.ages).toEqual(launchSettings.ages);
    expect(api.footer).toEqual(launchSettings.footer);
    expect(api.announcement).toEqual(launchSettings.announcement);
    expect(api.popularSearches).toEqual(launchSettings.popularSearches);
  });

  it('lists the same pages by slug and title', () => {
    const key = (p: { slug: string; title: string }) => `${p.slug}|${p.title}`;
    expect(api.pages.map(key).sort()).toEqual(launchSettings.pages.map(key).sort());
  });

  it('carries the same content for every page the built-in set has, apart from status', () => {
    for (const [slug, page] of Object.entries(launchPages)) {
      expect({ ...document.pages[slug], status: 'x' }).toEqual({ ...page, status: 'x' });
    }
  });

  it('keeps business facts out of the built-in set while the seed holds placeholders', () => {
    expect(launchSettings.delivery).toEqual([]);
    expect(launchSettings.exchange).toEqual({ windowDays: 0, conditions: [] });
    expect(api.delivery.length).toBeGreaterThan(0);
  });
});
