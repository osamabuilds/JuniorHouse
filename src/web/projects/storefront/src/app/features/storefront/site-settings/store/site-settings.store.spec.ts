import { TestBed } from '@angular/core/testing';
import { TransferState } from '@angular/core';
import { provideMockActions } from '@ngrx/effects/testing';
import { Action } from '@ngrx/store';
import { Observable, of, throwError } from 'rxjs';
import { launchSettings } from '../../../../shared/data/launch-settings';
import { SiteSettings } from '../../../../shared/models/site-settings.model';
import { SiteSettingsApi } from '../services/site-settings-api.service';
import { SiteSettingsActions } from './site-settings.actions';
import { SiteSettingsEffects } from './site-settings.effects';
import { initialSiteSettingsState, siteSettingsReducer } from './site-settings.reducer';
import { selectAges, selectFooter, selectHeaderEntries } from './site-settings.selectors';
import { SETTINGS_API_URL } from '../site-settings.providers';

const state = (settings: SiteSettings = launchSettings) => ({ siteSettings: { ...initialSiteSettingsState, settings } });

describe('siteSettings reducer', () => {
  it('ST-13 starts with the launch set, never empty', () => {
    expect(initialSiteSettingsState.settings.navigation.length).toBeGreaterThan(0);
  });

  it('ST-13 keeps the last good settings when a load fails', () => {
    const good = { ...launchSettings, version: 7 };
    let s = siteSettingsReducer(initialSiteSettingsState, SiteSettingsActions.loadSuccess({ settings: good, source: 'api' }));
    s = siteSettingsReducer(s, SiteSettingsActions.loadFailure({ error: 'down' }));
    expect(s.settings.version).toBe(7);
    expect(s.source).toBe('fallback');
  });
});

describe('siteSettings selectors', () => {
  it('SC-1 header entries are the enabled items in order', () => {
    expect(selectHeaderEntries(state() as never).map((i) => i.label)).toEqual(['Girls', 'Boys', 'New in', 'Collections', 'Sale']);
  });

  it('SC-3 renaming, disabling or adding an entry in the settings changes the header with no code change', () => {
    const edited: SiteSettings = {
      ...launchSettings,
      navigation: [
        { ...launchSettings.navigation[0], label: 'Little girls' },
        { ...launchSettings.navigation[1], enabled: false },
        ...launchSettings.navigation.slice(2),
        { key: 'gifts', label: 'Gifts', kind: 'link', target: { kind: 'path', path: '/sale' }, enabled: true, sort: 6, children: [] },
      ],
    };
    expect(selectHeaderEntries(state(edited) as never).map((i) => i.label)).toEqual(['Little girls', 'New in', 'Collections', 'Sale', 'Gifts']);
  });

  it('SC-5 ages are the enabled steps in order', () => {
    const edited = { ...launchSettings, ages: [{ code: '9y', label: '9Y', sort: 2, enabled: true }, { code: '1y', label: '1Y', sort: 1, enabled: true }, { code: 'x', label: 'X', sort: 3, enabled: false }] };
    expect(selectAges(state(edited) as never).map((a) => a.label)).toEqual(['1Y', '9Y']);
  });

  it('SC-12 the footer shows a page link only while that page is published', () => {
    const edited: SiteSettings = { ...launchSettings, pages: launchSettings.pages.map((p) => (p.slug === 'delivery' ? { ...p, status: 'draft' as const } : p)) };
    const labels = selectFooter(state(edited) as never).flatMap((c) => c.links.map((l) => l.label));
    expect(labels).not.toContain('Delivery');
    expect(labels).toContain('Exchanges');
  });

  it('SC-11 footer links carry resolved hrefs, including the WhatsApp link', () => {
    const links = selectFooter(state() as never).flatMap((c) => c.links);
    expect(links.find((l) => l.label === 'WhatsApp')?.href).toBe('https://wa.me/923315613217');
    expect(links.find((l) => l.label === 'Exchanges')?.href).toBe('/exchanges');
  });
});

describe('siteSettings effects', () => {
  const run = (api: Partial<SiteSettingsApi> | null, url: string | null, cached?: SiteSettings): Observable<Action> => {
    TestBed.configureTestingModule({
      providers: [
        SiteSettingsEffects,
        provideMockActions(() => of(SiteSettingsActions.load())),
        { provide: SiteSettingsApi, useValue: api ?? {} },
        { provide: SETTINGS_API_URL, useValue: url },
      ],
    });
    if (cached) TestBed.inject(TransferState).set(SiteSettingsEffects.TRANSFER_KEY, cached);
    return TestBed.inject(SiteSettingsEffects).load$;
  };

  it('uses the launch set when no settings API is configured', async () => {
    const action = await new Promise<Action>((res) => run(null, null).subscribe(res));
    expect(action).toEqual(SiteSettingsActions.loadSuccess({ settings: launchSettings, source: 'launch' }));
  });

  it('ST-1 accepts a valid settings document from the API', async () => {
    const doc = { ...launchSettings, version: 3 };
    const action = await new Promise<Action>((res) => run({ get: () => of(doc) }, '/api/storefront/settings').subscribe(res));
    expect(action).toEqual(SiteSettingsActions.loadSuccess({ settings: doc, source: 'api' }));
  });

  it('ST-13 falls back when the API errors', async () => {
    const action = await new Promise<Action>((res) => run({ get: () => throwError(() => new Error('503')) }, '/api/storefront/settings').subscribe(res));
    expect(action.type).toBe(SiteSettingsActions.loadFailure.type);
  });

  it('abuse case: a document with a dead or unsafe link is rejected like a failure', async () => {
    const bad: SiteSettings = {
      ...launchSettings,
      navigation: [{ key: 'x', label: 'X', kind: 'link', target: { kind: 'external', url: 'javascript:alert(1)' }, enabled: true, sort: 1, children: [] }],
    };
    const action = await new Promise<Action>((res) => run({ get: () => of(bad) }, '/api/storefront/settings').subscribe(res));
    expect(action.type).toBe(SiteSettingsActions.loadFailure.type);
  });

  it('SC-18 the client reuses the settings the server transferred, with no second request', async () => {
    const cached = { ...launchSettings, version: 9 };
    const get = vi.fn(() => of(launchSettings));
    const action = await new Promise<Action>((res) => run({ get }, '/api/storefront/settings', cached).subscribe(res));
    expect(get).not.toHaveBeenCalled();
    expect(action).toEqual(SiteSettingsActions.loadSuccess({ settings: cached, source: 'api' }));
  });
});
