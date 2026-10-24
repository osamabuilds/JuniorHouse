import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ContentPage } from '../../../../shared/models/site-settings.model';
import { PageApi, PageResult } from './page-api.service';

describe('PageApi', () => {
  function setup() {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    return { api: TestBed.inject(PageApi), http: TestBed.inject(HttpTestingController) };
  }
  const page: ContentPage = { slug: 'x', title: 'X', description: 'd', status: 'published', blocks: [] };

  function result(status: number | null): PageResult {
    const { api, http } = setup();
    let out: PageResult | undefined;
    api.get('x').subscribe((r) => (out = r));
    const req = http.expectOne('/api/storefront/pages/x');
    if (status === null) req.flush(page);
    else req.flush('', { status, statusText: 'x' });
    http.verify();
    return out!;
  }

  it('ST-8 returns the page on 200', () => expect(result(null)).toEqual({ status: 'ok', page }));
  it('ST-8 maps 404 to notFound', () => expect(result(404)).toEqual({ status: 'notFound' }));
  it('ST-8 maps 410 to gone', () => expect(result(410)).toEqual({ status: 'gone' }));
  it('an API failure is an error, never a 404', () => expect(result(500)).toEqual({ status: 'error' }));

  it('encodes the slug in the URL', () => {
    const { api, http } = setup();
    api.get('a/b?c').subscribe();
    http.expectOne('/api/storefront/pages/a%2Fb%3Fc').flush(page);
  });
});
