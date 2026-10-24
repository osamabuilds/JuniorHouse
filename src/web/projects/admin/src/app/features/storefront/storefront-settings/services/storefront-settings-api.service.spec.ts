import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { DraftPart } from '../models';
import { StorefrontSettingsApiService } from './storefront-settings-api.service';

const ROOT = '/api/admin/storefront-settings';

describe('StorefrontSettingsApiService (spec ST-1, ST-14)', () => {
  let api: StorefrontSettingsApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(StorefrontSettingsApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  const url = (path: string) => (r: { url: string }) => r.url.endsWith(`${ROOT}${path}`);

  it('loads the draft', () => {
    api.getDraft().subscribe();

    expect(http.expectOne(url('/draft')).request.method).toBe('GET');
  });

  it('saves the navigation quoting the draft version, and hands back the version and the tree as stored', () => {
    let result: unknown;
    api.save({ part: 'navigation', items: [] }, 4).subscribe((r) => (result = r));

    const request = http.expectOne(url('/navigation'));
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ expectedVersion: 4, items: [] });
    request.flush({ version: 5, data: [{ key: 'girls' }] });

    expect(result).toEqual({ version: 5, patch: { navigation: [{ key: 'girls' }] } });
  });

  const parts: [string, DraftPart, string, object, object][] = [
    ['ages', { part: 'ages', steps: [] }, '/ages', { expectedVersion: 2, steps: [] }, []],
    ['footer', { part: 'footer', columns: [] }, '/footer', { expectedVersion: 2, columns: [] }, []],
    [
      'contact',
      { part: 'contact', items: [], popularSearches: ['a'] },
      '/contact',
      { expectedVersion: 2, items: [], popularSearches: ['a'] },
      { items: [], popularSearches: [] },
    ],
    ['delivery', { part: 'delivery', rules: [], exchange: null }, '/delivery', { expectedVersion: 2, rules: [], exchange: null }, { rules: [], exchange: null }],
  ];

  it.each(parts)('saves %s to its own route', (_name, part, path, body, data) => {
    api.save(part, 2).subscribe();

    const request = http.expectOne(url(path));
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual(body);
    request.flush({ version: 3, data });
  });

  it('lists pages with only the filters that are set (paged)', () => {
    api.listPages({ status: '', page: 2, pageSize: 25 }).subscribe();
    const first = http.expectOne(url('/pages'));
    expect(first.request.params.keys().sort()).toEqual(['page', 'pageSize']);
    first.flush({ items: [], page: 2, pageSize: 25, totalCount: 0, totalPages: 0 });

    api.listPages({ status: 'Published', page: 1, pageSize: 10 }).subscribe();
    const second = http.expectOne(url('/pages'));
    expect(second.request.params.get('status')).toBe('Published');
    second.flush({ items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 0 });
  });

  it('creates and updates a page, quoting the row version on an update (ST-14)', () => {
    const value = { slug: 'care', title: 'Care', metaDescription: 'D', blocks: [] };
    api.createPage(value).subscribe();
    const create = http.expectOne(url('/pages'));
    expect(create.request.method).toBe('POST');
    expect(create.request.body).toEqual(value);
    create.flush({});

    api.updatePage(4, 9, value).subscribe();
    const update = http.expectOne(url('/pages/4'));
    expect(update.request.method).toBe('PUT');
    expect(update.request.body).toEqual({ rowVersion: 9, ...value });
    update.flush({});

    api.changePageStatus(4, 10, 'Published').subscribe();
    const status = http.expectOne(url('/pages/4/status'));
    expect(status.request.body).toEqual({ rowVersion: 10, status: 'Published' });
    status.flush({});
  });

  it('publishes with the version and a note, restores a version, and lists versions by page', () => {
    api.publish(6, 'March').subscribe();
    const publish = http.expectOne(url('/publish'));
    expect(publish.request.body).toEqual({ expectedVersion: 6, note: 'March' });
    publish.flush({});

    api.restore(2, 'oops').subscribe();
    expect(http.expectOne(url('/restore')).request.body).toEqual({ versionNo: 2, note: 'oops' });

    api.listSnapshots(3, 10).subscribe();
    const list = http.expectOne(url('/snapshots'));
    expect(list.request.params.get('page')).toBe('3');
    list.flush({ items: [], page: 3, pageSize: 10, totalCount: 0, totalPages: 0 });
  });
});
