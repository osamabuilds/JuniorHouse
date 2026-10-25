import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideStore } from '@ngrx/store';
import { of } from 'rxjs';
import { provideNavigation } from '../storefront/navigation';
import { provideSiteSettings } from '../storefront/site-settings';
import { NavigationApi } from '../storefront/navigation/services/navigation-api.service';
import { sampleHome } from '../storefront/navigation/testing/sample-home';
import { HomePageContainer } from './home/home-page.container';
import { ProductPageContainer } from './product/product-page.container';

function render<T>(type: new () => T, params: Record<string, string> = {}, query: Record<string, string> = {}): HTMLElement {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideStore({}),
      provideSiteSettings(),
      provideNavigation(),
      { provide: NavigationApi, useValue: { getHome: () => of(sampleHome) } },
      {
        provide: ActivatedRoute,
        useValue: { paramMap: of(convertToParamMap(params)), queryParamMap: of(convertToParamMap(query)), snapshot: {} },
      },
    ],
  });
  const fixture = TestBed.createComponent(type);
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

describe('catalogue pages (design system)', () => {
  it('NAV-10 home has one h1, the new in tiles from the API, an audience tile each and the section headings', () => {
    const el = render(HomePageContainer);
    expect(el.querySelectorAll('h1').length).toBe(1);
    expect(el.querySelectorAll('.rp-grid > li').length).toBe(3);
    expect([...el.querySelectorAll('h2')].map((h) => h.textContent!.trim())).toContain('New in');
    expect([...el.querySelectorAll('a.rp-tile')].map((a) => a.getAttribute('href'))).toEqual(['/girls', '/boys']);
  });

  it('product page has one h1, a labelled size radiogroup and keeps sold-out sizes disabled', () => {
    const el = render(ProductPageContainer, { category: 'girls', type: 'dresses', slug: 'linen-shirt-dress' });
    expect(el.querySelectorAll('h1').length).toBe(1);
    expect(el.querySelector('h1')!.textContent).toContain('Linen shirt dress');
    expect(el.querySelector('[role="radiogroup"][aria-label="Size"]')).not.toBeNull();
    expect(el.querySelector('.rp-size[aria-disabled="true"]')).not.toBeNull();
    expect(el.querySelectorAll('.rp-pdp details').length).toBe(3);
  });

  it('NAV-10 home writes Organization and WebSite structured data from the brand', () => {
    render(HomePageContainer);
    const types = [...document.head.querySelectorAll('script[data-rp="site"]')].map((s) => JSON.parse(s.textContent!)['@type']);
    expect(types).toEqual(['Organization', 'WebSite']);
    expect(document.title).toContain('Romp');
  });
});
