import { TestBed } from '@angular/core/testing';
import { provideEffects } from '@ngrx/effects';
import { Store, provideStore } from '@ngrx/store';
import { of } from 'rxjs';
import { launchSettings } from '../../../shared/data/launch-settings';
import { ContentPage, SiteSettings } from '../../../shared/models/site-settings.model';
import { SiteSettingsActions, provideSiteSettings } from '../site-settings';
import { SETTINGS_API_URL } from '../site-settings/site-settings.providers';
import { PageApi } from './services/page-api.service';
import { PageActions } from './store/page.actions';
import { PageEffects } from './store/page.effects';
import { pageFeature } from './store/page.reducer';
import { selectPageResults } from './store/page.selectors';

function setup(opts: { url: string | null; api?: Partial<PageApi>; settings?: SiteSettings }) {
  TestBed.configureTestingModule({
    providers: [
      provideStore({ [pageFeature.name]: pageFeature.reducer }),
      provideSiteSettings(),
      provideEffects(PageEffects),
      { provide: SETTINGS_API_URL, useValue: opts.url },
      { provide: PageApi, useValue: opts.api ?? {} },
    ],
  });
  const store = TestBed.inject(Store);
  store.dispatch(SiteSettingsActions.loadSuccess({ settings: opts.settings ?? launchSettings, source: 'api' }));
  return { store, results: () => store.selectSignal(selectPageResults)() };
}

describe('page store (launch mode, no settings API)', () => {
  it('SC-13 serves a published seeded page', () => {
    const { store, results } = setup({ url: null });
    store.dispatch(PageActions.load({ slug: 'delivery' }));
    const r = results()['delivery'];
    expect(r.status).toBe('ok');
    expect(r.status === 'ok' && r.page.title).toBe('Delivery');
  });

  it('ST-8 an unknown slug is not found and a draft page is not served', () => {
    const { store, results } = setup({ url: null });
    store.dispatch(PageActions.load({ slug: 'nope' }));
    store.dispatch(PageActions.load({ slug: 'privacy' }));
    expect(results()['nope']).toEqual({ status: 'notFound' });
    expect(results()['privacy']).toEqual({ status: 'notFound' });
  });

  it('ST-8 an archived page is gone (410)', () => {
    const settings: SiteSettings = { ...launchSettings, pages: launchSettings.pages.map((p) => (p.slug === 'contact' ? { ...p, status: 'archived' as const } : p)) };
    const { store, results } = setup({ url: null, settings });
    store.dispatch(PageActions.load({ slug: 'contact' }));
    expect(results()['contact']).toEqual({ status: 'gone' });
  });

  it('ST-8 a published page with no content is not found rather than blank', () => {
    const settings: SiteSettings = { ...launchSettings, pages: [...launchSettings.pages, { slug: 'extra', title: 'Extra', status: 'published' }] };
    const { store, results } = setup({ url: null, settings });
    store.dispatch(PageActions.load({ slug: 'extra' }));
    expect(results()['extra']).toEqual({ status: 'notFound' });
  });
});

describe('page store (settings API)', () => {
  const page: ContentPage = { slug: 'x', title: 'X', description: 'd', status: 'published', blocks: [] };

  it('loads the page from the API', () => {
    const { store, results } = setup({ url: '/api/storefront/settings', api: { get: () => of({ status: 'ok' as const, page }) } });
    store.dispatch(PageActions.load({ slug: 'x' }));
    expect(results()['x']).toEqual({ status: 'ok', page });
  });
});
