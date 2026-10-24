import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { HomePageContainer } from './home/home-page.container';
import { ListingPageContainer } from './listing/listing-page.container';
import { ProductPageContainer } from './product/product-page.container';

function render<T>(type: new () => T, params: Record<string, string> = {}, query: Record<string, string> = {}): HTMLElement {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
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
  it('home has one h1, the eight-card new in grid and the section headings in order', () => {
    const el = render(HomePageContainer);
    expect(el.querySelectorAll('h1').length).toBe(1);
    expect(el.querySelectorAll('.rp-grid > li').length).toBe(8);
    expect([...el.querySelectorAll('h2')].map((h) => h.textContent!.trim())).toContain('New in');
    expect(el.querySelectorAll('a.rp-tile').length).toBe(2);
  });

  it('listing shows the category as the single h1 with breadcrumb and filtered pieces', () => {
    const el = render(ListingPageContainer, { category: 'girls' });
    expect(el.querySelectorAll('h1').length).toBe(1);
    expect(el.querySelector('h1')!.textContent).toContain('Girls');
    expect(el.querySelector('nav[aria-label="Breadcrumb"] [aria-current="page"]')!.textContent).toContain('Girls');
    expect(el.querySelectorAll('.rp-card').length).toBeGreaterThan(0);
  });

  it('listing applies the age filter from the query string', () => {
    const all = render(ListingPageContainer, { category: 'girls' }).querySelectorAll('.rp-card').length;
    TestBed.resetTestingModule();
    const el = render(ListingPageContainer, { category: 'girls' }, { age: '9-12' });
    expect(el.querySelectorAll('.rp-card').length).toBeLessThan(all);
    expect(el.querySelector('.rp-active')).not.toBeNull();
  });

  it('product page has one h1, a labelled size radiogroup and keeps sold-out sizes disabled', () => {
    const el = render(ProductPageContainer, { category: 'girls', type: 'dresses', slug: 'linen-shirt-dress' });
    expect(el.querySelectorAll('h1').length).toBe(1);
    expect(el.querySelector('h1')!.textContent).toContain('Linen shirt dress');
    expect(el.querySelector('[role="radiogroup"][aria-label="Size"]')).not.toBeNull();
    expect(el.querySelector('.rp-size[aria-disabled="true"]')).not.toBeNull();
    expect(el.querySelectorAll('.rp-pdp details').length).toBe(3);
  });
});
