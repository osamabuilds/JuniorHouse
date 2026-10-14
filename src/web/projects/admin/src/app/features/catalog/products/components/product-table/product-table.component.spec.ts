import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { productRow } from '@app/testing/catalog-fixtures';
import { textOf } from '@app/testing/production-fixtures';
import { ProductTableComponent } from './product-table.component';

describe('ProductTableComponent (spec PRD-11)', () => {
  function create(products = [productRow(), productRow({ id: 8, name: 'Basic Tee', status: 'Published', publishedAt: '2026-10-10T00:00:00Z', sellableVariants: 6 })], loading = false) {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(ProductTableComponent);
    fixture.componentRef.setInput('products', products);
    fixture.componentRef.setInput('loading', loading);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement };
  }

  it('is a real table whose product names are links to the editor', () => {
    const { root } = create();

    expect(root.querySelectorAll('th[scope="col"]').length).toBe(5);
    const link = root.querySelector('tbody a') as HTMLAnchorElement;
    expect(link.getAttribute('href')).toBe('/catalog/products/7');
    expect(link.textContent).toContain('Floral Summer Dress');
  });

  it('shows how many variants can be bought now', () => {
    const { fixture } = create();

    expect(textOf(fixture)).toContain('2 of 6');
    expect(textOf(fixture)).toContain('6 of 6');
  });

  it('says so when there is nothing', () => {
    expect(textOf(create([]).fixture)).toContain('No products found.');
  });

  it('says so while loading', () => {
    expect(textOf(create([], true).fixture)).toContain('Loading…');
  });
});
